# Paga para Morir — cliente y servidor Unity (PC)

El mismo proyecto es el juego del jugador (lobby + partida) y el servidor dedicado
de cada partida.

## Prototipo jugable

- **Arena** de 140×140 m con coberturas, generada con semilla fija (igual en todos).
- **Shooter en primera persona:** WASD, ratón, saltar, 4 armas:

  | Arma | Daño | Cadencia | Cargador | Alcance |
  |---|---|---|---|---|
  | Pistola | 20 | 4/s | 12 | 60 m |
  | Rifle (automático) | 14 | 10/s | 30 | 80 m |
  | Escopeta | 10 × 8 perdigones | ~1/s | 6 | 20 m |
  | Francotirador | 85 | 1 cada 1.5 s | 4 | 200 m |

- **Zona que se cierra** en 4 fases (~3 minutos). Fuera de ella pierdes vida cada segundo.
- **Gana el último en pie.** Desempates:
  - Si los últimos caen a la vez: más bajas, luego más daño causado, luego quien entró primero.
  - Si se acaba el tiempo (5 min): el vivo con más vida.
  - Desconectarse en juego cuenta como eliminación.
  - Si todos se desconectan a la vez, la partida se cancela y hay reembolso.
- **Servidor autoritativo** (Netcode for GameObjects): el cliente solo manda su input;
  el servidor mueve a los jugadores, valida los disparos y aplica el daño.

### Cómo se conecta con el dinero

1. En el lobby, en **Mis partidas**, el botón **Jugar** firma un *boleto de entrada*
   con tu billetera. Así el servidor sabe que controlas la llave que pagó.
2. El servidor verifica la firma y que esa llave esté en la lista de la sala on-chain.
3. La sala de espera dura hasta 2 minutos, o hasta que se llene con todos conectados.
   Al empezar, el servidor llama a `start_match`: desde ahí nadie puede salirse
   con reembolso.
   - Quien pagó pero no se conectó a tiempo pierde su entrada.
   - Si no hay al menos 2 conectados, el servidor cancela (`cancel_match`) y cada
     uno reclama su entrada.
4. Al terminar, el servidor llama a `settle_match` con el ganador: 80% al ganador
   y 20% a la casa.
5. Si el servidor no logra pagar, después del tiempo límite cada jugador puede
   cancelar desde el lobby y recuperar su entrada.

Las reglas (armas, zona, árbitro) están en `Assets/PagaParaMorir/Rules`. No dependen
de Unity y tienen pruebas en `dotnet/PagaParaMorir.Rules.Tests`.

## Billetera y lobby

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
2. Al abrir, Unity descarga `com.solana.unity_sdk` (v1.2.9) y Netcode for
   GameObjects. Si pregunta por el nuevo *Input System*, responde **Yes**
   (Unity se reinicia).
3. Menú **Paga para Morir → Crear escena principal**. Esto crea:
   - Los prefabs de red `NetworkPlayer` y `NetworkMatch` en `Assets/PagaParaMorir/Prefabs`.
   - La escena `Assets/PagaParaMorir/Scenes/Main.unity`, con el NetworkManager, y
     la agrega a Build Settings.
4. Dale **Play**.

## Probar la partida sin dinero (práctica)

1. Abre tu billetera y, en el lobby, en **Práctica sin dinero**, pulsa **Crear**.
   Juegas solo; la zona y las armas funcionan igual.
2. Para probar con más jugadores:
   - Haz un build de PC y ábrelo varias veces, o usa **Window → Multiplayer Play Mode**.
   - Uno pulsa **Crear** y los demás **Unirse**.
   - `gameServerAddress` debe apuntar al equipo que creó la práctica. Puerto 7778
     por defecto.

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
| `gameServerAddress` / `gameServerPort` | Servidor de partidas al que se conecta **Jugar** (y **Unirse** a una práctica). |
| `practicePort` | Puerto de las prácticas sin dinero (7778). |

## Servidor dedicado de una partida con dinero

Haz un build (Windows/Linux, o *Dedicated Server*) y lánzalo así por cada sala:

```bash
PagaParaMorir -batchmode -nographics -ppm-server \
  -match 5 -port 7777 \
  -rpc https://api.devnet.solana.com \
  -keypair server.json            # la clave del servidor (config.authority)
```

Por ahora el cliente se conecta a `gameServerAddress:gameServerPort` (campos de
`PagaParaMorirApp`). Más adelante el backend levantará un servidor por sala y le
dirá a cada jugador dónde conectarse.

## Probar en devnet

1. Despliega el programa y configúralo con el USDC de devnet
   (ver el [README principal](../../README.md#probar-el-cliente-en-devnet)).
2. Crea una sala con la herramienta `ppm`:
   `ppm create --keypair server.json --id 1 --entry 1 --max 4`.
3. En el juego, crea una billetera, copia la dirección y mándale:
   - SOL de prueba con el botón **Pedir 1 SOL de prueba**.
   - USDC de devnet desde <https://faucet.circle.com> (elige Solana Devnet).
4. **Pagar y entrar**. Repite con otra billetera (otro equipo o restaurándola).
5. Lanza el servidor dedicado de esa sala (arriba) y pulsa **Jugar** en ambos.
   El servidor empieza la partida, y al final le paga al ganador solo.
   Sin servidor, puedes terminarla a mano con `ppm start` y `ppm settle`.

## Estructura

```
Assets/PagaParaMorir/
  Escrow/   Cliente C# del contrato y boleto de entrada, sin dependencias de Unity.
            Se prueba en dotnet/PagaParaMorir.Escrow.Tests.
  Rules/    Reglas del juego (armas, zona, árbitro y ganador), sin dependencias de Unity.
            Se prueban en dotnet/PagaParaMorir.Rules.Tests.
  Game/     PagaParaMorirApp (punto de entrada), WalletService y pantallas (UI/).
  Game/Match/  La partida en red: GameSession (servidor/práctica/cliente), MatchServer
            (árbitro + Solana), NetworkPlayer, MatchController, Arena y Hud.
  Game/Editor/  Menú "Paga para Morir" (crear escena y prefabs, borrar billetera de prueba).
```

## Lo que falta en el prototipo

- **Predicción del lado del cliente:** hoy tu movimiento llega con la latencia del
  servidor. En LAN no se nota; por internet habrá que predecir y reconciliar.
- **Compensación de lag** para los disparos y **anti-cheat** (el servidor confía en
  hacia dónde apuntas).
- **Arte y sonido:** todo son primitivas de colores.
- **Reconexión:** si te desconectas en juego, quedas eliminado.
