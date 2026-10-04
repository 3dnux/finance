#!/usr/bin/env bash
# Despliega Paga para Morir en devnet (o en un validador local) y deja el backend listo.
#
#   scripts/deploy.sh                 # devnet
#   scripts/deploy.sh --start-backend # devnet y arranca el backend al final
#   scripts/deploy.sh --cluster localnet   # contra solana-test-validator (pruebas)
#
# Se puede correr varias veces: lo que ya está hecho se salta (llaves, config) y el
# programa se actualiza si cambió. Necesita: Solana CLI, Rust (cargo build-sbf) y .NET 8.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# ---------- Opciones ----------
CLUSTER="devnet"
START_BACKEND=0
SKIP_BUILD=0
FEE_BPS=2000
SETTLE_TIMEOUT=3600
USDC_MINT=""
SERVER_SOL=0.5
DEVNET_USDC="4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU" # USDC de Circle en devnet

usage() {
  sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'
  cat <<'EOF'
Opciones:
  --cluster devnet|localnet  Red (por defecto devnet).
  --rpc URL                  RPC propio (por defecto el público de la red).
  --usdc-mint MINT           Mint de USDC (devnet: el de Circle; localnet: se crea uno de prueba).
  --fee-bps N                Comisión de la casa en puntos básicos (por defecto 2000 = 20%).
  --start-backend            Arranca el backend al terminar.
  --skip-build               No recompila el programa.
  -h, --help                 Esta ayuda.
EOF
}

RPC=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --cluster) CLUSTER="$2"; shift 2 ;;
    --rpc) RPC="$2"; shift 2 ;;
    --usdc-mint) USDC_MINT="$2"; shift 2 ;;
    --fee-bps) FEE_BPS="$2"; shift 2 ;;
    --start-backend) START_BACKEND=1; shift ;;
    --skip-build) SKIP_BUILD=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Opción desconocida: $1" >&2; usage; exit 1 ;;
  esac
done

case "$CLUSTER" in
  devnet) RPC="${RPC:-https://api.devnet.solana.com}"; USDC_MINT="${USDC_MINT:-$DEVNET_USDC}" ;;
  localnet) RPC="${RPC:-http://127.0.0.1:8899}" ;;
  *) echo "Red no soportada: $CLUSTER (usa devnet o localnet). Mainnet todavía no." >&2; exit 1 ;;
esac

KEYS="$ROOT/keys"
WORK="$ROOT/.deploy/$CLUSTER"
ADMIN="$KEYS/admin.json"
SERVER="$KEYS/server.json"
PROGRAM_KEYPAIR="$ROOT/target/deploy/paga_para_morir-keypair.json"
SO="$ROOT/target/deploy/paga_para_morir.so"
PPM_DIR="$ROOT/target/ppm"

# ---------- Utilidades ----------
step() { printf '\n\033[1;31m==>\033[0m \033[1m%s\033[0m\n' "$*"; }
info() { printf '    %s\n' "$*"; }
fail() { printf '\n\033[1;31mError:\033[0m %s\n' "$*" >&2; exit 1; }

need() {
  command -v "$1" >/dev/null 2>&1 || fail "Falta '$1'. $2"
}

lamports() { solana balance --lamports -u "$RPC" "$1" 2>/dev/null | awk '{print $1}'; }
sol() { awk -v l="$1" 'BEGIN { printf "%.3f", l / 1e9 }'; }
ppm() { dotnet "$PPM_DIR/ppm.dll" "$@" --url "$RPC" --program "$PROGRAM_ID"; }

new_keypair() {
  mkdir -p "$(dirname "$1")"
  solana-keygen new --no-bip39-passphrase --silent --outfile "$1" >/dev/null
  chmod 600 "$1"
}

# Pide SOL de prueba hasta tener al menos $2 lamports (devnet limita los airdrops).
fund() {
  local who="$1" want="$2" have
  have="$(lamports "$who")"
  for attempt in 1 2 3 4 5; do
    (( have >= want )) && return 0
    info "Pidiendo SOL de prueba ($(sol "$have") de $(sol "$want") SOL)…"
    solana airdrop 2 "$who" -u "$RPC" >/dev/null 2>&1 || sleep $((attempt * 3))
    have="$(lamports "$who")"
  done
  (( have >= want )) && return 0
  fail "La billetera $who tiene $(sol "$have") SOL y necesita $(sol "$want").
       Pide SOL en https://faucet.solana.com (red Devnet) y vuelve a correr el script."
}

