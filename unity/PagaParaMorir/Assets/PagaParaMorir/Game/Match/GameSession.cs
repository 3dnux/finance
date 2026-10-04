using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Arranca y termina partidas en red: servidor dedicado, práctica (host) o cliente.
    /// Vive en la escena junto al NetworkManager (lo arma el menú "Paga para Morir").
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Tooltip("NetworkManager de la escena (con UnityTransport y el prefab del jugador).")]
        public NetworkManager networkManager;

        [Tooltip("Prefab con NetworkObject + MatchController.")]
        public GameObject matchPrefab;

        /// <summary>Lógica del servidor (null en un cliente puro).</summary>
        public MatchServer Server { get; private set; }

        public bool InGame => networkManager != null && (networkManager.IsListening || networkManager.ShutdownInProgress);

        /// <summary>La sesión terminó; el texto explica por qué (para mostrarlo en el lobby).</summary>
        public event Action<string> Ended;

        private Arena _arena;
        private Hud _hud;
        private bool _dedicated;
        private bool _ending;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.AddNetworkPrefab(matchPrefab);
            networkManager.OnClientStopped += OnStopped;
            networkManager.OnServerStopped += OnStopped;
        }

        private void Update()
        {
            if (Server == null || !networkManager.IsServer) return;
            Server.Tick(Time.timeAsDouble, Time.deltaTime);
            if (_dedicated && Server.ShouldShutdown)
            {
                Debug.Log("[Servidor] Partida terminada. Cerrando.");
                networkManager.Shutdown();
                Application.Quit();
            }
            _arena?.Zone.SetRadius(MatchController.Instance != null ? MatchController.Instance.ZoneRadius.Value : 0);
        }

        // ---------- Modos ----------

        /// <summary>Servidor dedicado de una partida (sin gráficos). Ver <see cref="ServerOptions"/>.</summary>
        public async Task StartDedicatedServer(ServerOptions options)
        {
            _dedicated = true;
            Application.targetFrameRate = 60;
            await StartServerLike(options, host: false, connectionData: null);
            Debug.Log($"[Servidor] Escuchando en el puerto {options.Port}.");
        }

        /// <summary>Práctica sin dinero en este equipo; otros pueden unirse con la IP.</summary>
        public Task StartPracticeHost(ushort port, byte[] ticket) =>
            StartServerLike(ServerOptions.ForPractice(port), host: true, connectionData: ticket);

        /// <summary>Se conecta al servidor de una partida.</summary>
        public void Join(string address, ushort port, byte[] ticket)
        {
            PrepareScene(showHud: true);
            Transport.SetConnectionData(address, port);
            networkManager.NetworkConfig.ConnectionData = ticket ?? Array.Empty<byte>();
            if (!networkManager.StartClient()) EndSession("No se pudo iniciar la conexión.");
        }

        /// <summary>Salir de la partida y volver al lobby.</summary>
        public void Leave() => EndSession("Saliste de la partida.");

        private async Task StartServerLike(ServerOptions options, bool host, byte[] connectionData)
        {
            PrepareScene(showHud: host);
            Server = new MatchServer(options, networkManager, _arena);
            try
            {
                await Server.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EndSession("No se pudo preparar la partida: " + e.Message);
                if (_dedicated) Application.Quit(1);
                return;
            }

            Transport.SetConnectionData("127.0.0.1", options.Port, "0.0.0.0");
            networkManager.ConnectionApprovalCallback = Server.Approve;
            networkManager.OnClientDisconnectCallback += Server.OnClientDisconnected;
            networkManager.NetworkConfig.ConnectionData = connectionData ?? Array.Empty<byte>();
            var started = host ? networkManager.StartHost() : networkManager.StartServer();
            if (!started)
            {
                EndSession($"No se pudo abrir el puerto {options.Port}.");
                if (_dedicated) Application.Quit(1);
                return;
            }
            Server.OnServerStarted(matchPrefab);
        }

        private UnityTransport Transport => (UnityTransport)networkManager.NetworkConfig.NetworkTransport;

        private void PrepareScene(bool showHud)
        {
            _ending = false;
            _arena = Arena.Build();
            if (showHud) _hud = Hud.Create(this);
        }

        // ---------- Fin ----------

        private void OnStopped(bool _)
        {
            var reason = networkManager.DisconnectReason;
            EndSession(string.IsNullOrEmpty(reason) ? "Se cerró la conexión con la partida." : reason);
        }

        private void EndSession(string reason)
        {
            if (_ending) return;
            _ending = true;
            if (networkManager.IsListening && !networkManager.ShutdownInProgress) networkManager.Shutdown();
            if (Server != null)
            {
                networkManager.ConnectionApprovalCallback = null;
                networkManager.OnClientDisconnectCallback -= Server.OnClientDisconnected;
                Server = null;
            }
            if (_hud != null) _hud.Close();
            _hud = null;
            _arena?.Destroy();
            _arena = null;
            PlayerInputReader.LockCursor(false);
            Ended?.Invoke(reason);
        }

        private void LateUpdate()
        {
            // En los clientes la zona se dibuja con lo que publica el servidor.
            if (Server == null && _arena != null && MatchController.Instance != null)
                _arena.Zone.SetRadius(MatchController.Instance.ZoneRadius.Value);
        }
    }
}
