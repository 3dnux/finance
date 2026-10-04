using System;
using PagaParaMorir.Escrow;
using PagaParaMorir.Game.Match;
using PagaParaMorir.Game.UI;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;

namespace PagaParaMorir.Game
{
    /// <summary>
    /// Punto de entrada del juego: conecta con Solana y muestra la billetera y el lobby.
    /// Con <c>-ppm-server</c> en la línea de comandos arranca como servidor dedicado de una partida.
    /// Si la escena no tiene uno, se crea solo al dar Play.
    /// </summary>
    public class PagaParaMorirApp : MonoBehaviour
    {
        [Header("Red de Solana")]
        [Tooltip("DevNet para pruebas. MainNet solo cuando el contrato esté auditado.")]
        public RpcCluster cluster = RpcCluster.DevNet;

        [Tooltip("URL de RPC propia (por ejemplo Helius o http://127.0.0.1:8899). Vacío = la pública de la red.")]
        public string customRpc = "";

        [Tooltip("Program ID del escrow desplegado (cámbialo después de `anchor keys sync`).")]
        public string programId = EscrowProgram.DefaultProgramId;

        [Header("Lobby")]
        [Tooltip("Cada cuántos segundos se actualizan las salas.")]
        public float refreshSeconds = 5f;

        [Header("Servidor de partidas")]
        [Tooltip("Dirección del servidor de partidas (más adelante la dará el backend por sala).")]
        public string gameServerAddress = "127.0.0.1";
        public ushort gameServerPort = 7777;
        [Tooltip("Puerto para las prácticas sin dinero (crear o unirse).")]
        public ushort practicePort = 7778;

        public WalletService Wallet { get; } = new WalletService();
        public EscrowClient Escrow { get; private set; }
        public StatusBar Status { get; private set; }
        public Transform CanvasRoot { get; private set; }
        public bool IsDevNetwork => cluster != RpcCluster.MainNet;

        private RectTransform _content;
        private GameObject _canvas;
        private LobbyScreen _lobby;
        private float _nextRefresh;
        private bool _dedicatedServer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfMissing()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = FindAnyObjectByType<PagaParaMorirApp>();
#else
            var existing = FindObjectOfType<PagaParaMorirApp>();
#endif
            if (existing == null) new GameObject("PagaParaMorir").AddComponent<PagaParaMorirApp>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            ServerOptions server;
            try
            {
                server = ServerOptions.FromCommandLine(Environment.GetCommandLineArgs());
            }
            catch (ArgumentException e)
            {
                Debug.LogError(e.Message);
                Application.Quit(1);
                return;
            }
            if (server.IsServer)
            {
                _dedicatedServer = true;
                StartDedicatedServer(server);
                return;
            }
            CreateWeb3();
            BuildLayout();
            ShowWallet();
        }

        private void Start()
        {
            if (!_dedicatedServer && GameSession.Instance != null) GameSession.Instance.Ended += OnGameEnded;
        }

        private async void StartDedicatedServer(ServerOptions options)
        {
            // GameSession también despierta en Awake: esperamos un frame para que exista.
            await System.Threading.Tasks.Task.Yield();
            if (GameSession.Instance == null)
            {
                Debug.LogError("La escena no tiene GameSession. Usa el menú Paga para Morir → Crear escena principal.");
                Application.Quit(1);
                return;
            }
            await GameSession.Instance.StartDedicatedServer(options);
        }

        private void Update()
        {
            if (_lobby == null || Time.unscaledTime < _nextRefresh || InGame) return;
            _nextRefresh = Time.unscaledTime + refreshSeconds;
            _lobby.RefreshInBackground();
        }

        /// <summary>Después de abrir la billetera: conecta el cliente del escrow y abre el lobby.</summary>
        public void OnLoggedIn()
        {
            Escrow = new EscrowClient(Web3.Rpc, new EscrowProgram(new PublicKey(programId)));
            Ui.Clear(_content);
            _lobby = new LobbyScreen(this, _content);
            _nextRefresh = 0;
        }

        public void ShowWallet()
        {
            _lobby = null;
            Escrow = null;
            Ui.Clear(_content);
            new WalletScreen(this, _content);
        }

        public bool InGame => GameSession.Instance != null && GameSession.Instance.InGame;

        /// <summary>Entrar al servidor de una partida pagada.</summary>
        public void PlayMatch(ulong matchId) =>
            EnterGame(matchId, (session, ticket) => session.Join(gameServerAddress, gameServerPort, ticket));

        /// <summary>Práctica sin dinero en este equipo (otros pueden unirse con tu IP).</summary>
        public void StartPractice() =>
            EnterGame(0, (session, ticket) => session.StartPracticeHost(practicePort, ticket));

        public void JoinPractice() =>
            EnterGame(0, (session, ticket) => session.Join(gameServerAddress, practicePort, ticket));

        private async void EnterGame(ulong matchId, Action<GameSession, byte[]> start)
        {
            var session = GameSession.Instance;
            if (session == null)
            {
                Status.Error("Falta la escena de red: usa el menú Paga para Morir → Crear escena principal.");
                return;
            }
            if (session.InGame) return;
            try
            {
                Status.Info("Firmando tu boleto de entrada…");
                var ticket = await JoinTicket.CreateAsync(matchId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Wallet.PublicKey, Wallet.SignMessage);
                Status.Info("Conectando con la partida…");
                _canvas.SetActive(false);
                start(session, ticket.Encode());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _canvas.SetActive(true);
                Status.Error("No se pudo entrar: " + e.Message);
            }
        }

        private void OnGameEnded(string reason)
        {
            _canvas.SetActive(true);
            Status.Info(reason);
            _nextRefresh = 0;
        }

        public void Logout()
        {
            Wallet.Logout();
            Status.Info("Sesión cerrada.");
            ShowWallet();
        }

        private void CreateWeb3()
        {
            if (Web3.Instance != null) return;
            var go = new GameObject("Web3");
            go.transform.SetParent(transform, false);
            var web3 = go.AddComponent<Web3>();
            web3.rpcCluster = cluster;
            web3.customRpc = customRpc;
            web3.autoConnectOnStartup = false;
        }

        private void BuildLayout()
        {
            Ui.EnsureEventSystem();
            var canvas = Ui.CreateCanvas(transform, "Canvas");
            CanvasRoot = canvas.transform;
            _canvas = canvas.gameObject;

            var background = Ui.Panel(canvas.transform, Theme.Background, "Background");
            Ui.Stretch(background.rectTransform);

            var page = Ui.VStack(background.transform, 24, 48, TextAnchor.UpperLeft, "Page");
            Ui.Stretch(page);

            var header = Ui.HStack(page, 16, 0, TextAnchor.MiddleLeft, "Header");
            Ui.Label(header, "PAGA PARA MORIR", 56, Theme.Accent, FontStyle.Bold);
            Ui.Spacer(header);
            Ui.Label(header, "Red: " + NetworkName(), 24, IsDevNetwork ? Theme.Warning : Theme.Muted,
                FontStyle.Bold, TextAnchor.MiddleRight);

            _content = Ui.Rect(page, "Content");
            Ui.Size(_content, flexibleHeight: 1, flexibleWidth: 1);

            Status = new StatusBar(page);
        }

        private string NetworkName()
        {
            if (!string.IsNullOrEmpty(customRpc)) return cluster + " (RPC propio)";
            return cluster.ToString();
        }
    }
}
