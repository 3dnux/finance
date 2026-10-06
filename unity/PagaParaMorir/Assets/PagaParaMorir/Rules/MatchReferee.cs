using System;
using System.Collections.Generic;
using System.Linq;

namespace PagaParaMorir.Rules
{
    public enum MatchPhase : byte
    {
        /// <summary>Sala de espera: los jugadores se conectan; no hay daño.</summary>
        Waiting = 0,
        /// <summary>Cuenta regresiva: todos en su punto de aparición, sin moverse.</summary>
        Countdown = 1,
        Playing = 2,
        Finished = 3,
        /// <summary>No se jugó o falló: cada jugador recupera su entrada.</summary>
        Cancelled = 4,
    }

    public enum EliminationCause : byte
    {
        Weapon = 0,
        Zone = 1,
        Disconnected = 2,
    }

    public enum WinReason : byte
    {
        /// <summary>Fue el último en pie.</summary>
        LastAlive = 0,
        /// <summary>Los últimos cayeron a la vez; ganó el del mejor desempate.</summary>
        LastEliminated = 1,
        /// <summary>Se acabó el tiempo; ganó el mejor de los vivos.</summary>
        TimeLimit = 2,
    }

    public enum LobbyDecision
    {
        Wait,
        Start,
        Cancel,
    }

    public sealed class RefereeSettings
    {
        public int MaxHealth { get; set; } = 100;
        /// <summary>Mínimo de jugadores conectados para jugar.</summary>
        public int MinPlayers { get; set; } = 2;
        /// <summary>Capacidad de la sala: si se llena y todos están conectados, empieza sin esperar.</summary>
        public int MaxPlayers { get; set; } = 100;
        /// <summary>Tiempo máximo de espera en la sala antes de empezar (o cancelar).</summary>
        public double LobbySeconds { get; set; } = 120;
        public double CountdownSeconds { get; set; } = 5;
        public double TimeLimitSeconds { get; set; } = 300;
        public ZoneSchedule Zone { get; set; } = ZoneSchedule.Default();

        /// <summary>Práctica sin dinero: se puede jugar solo y empieza rápido.</summary>
        public static RefereeSettings Practice() => new RefereeSettings
        {
            MinPlayers = 1,
            LobbySeconds = 10,
        };
    }

    public sealed class PlayerRecord
    {
        public string Id { get; }
        public int JoinOrder { get; }
        public int Health { get; internal set; }
        public bool Alive { get; internal set; }
        public bool Connected { get; internal set; }
        public int Kills { get; internal set; }
        public int DamageDealt { get; internal set; }
        /// <summary>NaN mientras siga vivo.</summary>
        public double EliminatedAt { get; internal set; } = double.NaN;
        public EliminationCause? Cause { get; internal set; }
        public string KillerId { get; internal set; }
        internal float ZoneDamageCarry;

        internal PlayerRecord(string id, int joinOrder, int health)
        {
            Id = id;
            JoinOrder = joinOrder;
            Health = health;
            Alive = true;
            Connected = true;
        }
    }

    public sealed class Elimination
    {
        public string VictimId { get; }
        /// <summary><c>null</c> si murió por la zona o se desconectó.</summary>
        public string KillerId { get; }
        public EliminationCause Cause { get; }
        public double At { get; }
        /// <summary>El golpe final fue en la cabeza.</summary>
        public bool Headshot { get; }

        public Elimination(string victimId, string killerId, EliminationCause cause, double at, bool headshot = false)
        {
            VictimId = victimId;
            KillerId = killerId;
            Cause = cause;
            At = at;
            Headshot = headshot;
        }
    }

    public sealed class MatchResult
    {
        public string WinnerId { get; }
        public WinReason Reason { get; }

        public MatchResult(string winnerId, WinReason reason)
        {
            WinnerId = winnerId;
            Reason = reason;
        }
    }

    /// <summary>
    /// Árbitro de la partida: decide cuándo empieza, aplica el daño y define al ganador.
    /// Corre solo en el servidor. Los jugadores se identifican por su llave pública (base58).
    /// </summary>
    public sealed class MatchReferee
    {
        public RefereeSettings Settings { get; }
        public MatchPhase Phase { get; private set; } = MatchPhase.Waiting;
        public double PhaseStartedAt { get; private set; }
        public double LobbyDeadline { get; }
        public double PlayingStartedAt { get; private set; } = double.NaN;
        /// <summary>Cuántos jugadores empezaron la partida.</summary>
        public int StartedWith { get; private set; }
        public MatchResult Result { get; private set; }
        /// <summary>Por qué se canceló, para mostrarlo a los jugadores.</summary>
        public string CancelReason { get; private set; }

        public IEnumerable<PlayerRecord> Players => _order;
        public int AliveCount => _order.Count(p => p.Alive);
        public int ConnectedCount => _order.Count(p => p.Connected);

