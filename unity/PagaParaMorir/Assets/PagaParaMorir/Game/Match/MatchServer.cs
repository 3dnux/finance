using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PagaParaMorir.Escrow;
using PagaParaMorir.Rules;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Lógica del servidor de una partida: quién entra, cuándo empieza, el daño y el pago.
    /// En una partida con dinero también habla con el contrato: start_match al empezar,
    /// settle_match con el ganador y cancel_match si no se puede jugar.
    /// </summary>
    public sealed class MatchServer
    {
        /// <summary>Antigüedad máxima del boleto de entrada.</summary>
        private const long TicketMaxAgeSeconds = 120;
        private const double ChainPollSeconds = 5;
        private const double ResultsSeconds = 25;

        public MatchReferee Referee { get; private set; }
        public bool Paid => !_options.Practice;
        public bool CanShoot => Referee?.Phase == MatchPhase.Playing;
        /// <summary>El servidor dedicado debe cerrarse (la partida terminó y se mostraron los resultados).</summary>
        public bool ShouldShutdown => !double.IsNaN(_shutdownAt) && Time.timeAsDouble >= _shutdownAt;

        private readonly ServerOptions _options;
        private readonly NetworkManager _network;
        private readonly Arena _arena;
        private readonly Dictionary<ulong, string> _idByClient = new Dictionary<ulong, string>();

        private EscrowClient _escrow;
        private Account _serverKey;
        private ConfigAccount _config;
        private MatchController _controller;
        private MatchPhase _lastPhase = MatchPhase.Waiting;
        private bool _chainBusy;
        private bool _starting;
        private double _nextChainPoll;
        private double _shutdownAt = double.NaN;
        private int _nextSpawn;
        private int _guests;

        public MatchServer(ServerOptions options, NetworkManager network, Arena arena)
        {
            _options = options;
            _network = network;
            _arena = arena;
        }

        /// <summary>Prepara la partida. En modo con dinero valida la clave del servidor y la sala on-chain.</summary>
        public async Task InitializeAsync()
        {
            var now = Time.timeAsDouble;
            if (!Paid)
            {
                Referee = new MatchReferee(RefereeSettings.Practice(), now);
                return;
            }

            _serverKey = Keypairs.FromSolanaCliJson(File.ReadAllText(_options.KeypairPath));
            _escrow = new EscrowClient(ClientFactory.GetClient(_options.RpcUrl),
                new EscrowProgram(new PublicKey(_options.ProgramId)));
            _config = await _escrow.GetConfigAsync();
            if (!_config.Authority.Equals(_serverKey.PublicKey))
                throw new InvalidOperationException(
                    $"La clave {_serverKey.PublicKey} no es el servidor del juego ({_config.Authority}).");

            var match = await _escrow.GetMatchAsync(_options.MatchId)
                        ?? throw new InvalidOperationException($"No existe la sala #{_options.MatchId}.");
            if (match.State != Escrow.MatchState.Open)
                throw new InvalidOperationException($"La sala #{_options.MatchId} no está abierta ({match.State}).");

            var settings = new RefereeSettings { MaxPlayers = match.MaxPlayers };
            Referee = new MatchReferee(settings, now, match.Players.Select(p => p.Key));
            Debug.Log($"[Servidor] Sala #{match.MatchId}: entrada {Usdc.Format(match.EntryFee)}, " +
                      $"{match.Players.Count}/{match.MaxPlayers} jugadores pagaron.");
        }

        public void OnServerStarted(GameObject matchPrefab)
        {
            var go = UnityEngine.Object.Instantiate(matchPrefab);
            _controller = go.GetComponent<MatchController>();
            go.GetComponent<NetworkObject>().Spawn();
            _controller.ServerSet(_controller.Paid, Paid);
        }

        public string IdOf(ulong clientId) => _idByClient.TryGetValue(clientId, out var id) ? id : "";

        // ---------- Conexiones ----------

        public void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var ticket = JoinTicket.Decode(request.Payload);
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string id;
            if (Paid)
            {
                if (ticket == null) { Reject(response, "Falta el boleto de entrada."); return; }
                if (!ticket.Verify(_options.MatchId, nowUnix, TicketMaxAgeSeconds, out var error)) { Reject(response, error); return; }
                id = ticket.Player.Key;
                if (!Referee.IsExpected(id))
                {
                    // Puede haber pagado hace segundos: revisamos la cadena antes de rechazar.
                    response.Pending = true;
                    RefreshPlayersThenDecide(request.ClientNetworkId, id, response);
                    return;
                }
            }
            else
            {
                // Práctica: si trae un boleto válido usamos su billetera como nombre.
                id = ticket != null && ticket.Verify(0, nowUnix, TicketMaxAgeSeconds, out _)
                    ? ticket.Player.Key
                    : $"Invitado-{++_guests}";
            }
            Decide(request.ClientNetworkId, id, response);
        }

        private async void RefreshPlayersThenDecide(ulong clientId, string id, NetworkManager.ConnectionApprovalResponse response)
        {
            try
            {
                await PollPlayersAsync();
                Decide(clientId, id, response);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Reject(response, "El servidor no pudo verificar tu pago. Intenta de nuevo.");
            }
            finally
            {
                response.Pending = false;
            }
        }

        private void Decide(ulong clientId, string id, NetworkManager.ConnectionApprovalResponse response)
        {
            if (_starting)
            {
                Reject(response, "La partida ya está empezando.");
                return;
            }
            if (!Referee.CanJoin(id, out var reason))
            {
                Reject(response, reason);
                return;
            }
            Referee.Join(id);
            _idByClient[clientId] = id;
            var spawn = _arena.SpawnPoints[_nextSpawn++ % _arena.SpawnPoints.Count];
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = spawn;
            response.Rotation = Quaternion.Euler(0, Arena.YawTowardsCenter(spawn), 0);
            Debug.Log($"[Servidor] Entró {id} (cliente {clientId}).");
        }

        private static void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = reason;
        }

        public void OnClientDisconnected(ulong clientId)
        {
            if (!_idByClient.TryGetValue(clientId, out var id)) return;
            _idByClient.Remove(clientId);
            var elimination = Referee.Leave(id, Time.timeAsDouble);
            if (elimination != null) Announce(elimination);
            Debug.Log($"[Servidor] Salió {id}.");
        }

        // ---------- Partida ----------

        public void Tick(double now, float dt)
        {
            if (Referee == null || _controller == null) return;

            if (Referee.Phase == MatchPhase.Waiting && !_starting)
            {
                if (Paid && now >= _nextChainPoll && !_chainBusy) PollPlayersInBackground(now);
                switch (Referee.EvaluateLobby(now))
                {
                    case LobbyDecision.Start: BeginMatch(); break;
                    case LobbyDecision.Cancel: CancelMatch("No llegaron suficientes jugadores."); break;
                }
            }

            Referee.Update(now);
            if (Referee.Phase == MatchPhase.Playing)
            {
                var radius = Referee.ZoneRadius(now);
                var outside = NetworkPlayer.All
                    .Where(p => p.Alive.Value && new Vector2(p.transform.position.x, p.transform.position.z).magnitude > radius)
                    .Select(p => p.Id)
                    .ToList();
                foreach (var e in Referee.ApplyZoneDamage(outside, dt, now)) Announce(e);
            }

            var frozen = Referee.Phase == MatchPhase.Countdown || Referee.Phase >= MatchPhase.Finished;
            foreach (var p in NetworkPlayer.All) p.ServerSync(Referee.Get(p.Id), frozen);

            if (Referee.Phase != _lastPhase) OnPhaseChanged(_lastPhase, Referee.Phase);
            _lastPhase = Referee.Phase;
            PublishState(now);
        }

        public void OnHit(NetworkPlayer attacker, NetworkPlayer victim, int damage)
        {
            var elimination = Referee.ApplyDamage(attacker.Id, victim.Id, damage, Time.timeAsDouble);
            victim.ServerSync(Referee.Get(victim.Id), victim.Frozen.Value);
            attacker.HitConfirmedRpc(elimination != null);
            if (elimination != null) Announce(elimination);
        }

        private async void BeginMatch()
        {
            _starting = true;
            try
            {
                if (Paid)
                {
                    _controller.ServerSetBanner("Bloqueando la sala en Solana…");
                    await SendWithRetry(_escrow.Program.StartMatch(_serverKey.PublicKey, _options.MatchId), "start_match",
                        Escrow.MatchState.InProgress);
                    // Quien se salió (leave_match) después de conectarse ya no juega.
                    var match = await _escrow.GetMatchAsync(_options.MatchId);
                    Referee.SetExpectedPlayers(match.Players.Select(p => p.Key));
                    foreach (var pair in _idByClient.ToList().Where(pair => !Referee.IsExpected(pair.Value)))
                        _network.DisconnectClient(pair.Key, "Te saliste de la sala; tu entrada ya fue devuelta.");
                }
                Referee.BeginCountdown(Time.timeAsDouble);
                var i = 0;
                foreach (var p in NetworkPlayer.All.Where(p => Referee.Get(p.Id) != null))
                {
                    var spawn = _arena.SpawnPoints[i++ * (_arena.SpawnPoints.Count / Math.Max(1, Referee.StartedWith)) % _arena.SpawnPoints.Count];
                    p.ServerTeleport(spawn, Arena.YawTowardsCenter(spawn));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                CancelMatch("No se pudo iniciar la partida en Solana.");
            }
        }

        private void OnPhaseChanged(MatchPhase from, MatchPhase to)
        {
            Debug.Log($"[Servidor] Fase {from} → {to}");
            if (to == MatchPhase.Finished) PayWinner(Referee.Result);
            else if (to == MatchPhase.Cancelled) CancelMatch(Referee.CancelReason ?? "Partida cancelada.");
        }

        private async void PayWinner(MatchResult result)
        {
            _controller.ServerSet(_controller.WinnerId, new FixedString64Bytes(result.WinnerId));
            if (!Paid)
            {
                _controller.ServerSetBanner($"Ganó {Visuals.ShortId(result.WinnerId)} (práctica, sin premio).");
                return;
            }

            _controller.ServerSetBanner("Pagando al ganador en Solana…");
            try
            {
                var match = await _escrow.GetMatchAsync(_options.MatchId);
                var prize = _config.PrizeFor(match.Pot);
                await SendWithRetry(_escrow.Program.SettleMatch(_serverKey.PublicKey, _options.MatchId,
                    new PublicKey(result.WinnerId), _config.Treasury, _config.UsdcMint), "settle_match",
                    Escrow.MatchState.Settled);
                _controller.ServerSetBanner($"Premio pagado: {Usdc.Format(prize)} a {Visuals.ShortId(result.WinnerId)}.");
                try
                {
                    await _escrow.SendAsync(_serverKey.PublicKey, Sign,
                        _escrow.Program.CloseMatch(_serverKey.PublicKey, _options.MatchId, _config.Treasury, _config.UsdcMint));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("close_match falló (no afecta el premio): " + e.Message);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _controller.ServerSetBanner("No se pudo pagar ahora. Si nadie paga a tiempo, cada jugador " +
                                            "podrá cancelar y recuperar su entrada desde el lobby.");
            }
            finally
            {
                _shutdownAt = Time.timeAsDouble + ResultsSeconds;
            }
        }

        private async void CancelMatch(string reason)
        {
            if (Referee.Phase != MatchPhase.Cancelled) Referee.Cancel(Time.timeAsDouble, reason);
            _lastPhase = MatchPhase.Cancelled;
            _controller.ServerSetBanner($"Partida cancelada: {reason}" + (Paid ? " Reclama tu entrada en el lobby." : ""));
            _shutdownAt = Time.timeAsDouble + ResultsSeconds;
            if (!Paid) return;
            try
            {
                await SendWithRetry(_escrow.Program.CancelMatch(_serverKey.PublicKey, _options.MatchId), "cancel_match",
                    Escrow.MatchState.Cancelled);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Announce(Elimination e)
        {
            string text;
            switch (e.Cause)
            {
                case EliminationCause.Weapon: text = $"{Visuals.ShortId(e.KillerId)} eliminó a {Visuals.ShortId(e.VictimId)}"; break;
                case EliminationCause.Zone: text = $"{Visuals.ShortId(e.VictimId)} murió en la zona"; break;
                default: text = $"{Visuals.ShortId(e.VictimId)} abandonó la partida"; break;
            }
            _controller.KillFeedRpc(new FixedString128Bytes(text));
        }

        private void PublishState(double now)
        {
            var c = _controller;
            c.ServerSet(c.Phase, Referee.Phase);
            c.ServerSet(c.SecondsLeft, (float)Math.Ceiling(Referee.SecondsLeft(now)));
            c.ServerSet(c.ZoneRadius, Mathf.Round(Referee.ZoneRadius(now) * 10f) / 10f);
            c.ServerSet(c.AliveCount, Referee.Phase == MatchPhase.Waiting ? Referee.ConnectedCount : Referee.AliveCount);
            c.ServerSet(c.PlayerCount, Referee.Phase == MatchPhase.Waiting ? Referee.Settings.MaxPlayers : Referee.StartedWith);
            if (Referee.Phase == MatchPhase.Waiting && !_starting)
                c.ServerSetBanner($"Esperando jugadores: {Referee.ConnectedCount} conectados" +
                                  (Paid ? $" de {Referee.Settings.MaxPlayers}." : "."));
        }

        // ---------- Solana ----------

        private void PollPlayersInBackground(double now)
        {
            _nextChainPoll = now + ChainPollSeconds;
            PollPlayersAsync().ContinueWith(t =>
            {
                if (t.Exception != null) Debug.LogWarning("No se pudo leer la sala: " + t.Exception.GetBaseException().Message);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private async Task PollPlayersAsync()
        {
            if (!Paid) return;
            _chainBusy = true;
            try
            {
                var match = await _escrow.GetMatchAsync(_options.MatchId);
                if (match != null && Referee.Phase == MatchPhase.Waiting)
                    Referee.SetExpectedPlayers(match.Players.Select(p => p.Key));
            }
            finally
            {
                _chainBusy = false;
            }
        }

        /// <summary>
        /// Envía una instrucción del servidor con reintentos ante fallas de red. Si una transacción
        /// anterior sí llegó (el reintento falla porque el estado ya cambió), se da por hecha.
        /// </summary>
        private async Task SendWithRetry(Solana.Unity.Rpc.Models.TransactionInstruction instruction, string name,
            Escrow.MatchState expectedState)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var signature = await _escrow.SendAsync(_serverKey.PublicKey, Sign, instruction);
                    Debug.Log($"[Servidor] {name} OK: {signature}");
                    return;
                }
                catch (EscrowException e)
                {
                    var match = await TryGetMatch();
                    if (match != null && match.State == expectedState)
                    {
                        Debug.Log($"[Servidor] {name}: la sala ya está en {expectedState}.");
                        return;
                    }
                    // Los errores del programa no cambian al reintentar; los de red sí pueden.
                    if (e.Code != null || attempt >= 5) throw;
                    Debug.LogWarning($"[Servidor] {name} falló (intento {attempt}): {e.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
                }
            }
        }

        private async Task<MatchAccount> TryGetMatch()
        {
            try
            {
                return await _escrow.GetMatchAsync(_options.MatchId);
            }
            catch (EscrowException)
            {
                return null;
            }
        }

        private Task<Solana.Unity.Rpc.Core.Http.RequestResult<string>> Sign(Solana.Unity.Rpc.Models.Transaction tx)
        {
            tx.Sign(_serverKey);
            return _escrow.Rpc.SendTransactionAsync(tx.Serialize(), false, Commitment.Confirmed);
        }
    }
}
