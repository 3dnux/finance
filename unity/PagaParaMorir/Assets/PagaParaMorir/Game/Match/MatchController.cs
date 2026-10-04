using System;
using PagaParaMorir.Rules;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Estado de la partida que ven todos (fase, reloj, zona, ganador) y efectos para todos
    /// (disparos, bajas). Lo escribe solo el servidor.
    /// </summary>
    public class MatchController : NetworkBehaviour
    {
        public static MatchController Instance { get; private set; }

        /// <summary>Texto para la lista de bajas.</summary>
        public static event Action<string> KillFeed;

        public readonly NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(
            MatchPhase.Waiting, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> SecondsLeft = new NetworkVariable<float>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> ZoneRadius = new NetworkVariable<float>(
            60, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> AliveCount = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> PlayerCount = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> Paid = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<FixedString64Bytes> WinnerId = new NetworkVariable<FixedString64Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Mensaje del servidor: espera, premio pagado, motivo de cancelación…</summary>
        public readonly NetworkVariable<FixedString512Bytes> Banner = new NetworkVariable<FixedString512Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn() => Instance = this;

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        [Rpc(SendTo.Everyone, Delivery = RpcDelivery.Unreliable)]
        public void ShotRpc(Vector3 from, Vector3 to) => Visuals.Tracer(from, to);

        [Rpc(SendTo.Everyone)]
        public void KillFeedRpc(FixedString128Bytes text) => KillFeed?.Invoke(text.ToString());

        // ---------- Escritura (solo servidor) ----------

        public void ServerSet<T>(NetworkVariable<T> variable, T value)
        {
            if (!Equals(variable.Value, value)) variable.Value = value;
        }

        public void ServerSetBanner(string text) =>
            ServerSet(Banner, new FixedString512Bytes(Truncate(text, 120)));

        private static string Truncate(string text, int max) =>
            text == null ? "" : text.Length <= max ? text : text.Substring(0, max);
    }
}