        private readonly Dictionary<string, PlayerRecord> _players = new Dictionary<string, PlayerRecord>();
        private readonly List<PlayerRecord> _order = new List<PlayerRecord>();
        private HashSet<string> _expected;
        private int _nextJoinOrder;

        /// <param name="expectedPlayers">Quienes pagaron la entrada; <c>null</c> = cualquiera (práctica).</param>
        public MatchReferee(RefereeSettings settings, double now, IEnumerable<string> expectedPlayers = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PhaseStartedAt = now;
            LobbyDeadline = now + settings.LobbySeconds;
            if (expectedPlayers != null) SetExpectedPlayers(expectedPlayers);
        }

        public PlayerRecord Get(string id) => id != null && _players.TryGetValue(id, out var p) ? p : null;

        /// <summary>Actualiza la lista de quienes pagaron (cambia mientras la sala está abierta).</summary>
        public void SetExpectedPlayers(IEnumerable<string> ids) => _expected = new HashSet<string>(ids);

        public bool IsExpected(string id) => _expected == null || _expected.Contains(id);

        // ---------- Sala de espera ----------

        public bool CanJoin(string id, out string reason)
        {
            reason = null;
            if (Phase != MatchPhase.Waiting) reason = "La partida ya empezó.";
            else if (!IsExpected(id)) reason = "No pagaste la entrada de esta partida.";
            else if (_players.TryGetValue(id, out var p) && p.Connected) reason = "Ya estás conectado desde otro equipo.";
            else if (ConnectedCount >= Settings.MaxPlayers) reason = "La sala está llena.";
            return reason == null;
        }

        public PlayerRecord Join(string id)
        {
            if (!CanJoin(id, out var reason)) throw new InvalidOperationException(reason);
            if (_players.TryGetValue(id, out var existing))
            {
                existing.Connected = true;
                return existing;
            }
            var record = new PlayerRecord(id, _nextJoinOrder++, Settings.MaxHealth);
            _players[id] = record;
            _order.Add(record);
            return record;
        }

        /// <summary>
        /// El jugador se desconectó. En la sala de espera simplemente sale;
        /// con la partida empezada queda eliminado.
        /// </summary>
        public Elimination Leave(string id, double now)
        {
            if (!_players.TryGetValue(id, out var p)) return null;
            p.Connected = false;
            if (Phase == MatchPhase.Waiting)
            {
                _players.Remove(id);
                _order.Remove(p);
                return null;
            }
            if ((Phase == MatchPhase.Countdown || Phase == MatchPhase.Playing) && p.Alive)
            {
                var e = Eliminate(p, null, EliminationCause.Disconnected, now);
                CheckFinish(now);
                return e;
            }
            return null;
        }

        public LobbyDecision EvaluateLobby(double now)
        {
            if (Phase != MatchPhase.Waiting) return LobbyDecision.Wait;
            var connected = ConnectedCount;
            var everyonePaidIsHere = _expected == null || _expected.All(id => Get(id)?.Connected == true);
            var roomFull = (_expected?.Count ?? connected) >= Settings.MaxPlayers;
            if (roomFull && everyonePaidIsHere && connected >= Settings.MinPlayers) return LobbyDecision.Start;
            if (now >= LobbyDeadline) return connected >= Settings.MinPlayers ? LobbyDecision.Start : LobbyDecision.Cancel;
            return LobbyDecision.Wait;
        }

        /// <summary>Cierra la sala y empieza la cuenta regresiva con los conectados.</summary>
        public void BeginCountdown(double now)
        {
            if (Phase != MatchPhase.Waiting) throw new InvalidOperationException("La partida ya empezó.");
            foreach (var p in _order.Where(p => !p.Connected).ToList())
            {
                _players.Remove(p.Id);
                _order.Remove(p);
            }
            foreach (var p in _order)
            {
                p.Health = Settings.MaxHealth;
                p.Alive = true;
            }
            StartedWith = _order.Count;
            SetPhase(MatchPhase.Countdown, now);
        }

        public void Cancel(double now, string reason)
        {
            if (Phase == MatchPhase.Finished || Phase == MatchPhase.Cancelled) return;
            CancelReason = reason;
            SetPhase(MatchPhase.Cancelled, now);
        }

        // ---------- Partida ----------

        /// <summary>Avanza el reloj: fin de la cuenta regresiva y límite de tiempo.</summary>
        public void Update(double now)
        {
            if (Phase == MatchPhase.Countdown && now >= PhaseStartedAt + Settings.CountdownSeconds)
            {
                PlayingStartedAt = now;
                SetPhase(MatchPhase.Playing, now);
                CheckFinish(now);
            }
            else if (Phase == MatchPhase.Playing && now >= PlayingStartedAt + Settings.TimeLimitSeconds)
            {
                var best = Rank(_order.Where(p => p.Alive), byHealth: true).FirstOrDefault();
                if (best != null) Finish(best, WinReason.TimeLimit, now);
                else CheckFinish(now);
            }
        }