# ---------- 1. Herramientas ----------
step "Revisando herramientas"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.dotnet:$PATH"
need solana "Instala Solana CLI: sh -c \"\$(curl -sSfL https://release.anza.xyz/stable/install)\""
need solana-keygen "Viene con Solana CLI."
need cargo "Instala Rust: https://rustup.rs"
need dotnet "Instala .NET 8: https://dotnet.microsoft.com/download"
[[ "$CLUSTER" == localnet ]] && need spl-token "Viene con Solana CLI."
solana cluster-version -u "$RPC" >/dev/null 2>&1 \
  || fail "No hay conexión con $RPC.$([[ "$CLUSTER" == localnet ]] && echo ' ¿Está corriendo solana-test-validator?')"
info "Red: $CLUSTER ($RPC)"

# ---------- 2. Llaves ----------
step "Llaves (carpeta keys/, nunca se sube al repo)"
[[ -f "$ADMIN" ]] || { new_keypair "$ADMIN"; info "Nueva llave de admin (despliega y configura el juego)."; }
[[ -f "$SERVER" ]] || { new_keypair "$SERVER"; info "Nueva llave del servidor (crea salas y paga premios)."; }
[[ -f "$PROGRAM_KEYPAIR" ]] || { new_keypair "$PROGRAM_KEYPAIR"; info "Nueva dirección para el programa."; }
ADMIN_PK="$(solana-keygen pubkey "$ADMIN")"
SERVER_PK="$(solana-keygen pubkey "$SERVER")"
PROGRAM_ID="$(solana-keygen pubkey "$PROGRAM_KEYPAIR")"
info "Admin:     $ADMIN_PK"
info "Servidor:  $SERVER_PK"
info "Programa:  $PROGRAM_ID"

# ---------- 3. Program ID en el código ----------
step "Program ID en el código"
OLD_ID="$(grep -oE 'declare_id!\("[1-9A-HJ-NP-Za-km-z]+"\)' programs/paga-para-morir/src/lib.rs | cut -d'"' -f2)"
if [[ "$OLD_ID" != "$PROGRAM_ID" ]]; then
  FILES=$(grep -rl --exclude-dir=target --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj \
            --exclude-dir=Library --exclude-dir=.deploy --exclude-dir=keys "$OLD_ID" . || true)
  for f in $FILES; do
    sed -i.bak "s/$OLD_ID/$PROGRAM_ID/g" "$f" && rm -f "$f.bak"
    info "Actualizado: ${f#./}"
  done
  info "Sube estos cambios al repo para que todos usen el mismo programa."
  SKIP_BUILD=0
else
  info "Ya es $PROGRAM_ID."
fi

# ---------- 4. Compilar ----------
if [[ "$SKIP_BUILD" == 0 || ! -f "$SO" ]]; then
  step "Compilando el programa"
  cargo build-sbf --manifest-path programs/paga-para-morir/Cargo.toml 2>&1 | tail -n 2
fi
[[ -f "$SO" ]] || fail "No se generó $SO."

step "Compilando la herramienta ppm"
dotnet build dotnet/PagaParaMorir.DevTool -c Release -o "$PPM_DIR" -v quiet -nologo >/dev/null \
  || fail "No compiló dotnet/PagaParaMorir.DevTool."

# ---------- 5. ¿Hace falta desplegar? ----------
mkdir -p "$WORK"
DEPLOY=1
SO_BYTES=$(wc -c < "$SO")
# Renta de la cuenta del programa (~7 SOL por MB) + buffer temporal + comisiones.
NEEDED=$(( SO_BYTES * 2 * 6960 + 1000000000 ))
if solana program show "$PROGRAM_ID" --keypair "$ADMIN" -u "$RPC" >/dev/null 2>&1; then
  NEEDED=$(( SO_BYTES * 6960 + 500000000 )) # actualizar solo necesita el buffer
  if solana program dump "$PROGRAM_ID" "$WORK/onchain.so" --keypair "$ADMIN" -u "$RPC" >/dev/null 2>&1 &&
     cmp -s -n "$SO_BYTES" "$SO" "$WORK/onchain.so"; then
    DEPLOY=0
  fi
fi

# ---------- 6. Desplegar ----------
step "Desplegando el programa"
if [[ "$DEPLOY" == 0 ]]; then
  info "El programa en la red ya es igual al compilado: no hace falta desplegar."
else
  fund "$ADMIN_PK" "$NEEDED"
  info "Admin: $(sol "$(lamports "$ADMIN_PK")") SOL"
  solana program deploy "$SO" --program-id "$PROGRAM_KEYPAIR" --keypair "$ADMIN" --fee-payer "$ADMIN" \
    --upgrade-authority "$ADMIN" -u "$RPC" 2>&1 | grep -E "Program Id|Signature|Error|error" | sed 's/^/    /'
  solana program show "$PROGRAM_ID" --keypair "$ADMIN" -u "$RPC" >/dev/null 2>&1 || fail "El despliegue no terminó. Vuelve a correr el script."
fi

