# Paga para Morir — cliente Unity (PC)

Por ahora el cliente tiene la parte de dinero. El shooter viene después.

- **Billetera del juego:** crear, restaurar con 12 palabras y abrir con contraseña.
- **Saldo:** USDC y SOL, más un botón de SOL de prueba en devnet.
- **Salas abiertas:** entrada, jugadores, premio si entras y premio con la sala llena.
- **Pagar y entrar**, con confirmación antes de pagar.
- **Mis partidas:**
  - Salir de una sala abierta y recuperar la entrada.
  - Reclamar el reembolso de una partida cancelada.
  - Cancelar una partida si el servidor no reportó ganador a tiempo.

## Por qué una billetera dentro del juego

En PC (Windows) el Solana Unity SDK no puede conectarse directo a Phantom. Solo lo
hace en navegador (WebGL) y en móvil. Por eso cada jugador tiene una billetera propia:

- Se guarda cifrada con su contraseña en el equipo (`PlayerPrefs`).
- Se respalda con 12 palabras.
- El jugador le manda USDC desde Phantom o un exchange.

## Requisitos

- Unity **6 LTS (6000.0)** con *Windows Build Support*.
- **Git** instalado: Unity descarga el SDK de Solana desde GitHub.

## Abrir el proyecto

1. Unity Hub → **Add → Add project from disk** → elige `unity/PagaParaMorir`.
2. Al abrir, Unity descarga `com.solana.unity_sdk` (v1.2.9). Si pregunta por el
   nuevo *Input System*, responde **Yes** (Unity se reinicia).
3. Menú **Paga para Morir → Crear escena principal**. Esto crea
   `Assets/PagaParaMorir/Scenes/Main.unity` y la agrega a Build Settings.
4. Dale **Play**.

Unity genera los archivos `.meta` la primera vez que abre el proyecto. Súbelos
al repo para que todos tengan los mismos GUIDs.

## Configuración

En el objeto `PagaParaMorir` de la escena (componente `PagaParaMorirApp`):

| Campo | Para qué |
|---|---|
| `cluster` | `DevNet` para pruebas. `MainNet` solo con el contrato auditado. |
| `customRpc` | RPC propio (Helius, QuickNode o `http://127.0.0.1:8899`). Vacío = RPC pública. |
| `programId` | Program ID del escrow. Cámbialo si corriste `anchor keys sync`. |
| `refreshSeconds` | Cada cuánto se actualizan las salas. |

## Probar en devnet

1. Despliega el programa y configúralo con el USDC de devnet
   (ver el [README principal](../../README.md#probar-el-cliente-en-devnet)).
2. Crea una sala con la herramienta `ppm`:
   `ppm create --keypair server.json --id 1 --entry 1 --max 4`.
3. En el juego, crea una billetera, copia la dirección y mándale:
   - SOL de prueba con el botón **Pedir 1 SOL de prueba**.
   - USDC de devnet desde <https://faucet.circle.com> (elige Solana Devnet).
4. **Pagar y entrar**. Repite con otra billetera (otro equipo o restaurándola)
   y termina la partida con `ppm start` y `ppm settle`.

## Estructura

```
Assets/PagaParaMorir/
  Escrow/   Cliente C# del contrato, sin dependencias de Unity
            (PDAs, instrucciones, lectura de cuentas y errores en español).
            Se prueba fuera de Unity en dotnet/PagaParaMorir.Escrow.Tests.
  Game/     Scripts de Unity: PagaParaMorirApp (punto de entrada),
            WalletService y pantallas uGUI armadas en código (UI/).
  Game/Editor/  Menú "Paga para Morir" (crear escena, borrar billetera de prueba).
```