        public double SecondsPlayed(double now) => Phase >= MatchPhase.Playing ? now - PlayingStartedAt : 0;

        public double SecondsLeft(double now)
        {
            switch (Phase)
            {
                case MatchPhase.Waiting: return Math.Max(0, LobbyDeadline - now);
                case MatchPhase.Countdown: return Math.Max(0, PhaseStartedAt + Settings.CountdownSeconds - now);
                case MatchPhase.Playing: return Math.Max(0, PlayingStartedAt + Settings.TimeLimitSeconds - now);
                default: return 0;
            }
        }

        public float ZoneRadius(double now)
        {
            if (Phase == MatchPhase.Waiting || Phase == MatchPhase.Countdown) return Settings.Zone.InitialRadius;
            var t = Phase == MatchPhase.Playing ? now - PlayingStartedAt : PhaseStartedAt - PlayingStartedAt;
            return Settings.Zone.RadiusAt(double.IsNaN(t) ? 0 : t);
        }

        /// <summary>Daño de arma. Devuelve la eliminación si el golpe fue mortal.</summary>
        public Elimination ApplyDamage(string attackerId, string victimId, int amount, double now, bool headshot = false)
        {
            if (Phase != MatchPhase.Playing || amount <= 0 || attackerId == victimId) return null;
            var attacker = Get(attackerId);
            var victim = Get(victimId);
            if (attacker == null || victim == null || !attacker.Alive || !victim.Alive) return null;

            var dealt = Math.Min(amount, victim.Health);
            victim.Health -= dealt;
            attacker.DamageDealt += dealt;
            if (victim.Health > 0) return null;

            attacker.Kills++;
            var e = Eliminate(victim, attacker.Id, EliminationCause.Weapon, now, headshot);
            CheckFinish(now);
            return e;
        }

        /// <summary>Daño de la zona a quienes están fuera durante <paramref name="deltaSeconds"/>.</summary>
        public List<Elimination> ApplyZoneDamage(IEnumerable<string> outsideIds, double deltaSeconds, double now)
        {
            var eliminations = new List<Elimination>();
            if (Phase != MatchPhase.Playing || deltaSeconds <= 0) return eliminations;
            var dps = Settings.Zone.DamagePerSecondAt(now - PlayingStartedAt);
            foreach (var id in outsideIds)
            {
                var p = Get(id);
                if (p == null || !p.Alive) continue;
                p.ZoneDamageCarry += (float)(dps * deltaSeconds);
                var damage = (int)p.ZoneDamageCarry;
                if (damage <= 0) continue;
                p.ZoneDamageCarry -= damage;
                p.Health = Math.Max(0, p.Health - damage);
                if (p.Health == 0) eliminations.Add(Eliminate(p, null, EliminationCause.Zone, now));
            }
            if (eliminations.Count > 0) CheckFinish(now);
            return eliminations;
        }

        private Elimination Eliminate(PlayerRecord p, string killerId, EliminationCause cause, double now, bool headshot = false)
        {
            p.Alive = false;
            p.Health = 0;
            p.EliminatedAt = now;
            p.Cause = cause;
            p.KillerId = killerId;
            return new Elimination(p.Id, killerId, cause, now, headshot);
        }

        private void CheckFinish(double now)
        {
            if (Phase != MatchPhase.Playing) return;
            var alive = _order.Where(p => p.Alive).ToList();
            if (alive.Count == 1 && StartedWith >= 2)
            {
                Finish(alive[0], WinReason.LastAlive, now);
                return;
            }
            if (alive.Count > 0) return;

            // Nadie quedó en pie: gana el mejor de los que cayeron al último.
            var lastAt = _order.Max(p => p.EliminatedAt);
            var lastBatch = _order.Where(p => p.EliminatedAt == lastAt).ToList();
            if (lastBatch.All(p => p.Cause == EliminationCause.Disconnected))
            {
                // Todos se desconectaron a la vez: probablemente falló la red o el servidor.
                Cancel(now, "Todos los jugadores se desconectaron.");
                return;
            }
            Finish(Rank(lastBatch, byHealth: false).First(), WinReason.LastEliminated, now);
        }

        private void Finish(PlayerRecord winner, WinReason reason, double now)
        {
            Result = new MatchResult(winner.Id, reason);
            SetPhase(MatchPhase.Finished, now);
        }

        /// <summary>Desempate: (vida), bajas, daño causado y, al final, quién entró primero.</summary>
        private static IEnumerable<PlayerRecord> Rank(IEnumerable<PlayerRecord> players, bool byHealth) =>
            players
                .OrderByDescending(p => byHealth ? p.Health : 0)
                .ThenByDescending(p => p.Kills)
                .ThenByDescending(p => p.DamageDealt)
                .ThenBy(p => p.JoinOrder);

        private void SetPhase(MatchPhase phase, double now)
        {
            Phase = phase;
            PhaseStartedAt = now;
        }
    }
}