# ---------- 7. USDC ----------
if [[ "$CLUSTER" == localnet && -z "$USDC_MINT" ]]; then
  step "USDC de prueba (solo localnet)"
  if [[ -f "$WORK/usdc-mint.txt" ]] && spl-token supply "$(cat "$WORK/usdc-mint.txt")" -u "$RPC" >/dev/null 2>&1; then
    USDC_MINT="$(cat "$WORK/usdc-mint.txt")"
  else
    USDC_MINT=$(spl-token create-token --decimals 6 --fee-payer "$ADMIN" --mint-authority "$ADMIN_PK" -u "$RPC" \
      | grep -oE 'Address: +[1-9A-HJ-NP-Za-km-z]+' | awk '{print $2}')
    echo "$USDC_MINT" > "$WORK/usdc-mint.txt"
  fi
  info "Mint: $USDC_MINT (puedes darle USDC a un jugador con: spl-token mint $USDC_MINT 100 --recipient-owner <PUBKEY> --mint-authority keys/admin.json --fee-payer keys/admin.json -u $RPC)"
fi

# ---------- 8. Configurar el juego ----------
step "Configuración del juego"
if CONFIG_OUT=$(ppm config 2>&1); then
  info "Ya estaba configurado:"
  while IFS= read -r line; do info "  $line"; done <<< "$CONFIG_OUT"
  echo "$CONFIG_OUT" | grep -q "Servidor: *$SERVER_PK" \
    || fail "El juego está configurado con otro servidor. Usa esa llave como keys/server.json o cambia la autoridad con update_config."
elif echo "$CONFIG_OUT" | grep -q "no está configurado"; then
  ppm init --keypair "$ADMIN" --mint "$USDC_MINT" --authority "$SERVER_PK" \
    --fee-bps "$FEE_BPS" --timeout "$SETTLE_TIMEOUT" | sed 's/^/    /'
else
  fail "No se pudo leer la configuración: $CONFIG_OUT"
fi

step "SOL para el servidor (crea salas y paga la renta)"
SERVER_WANT=$(awk -v s="$SERVER_SOL" 'BEGIN { printf "%d", s * 1e9 }')
if (( $(lamports "$SERVER_PK") < SERVER_WANT / 2 )); then
  fund "$ADMIN_PK" $(( SERVER_WANT + 100000000 ))
  solana transfer --allow-unfunded-recipient "$SERVER_PK" "$SERVER_SOL" --keypair "$ADMIN" --fee-payer "$ADMIN" \
    -u "$RPC" >/dev/null
fi
info "Servidor: $(sol "$(lamports "$SERVER_PK")") SOL"

# ---------- 9. Backend ----------
step "Configuración del backend"
ENV_FILE="$WORK/backend.env"
cat > "$ENV_FILE" <<EOF
# Generado por scripts/deploy.sh. Uso: set -a; source $ENV_FILE; set +a; dotnet run --project dotnet/PagaParaMorir.Backend
PagaParaMorir__RpcUrl=$RPC
PagaParaMorir__ProgramId=$PROGRAM_ID
PagaParaMorir__ServerKeypairPath=$SERVER
PagaParaMorir__StateFile=$WORK/backend-state.json
PagaParaMorir__Launcher__LogDirectory=$WORK/logs
EOF
info "Guardada en ${ENV_FILE#"$ROOT"/}"

EXPLORER_CLUSTER=$([[ "$CLUSTER" == devnet ]] && echo "devnet" || echo "custom&customUrl=$RPC")
cat <<EOF

$(printf '\033[1;32m')Listo.$(printf '\033[0m') Paga para Morir está desplegado en $CLUSTER.

  Programa:   $PROGRAM_ID
              https://explorer.solana.com/address/$PROGRAM_ID?cluster=$EXPLORER_CLUSTER
  USDC:       $USDC_MINT
  Servidor:   $SERVER_PK
  Comisión:   $(( FEE_BPS / 100 ))%

  Respalda la carpeta keys/: sin admin.json no puedes actualizar el programa
  y sin server.json no se pueden pagar los premios.

Siguientes pasos:
  1. Backend:  set -a; source ${ENV_FILE#"$ROOT"/}; set +a; dotnet run --project dotnet/PagaParaMorir.Backend
  2. Unity:    en PagaParaMorirApp pon programId = $PROGRAM_ID$([[ "$CLUSTER" == localnet ]] && echo ", cluster = LocalNet y customRpc = $RPC")
  3. Jugadores: SOL con el botón del juego y USDC $([[ "$CLUSTER" == devnet ]] && echo "en https://faucet.circle.com (Solana Devnet)" || echo "con el comando spl-token mint de arriba").
EOF

if [[ "$START_BACKEND" == 1 ]]; then
  step "Arrancando el backend (Ctrl+C para detenerlo)"
  set -a
  # shellcheck source=/dev/null
  source "$ENV_FILE"
  set +a
  exec dotnet run --project dotnet/PagaParaMorir.Backend
fi
