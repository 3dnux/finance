# Paga para Morir

Shooter multijugador estilo battle royale donde cada jugador deposita USDC para
entrar a la partida y **el ganador se lleva el pozo**. Los pagos se liquidan en
la red **Solana**.

> Estado: fase de diseño. Nada de esto está listo para dinero real.

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
| Netcode | Fish-Net o Photon Fusion (servidor autoritativo) |
| Hosting de servidores | Edgegap / AWS GameLift |
| Contrato de escrow | Rust + Anchor |
| Backend | TypeScript (matchmaking, login con wallet, reporte de resultados) |
| Custodia de la clave del servidor | Multisig (Squads) / HSM |

### Programa escrow (instrucciones)

- `create_match(entry_fee, max_players)` — crea la PDA de la partida y su vault de USDC.
- `join_match()` — el jugador transfiere `entry_fee` USDC al vault.
- `settle_match(winner)` — solo la autoridad del servidor; paga pozo − comisión.
- `cancel_match()` / `refund()` — reembolso si no se llena o expira sin resultado.

## Riesgos a resolver antes de mainnet

- **Legal:** apuestas con dinero real están reguladas según país/estado
  (licencias, geobloqueo, verificación de edad, KYC). Requiere asesoría legal.
- **Anti-cheat:** lógica 100% en servidor + anti-cheat de cliente + replays.
- **Confianza:** la clave que decide al ganador debe estar protegida y las
  partidas deben ser auditables.
- **Auditoría** de seguridad del contrato.

## Roadmap MVP

1. Programa escrow en Anchor + tests en devnet.
2. Prototipo jugable en Unity: arena de 8–16 jugadores, un mapa, 3–4 armas.
3. Backend de matchmaking + login con wallet + liquidación automática.
4. Anti-cheat, auditoría, revisión legal → mainnet.
