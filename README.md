# Paga para Morir

Shooter multijugador estilo battle royale donde cada jugador deposita USDC para
entrar a la partida y **el ganador se lleva el pozo**. Los pagos se liquidan en
la red **Solana**.

> Estado: prototipo jugable conectado al escrow (devnet). Nada de esto está listo
> para dinero real (falta auditoría, anti-cheat y revisión legal).

## Decisiones

| Tema | Decisión |
|---|---|
| Plataforma | PC |
| Lenguaje del juego | C# (Unity) |
| Premio | El ganador se lleva todo el pozo |
| Comisión de la casa | 20% (`fee_bps = 2000`, tope en el contrato: 25%) |
| Billetera en PC | Billetera dentro del juego (el SDK no conecta Phantom en Windows); el jugador le manda USDC |

Ejemplo: 10 jugadores × 5 USDC = 50 USDC → **40 USDC al ganador**, 10 USDC a la casa.

## Estructura del repo

| Carpeta | Qué hay |
|---|---|
| `programs/paga-para-morir` | Contrato escrow (Rust + Anchor) y sus pruebas con LiteSVM. |
| `idl/` | IDL del contrato. |
| `unity/PagaParaMorir` | Proyecto de Unity: billetera, lobby, partida en red y servidor dedicado ([README](unity/PagaParaMorir/README.md)). |
| `dotnet/PagaParaMorir.Escrow` | Compila el cliente C# de Unity (`Assets/PagaParaMorir/Escrow`) fuera del editor. |
| `dotnet/PagaParaMorir.Escrow.Tests` | Pruebas del cliente C#, incluidas partidas reales contra `solana-test-validator`. |
| `dotnet/PagaParaMorir.Rules.Tests` | Pruebas de las reglas del juego: armas, zona y quién gana. |
| `dotnet/PagaParaMorir.DevTool` | `ppm`: crea y maneja salas desde la terminal mientras no exista el backend. |

## Concepto

1. El jugador conecta su wallet (Phantom, Solflare, etc.).
2. Elige una sala por monto de entrada (ej. $1, $5, $20 USDC).
3. Su USDC se deposita en un **escrow on-chain** (no en una wallet del equipo).
4. Se juega la partida en un servidor autoritativo.
5. Al terminar, el servidor reporta el ganador y el contrato le paga el pozo
   menos una comisión de la casa.
6. Si la partida no se llena o falla, cada jugador recupera su depósito.

## Arquitectura

```
[Cliente Unity] ──firma depósito──▶ [Programa Escrow en Solana (Anchor/Rust)]
      │                                        ▲
      │ juega                                  │ settle(ganador) firmado
      ▼                                        │
[Servidor dedicado autoritativo] ──resultado──▶ [Backend: matchmaking + oráculo]
```

| Componente | Tecnología propuesta |
|---|---|
| Motor gráfico / cliente | Unity + Solana.Unity-SDK |
| Netcode | Netcode for GameObjects (oficial de Unity), servidor autoritativo |
| Hosting de servidores | Edgegap / AWS GameLift |
| Contrato de escrow | Rust + Anchor |
| Backend | TypeScript (matchmaking, login con wallet, reporte de resultados) |
| Custodia de la clave del servidor | Multisig (Squads) / HSM |

### Programa escrow (`programs/paga-para-morir`)

Program ID: `2NB9Xwtj7BRFkZEvDWWBhuqTWK18ASTogRt1SCGKZGhg`

| Instrucción | Quién firma | Qué hace |
|---|---|---|
| `initialize_config(authority, fee_bps, settle_timeout_secs)` | Upgrade authority del programa | Fija el servidor, la tesorería, el mint de USDC y la comisión. |
| `update_config(update)` | Admin | Cambia servidor, admin, comisión, timeout, pausa o tesorería. |
| `create_match(match_id, entry_fee, max_players)` | Servidor | Abre una sala (2–100 jugadores) y su vault de USDC. |
| `join_match()` | Jugador | Deposita la entrada en el vault. |
| `leave_match()` | Jugador | Sale de una sala abierta y recupera su entrada. |
| `start_match()` | Servidor | Bloquea la sala (mínimo 2 jugadores). |
| `settle_match(winner)` | Servidor | Paga 80% al ganador y 20% a la tesorería. El ganador debe ser un jugador de la partida. |
| `cancel_match()` | Servidor, o cualquiera si pasó el timeout | Cancela; los jugadores reclaman su entrada. |
| `claim_refund()` | Jugador | Recupera su entrada de una partida cancelada. |
| `close_match()` | Servidor | Cierra la partida y el vault y recupera la renta. Lo sobrante va a la tesorería. |

