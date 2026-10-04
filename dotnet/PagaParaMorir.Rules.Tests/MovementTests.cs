using System;
using System.Numerics;
using PagaParaMorir.Rules;
using Xunit;

namespace PagaParaMorir.Rules.Tests
{
    public class MovementTests
    {
        private static readonly MoveState OnGround = new MoveState(Vector3.Zero, 0, grounded: true);

        private static PlayerInput Walk(float x, float y, float yaw = 0, bool jump = false) =>
            new PlayerInput { Move = new Vector2(x, y), Yaw = yaw, Jump = jump };

        private static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-4f) =>
            Assert.True(Vector3.Distance(expected, actual) < tolerance, $"esperado {expected}, obtenido {actual}");

        [Fact]
        public void Adelante_mira_a_Z_y_a_90_grados_a_X()
        {
            Near(new Vector3(0, 0, 1), Movement.Horizontal(new Vector2(0, 1), 0));
            Near(new Vector3(1, 0, 0), Movement.Horizontal(new Vector2(0, 1), 90));
            Near(new Vector3(1, 0, 0), Movement.Horizontal(new Vector2(1, 0), 0));  // derecha
            Near(new Vector3(0, 0, -1), Movement.Horizontal(new Vector2(1, 0), 90)); // derecha mirando a +X
        }

        [Fact]
        public void En_diagonal_no_se_corre_mas_rapido()
        {
            Assert.Equal(1f, Movement.Horizontal(new Vector2(1, 1), 30).Length(), 4);
        }

        [Fact]
        public void Un_segundo_caminando_avanza_la_velocidad_de_movimiento()
        {
            var s = OnGround;
            for (var i = 0; i < Movement.TickRate; i++) s = Movement.SimulateFlat(s, Walk(0, 1), frozen: false);
            Assert.Equal(Movement.MoveSpeed, s.Position.Z, 3);
            Assert.Equal(0f, s.Position.Y);
            Assert.True(s.Grounded);
        }

        [Fact]
        public void Salta_solo_desde_el_suelo_y_vuelve_a_caer()
        {
            var s = Movement.SimulateFlat(OnGround, Walk(0, 0, jump: true), frozen: false);
            Assert.False(s.Grounded);
            Assert.True(s.Position.Y > 0);

            var height = s.Position.Y;
            s = Movement.SimulateFlat(s, Walk(0, 0, jump: true), frozen: false); // en el aire no hay doble salto
            Assert.True(s.VerticalVelocity < Movement.JumpSpeed);

            for (var i = 0; i < 60 && !s.Grounded; i++) s = Movement.SimulateFlat(s, Walk(0, 0), frozen: false);
            Assert.True(s.Grounded);
            Assert.True(height > 0);
        }

