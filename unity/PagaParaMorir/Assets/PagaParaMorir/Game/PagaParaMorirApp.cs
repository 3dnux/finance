using PagaParaMorir.Escrow;
using PagaParaMorir.Game.UI;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;

namespace PagaParaMorir.Game
{
    /// <summary>
    /// Punto de entrada del juego: conecta con Solana y muestra la billetera y el lobby.
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

        public WalletService Wallet { get; } = new WalletService();
        public EscrowClient Escrow { get; private set; }
        public StatusBar Status { get; private set; }
        public Transform CanvasRoot { get; private set; }
        public bool IsDevNetwork => cluster != RpcCluster.MainNet;

        private RectTransform _content;
        private LobbyScreen _lobby;
        private float _nextRefresh;

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
            CreateWeb3();
            BuildLayout();
            ShowWallet();
        }

        private void Update()
        {
            if (_lobby == null || Time.unscaledTime < _nextRefresh) return;
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