Garantías del contrato:
- El dinero vive en un vault controlado por el programa, no en una wallet del equipo.
- El servidor solo puede pagar a alguien que pagó su entrada en esa partida.
- Si el servidor desaparece, pasado `settle_timeout_secs` cualquiera puede
  cancelar y cada jugador recupera su dinero.
- El IDL para generar el cliente está en `idl/paga_para_morir.json`.

## Desarrollo

Requisitos: Rust (se fija con `rust-toolchain.toml`), Solana CLI y Anchor CLI 1.2.

```bash
# Instalar Solana CLI
sh -c "$(curl -sSfL https://release.anza.xyz/stable/install)"
# Instalar Anchor CLI
cargo install anchor-cli --version 1.2.0 --locked

# Compilar el programa (genera target/deploy/paga_para_morir.so y el IDL)
anchor build

# Correr las pruebas (unitarias + integración con LiteSVM)
cargo test -p paga-para-morir
```

### Cliente C# (Unity)

Requisitos: .NET SDK 8. Las pruebas de integración levantan `solana-test-validator`
con el programa compilado (`anchor build` antes); si no está instalado, se saltan.

```bash
cd dotnet
dotnet test
```

### Herramienta `ppm` (servidor manual)

```bash
cd dotnet
dotnet run --project PagaParaMorir.DevTool -- list --url devnet
dotnet run --project PagaParaMorir.DevTool -- create --keypair server.json --id 1 --entry 5 --max 10
dotnet run --project PagaParaMorir.DevTool -- start  --keypair server.json --id 1
dotnet run --project PagaParaMorir.DevTool -- settle --keypair server.json --id 1 --winner <PUBKEY>
```

Comandos: `config`, `init`, `list`, `create`, `start`, `settle`, `cancel`, `close`
(`--help` para ver las opciones).

### Desplegar en devnet

```bash
anchor keys sync              # genera tu propio program ID y lo escribe en el código
anchor build
solana config set --url devnet
solana airdrop 2
anchor deploy --provider.cluster devnet
```

Después configura el juego con la misma wallet que desplegó (es la upgrade authority):

```bash
cd dotnet
# Mint de USDC de Circle en devnet (verifícalo en https://faucet.circle.com)
dotnet run --project PagaParaMorir.DevTool -- init \
  --mint 4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU \
  --authority <PUBKEY_DEL_SERVIDOR> --fee-bps 2000
```

La tesorería queda en la cuenta de USDC de quien firma.

### Probar el cliente en devnet

1. Despliega y configura el programa (arriba).
2. Crea salas con `ppm create` firmando con la clave del servidor.
3. Abre el proyecto de Unity, crea una billetera y mándale SOL de prueba (botón en
   el juego) y USDC de devnet desde <https://faucet.circle.com>.
4. Entra a una sala desde el juego y termínala con `ppm start` / `ppm settle`.

## Riesgos a resolver antes de mainnet

- **Legal:** apuestas con dinero real están reguladas según país/estado
  (licencias, geobloqueo, verificación de edad, KYC). Requiere asesoría legal.
- **Anti-cheat:** lógica 100% en servidor + anti-cheat de cliente + replays.
- **Confianza:** la clave que decide al ganador debe estar protegida y las
  partidas deben ser auditables.
- **Auditoría** de seguridad del contrato.

## Roadmap MVP

1. ~~Programa escrow en Anchor + pruebas.~~ ✅ Falta desplegar en devnet.
2. Cliente Unity: ~~billetera, lobby y pago de entradas~~ ✅ · ~~prototipo jugable
   (arena, 4 armas, zona, servidor dedicado que paga al ganador)~~ ✅. Falta
   predicción del cliente, arte y sonido.
3. Backend: crear salas y levantar un servidor dedicado por sala (la liquidación
   automática ya la hace el servidor de la partida).
4. Anti-cheat, auditoría, revisión legal → mainnet.