        [Fact]
        public void Congelado_no_camina_ni_salta()
        {
            var s = Movement.SimulateFlat(OnGround, Walk(0, 1, jump: true), frozen: true);
            Assert.Equal(Vector3.Zero, s.Position);
            Assert.True(s.Grounded);
        }
    }

    public class PredictionTests
    {
        private static MoveState Sim(MoveState s, PlayerInput i) => Movement.SimulateFlat(s, i, frozen: false);
        private static bool Same(MoveState a, MoveState b) => Vector3.Distance(a.Position, b.Position) < 0.01f;
        private static PlayerInput Forward(uint tick) => new PlayerInput { Tick = tick, Move = new Vector2(0, 1) };

        private static (PredictionBuffer<PlayerInput, MoveState> buffer, MoveState current) Predict(int ticks)
        {
            var buffer = new PredictionBuffer<PlayerInput, MoveState>();
            var s = new MoveState(Vector3.Zero, 0, true);
            for (uint t = 1; t <= ticks; t++)
            {
                s = Sim(s, Forward(t));
                buffer.Record(t, Forward(t), s);
            }
            return (buffer, s);
        }

        [Fact]
        public void Si_el_servidor_coincide_no_corrige()
        {
            var (buffer, current) = Predict(10);
            var server = new MoveState(new Vector3(0, 0, 4 * Movement.MoveSpeed / Movement.TickRate), Movement.GroundStick + Movement.Gravity / Movement.TickRate, true);
            var result = buffer.Reconcile(4, server, current, Same, Sim, out var corrected);
            Assert.False(corrected);
            Assert.Equal(current.Position, result.Position);
            Assert.Equal(6, buffer.PendingCount);
        }

        [Fact]
        public void Si_el_servidor_difiere_rehace_los_inputs_pendientes()
        {
            var (buffer, current) = Predict(10);
            // El servidor dice que en el tick 4 chocamos con algo y estamos 1 m más atrás.
            var server = new MoveState(new Vector3(0, 0, 4 * Movement.MoveSpeed / Movement.TickRate - 1), 0, true);
            var result = buffer.Reconcile(4, server, current, Same, Sim, out var corrected);
            Assert.True(corrected);
            Assert.Equal(current.Position.Z - 1, result.Position.Z, 3);
            Assert.Equal(1, buffer.Corrections);
        }

        [Fact]
        public void Ignora_estados_del_servidor_que_llegan_tarde()
        {
            var (buffer, current) = Predict(10);
            buffer.Reconcile(6, new MoveState(new Vector3(0, 0, 999), 0, true), current, Same, Sim, out _);
            var result = buffer.Reconcile(3, new MoveState(new Vector3(0, 0, -999), 0, true), current, Same, Sim, out var corrected);
            Assert.False(corrected);
            Assert.Equal(6u, buffer.LastAcknowledged);
            Assert.Equal(current.Position, result.Position);
        }

        [Fact]
        public void El_mismo_tick_confirmado_dos_veces_no_corrige()
        {
            var (buffer, current) = Predict(10);
            var server = new MoveState(new Vector3(0, 0, 4 * Movement.MoveSpeed / Movement.TickRate), 0, true);
            buffer.Reconcile(4, server, current, Same, Sim, out _);
            buffer.Reconcile(4, server, current, Same, Sim, out var corrected);
            Assert.False(corrected);
            Assert.Equal(0, buffer.Corrections);
        }

        [Fact]
        public void Reenvia_los_ultimos_inputs()
        {
            var (buffer, _) = Predict(5);
            Assert.Equal(new uint[] { 3, 4, 5 }, buffer.LatestInputs(3).ConvertAll(i => i.Tick));
        }
    }

    public class ServerInputQueueTests
    {
        private static PlayerInput[] Ticks(params uint[] ticks) => Array.ConvertAll(ticks, t => new PlayerInput { Tick = t });

        [Fact]
        public void Descarta_repetidos_y_viejos_y_ordena()
        {
            var q = new ServerInputQueue();
            Assert.Equal(new uint[] { 1, 2, 3 }, q.Accept(Ticks(3, 1, 2), 0).ConvertAll(i => i.Tick));
            Assert.Equal(new uint[] { 4 }, q.Accept(Ticks(2, 3, 4), 0.1).ConvertAll(i => i.Tick));
            Assert.Empty(q.Accept(Ticks(4), 0.2));
            Assert.Equal(4u, q.LastProcessedTick);
        }

        [Fact]
        public void No_deja_moverse_mas_rapido_mandando_inputs_de_mas()
        {
            var q = new ServerInputQueue(ticksPerSecond: 30, maxBurst: 10);
            var simulated = 0;
            uint tick = 0;
            // Un tramposo manda 90 ticks por segundo durante 2 segundos.
            for (var frame = 0; frame < 60; frame++)
            {
                var now = frame / 30.0;
                simulated += q.Accept(Ticks(++tick, ++tick, ++tick), now).Count;
            }
            // Solo se simulan ~30 por segundo (+ la ráfaga inicial).
            Assert.InRange(simulated, 60, 72);
            Assert.True(q.Throttled > 100);
        }

        [Fact]
        public void Espera_un_tick_que_falta_y_si_no_llega_lo_salta()
        {
            var q = new ServerInputQueue(maxWaitForMissing: 0.1);
            q.Accept(Ticks(1), 0);
            Assert.Empty(q.Accept(Ticks(3, 4), 0.03)); // falta el 2
            Assert.Equal(new uint[] { 2, 3, 4 }, q.Accept(Ticks(2), 0.05).ConvertAll(i => i.Tick));

            Assert.Empty(q.Accept(Ticks(6), 0.1)); // falta el 5 y nunca llega
            Assert.Empty(q.Poll(0.15));
            Assert.Equal(new uint[] { 6 }, q.Poll(0.21).ConvertAll(i => i.Tick));
            Assert.Equal(1, q.Skipped);
        }

        [Fact]
        public void Tras_un_pico_de_lag_acepta_una_rafaga()
        {
            var q = new ServerInputQueue(ticksPerSecond: 30, maxBurst: 10);
            q.Accept(Ticks(1), 0);
            // 300 ms sin paquetes; llegan 9 de golpe.
            var accepted = q.Accept(Ticks(2, 3, 4, 5, 6, 7, 8, 9, 10), 0.3);
            Assert.Equal(9, accepted.Count);
        }
    }

    public class SnapshotBufferTests
    {
        [Fact]
        public void Interpola_entre_posiciones_en_el_pasado()
        {
            var b = new SnapshotBuffer { Delay = 0.1 };
            b.Add(1.0, new Vector3(0, 0, 0), 0);
            b.Add(1.1, new Vector3(10, 0, 0), 90);
            Assert.True(b.Sample(1.15, out var p, out var yaw));
            Assert.Equal(5f, p.X, 3);
            Assert.Equal(45f, yaw, 3);
        }

        [Fact]
        public void Acepta_paquetes_desordenados()
        {
            var b = new SnapshotBuffer { Delay = 0 };
            b.Add(2.0, new Vector3(20, 0, 0), 0);
            b.Add(1.0, new Vector3(10, 0, 0), 0);
            b.Sample(1.5, out var p, out _);
            Assert.Equal(15f, p.X, 3);
        }

        [Fact]
        public void Si_dejan_de_llegar_datos_extrapola_poco_y_se_detiene()
        {
            var b = new SnapshotBuffer { Delay = 0, MaxExtrapolation = 0.1 };
            b.Add(0.0, new Vector3(0, 0, 0), 0);
            b.Add(0.1, new Vector3(1, 0, 0), 0);
            b.Sample(0.15, out var p, out _);
            Assert.Equal(1.5f, p.X, 3);
            b.Sample(5.0, out p, out _);
            Assert.Equal(2f, p.X, 3); // como mucho 0.1 s de extrapolación
        }

        [Fact]
        public void Gira_por_el_camino_corto()
        {
            Assert.Equal(360f, SnapshotBuffer.LerpAngle(350, 10, 0.5f), 3);
        }
    }
}
