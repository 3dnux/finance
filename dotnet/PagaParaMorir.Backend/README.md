# Backend de Paga para Morir

Servicio en C# (ASP.NET Core) que mantiene el juego andando sin intervención manual.

1. **Abre salas.** Siempre hay una sala abierta por tipo, por ejemplo
   "1 USDC · 8 jugadores". Cuando una se llena, abre otra.
2. **Levanta un servidor por sala.** En cuanto pagan 2 jugadores (`LaunchAtPlayers`),
   lanza el servidor dedicado de Unity con `-ppm-server -match <id> -port <puerto> …`.
   Ese servidor empieza la partida y le paga al ganador.
3. **Les dice a los jugadores dónde jugar.** El botón **Jugar** del lobby pregunta
   `GET /api/matches/{id}/server`.
4. **Red de seguridad para el dinero.** Cancela, para que cada jugador recupere su
   entrada, en estos casos:
   - El servidor se cae en plena partida.
   - El servidor no logra arrancar después de 3 intentos.
   - La partida lleva más de 15 minutos sin resolverse.
   - Una sala con un solo jugador no junta más gente en 30 minutos.
5. **Limpia.** Cierra las salas ya pagadas y las canceladas sin reembolsos pendientes,
   para recuperar la renta en SOL.

El backend firma con la **misma clave del servidor** (`config.authority`) que los
servidores de partida. No puede mandar dinero a nadie que no haya jugado: el
contrato solo permite pagar a un jugador de la sala.

## Correrlo

```bash
cd dotnet/PagaParaMorir.Backend
cp /ruta/a/server.json .        # clave del servidor del juego (no la subas al repo)
dotnet run                      # escucha en http://0.0.0.0:5080
```

La configuración está en `appsettings.json`, en la sección `PagaParaMorir`.
También se puede pasar por variables de entorno, por ejemplo
`PagaParaMorir__RpcUrl=https://…`.

| Opción | Para qué |
|---|---|
| `RpcUrl`, `ProgramId` | Red y programa. Para producción usa un RPC dedicado (Helius, Triton…). |
| `ServerKeypairPath` | Keypair de Solana CLI del servidor (`config.authority`). |
| `Tiers` | Tipos de sala: `Name`, `EntryUsdc`, `MaxPlayers`. |
| `LaunchAtPlayers` | Con cuántos pagos se levanta el servidor (mínimo 2). |
| `OpenMatchMaxMinutes` / `StuckInProgressMinutes` | Cuándo cancelar salas que no avanzan. |
| `Launcher:Mode` | `Process`: lanza el build de Unity en esta máquina. `Manual`: solo muestra el comando en el log. |
| `Launcher:Executable` | Ruta del build del servidor dedicado (modo `Process`). |
| `Launcher:PublicHost` | IP o dominio que se le da a los jugadores. |
| `Launcher:PortRangeStart/End`, `MaxServers` | Puertos UDP para los servidores y cuántos a la vez. |
| `Launcher:LobbySeconds` | Sala de espera de cada servidor antes de empezar con los conectados. |

## API

| Método | Ruta | Respuesta |
|---|---|---|
| GET | `/api/health` | `ok`, último ciclo y servidores corriendo. |
| GET | `/api/tiers` | Tipos de sala configurados. |
| GET | `/api/rooms` | `{ rooms: [...] }`: salas on-chain con su servidor (`host`, `port`, `status`) si lo tienen. |
| GET | `/api/matches/{id}/server` | Dónde conectarse; 404 con `error` (en español) si aún no está listo. |

La API es solo de lectura. Los servidores de partida verifican por su cuenta, con
el boleto firmado, que cada jugador pagó.

## Producción (pendiente)

- Para un solo VPS basta `Launcher:Mode = Process` con los puertos UDP abiertos.
  Para escalar, se implementa `IGameServerLauncher` para Edgegap, GameLift o
  Kubernetes; el resto no cambia.
- Guardar la clave del servidor en un KMS/HSM y no en un archivo.
- Poner HTTPS delante (Caddy o nginx) y rate limiting.
