using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PagaParaMorir.Rules;
using Xunit;

namespace PagaParaMorir.Rules.Tests
{
    /// <summary>
    /// Cliente y servidor completos sobre una red simulada con latencia, pérdida y desorden.
    /// El cliente predice con el mismo código que usa el servidor; debe terminar donde el
    /// servidor dice, sin correcciones cuando el mundo es igual en ambos lados.
    /// </summary>
    public class NetcodeSimulationTests
    {
        private sealed class Network<T>
        {
            private readonly List<(double at, T message)> _inFlight = new List<(double, T)>();
            private readonly Random _rng;
            private readonly double _latency;
            private readonly double _jitter;
            private readonly double _loss;

            public Network(int seed, double latency, double jitter, double loss)
            {
                _rng = new Random(seed);
                _latency = latency;
                _jitter = jitter;
                _loss = loss;
            }

            public void Send(double now, T message)
            {
                if (_rng.NextDouble() < _loss) return;
                _inFlight.Add((now + _latency + _rng.NextDouble() * _jitter, message));
            }

            public List<T> Receive(double now)
            {
                var arrived = _inFlight.Where(m => m.at <= now).OrderBy(m => m.at).ToList();
                _inFlight.RemoveAll(m => m.at <= now);
                return arrived.Select(m => m.message).ToList();
            }
        }

        private struct ServerState
        {
            public uint Ack;
            public MoveState State;
        }

        private static bool Same(MoveState a, MoveState b) => Vector3.Distance(a.Position, b.Position) < 0.01f;

        /// <summary>
        /// Juega <paramref name="seconds"/> segundos; <paramref name="serverWall"/> es un muro que solo
        /// existe en el servidor (para forzar correcciones).
        /// </summary>
        private static (MoveState client, MoveState server, PredictionBuffer<PlayerInput, MoveState> buffer, ServerInputQueue queue)
            Play(double seconds, double latency, double loss, float? serverWall = null, int seed = 7)
        {
            var up = new Network<PlayerInput[]>(seed, latency, latency * 0.3, loss);
            var down = new Network<ServerState>(seed + 1, latency, latency * 0.3, loss);
            var buffer = new PredictionBuffer<PlayerInput, MoveState>();
            var queue = new ServerInputQueue();
            var start = new MoveState(Vector3.Zero, 0, true);
            var client = start;
            var server = start;
            var rng = new Random(seed);

            MoveState ServerSim(MoveState s, PlayerInput i)
            {
                var next = Movement.SimulateFlat(s, i, frozen: false);
                if (serverWall.HasValue && next.Position.Z > serverWall.Value)
                    next.Position = new Vector3(next.Position.X, next.Position.Y, serverWall.Value);
                return next;
            }

            var steps = (int)(seconds * Movement.TickRate);
            for (uint tick = 1; tick <= steps; tick++)
            {
                var now = tick * (double)Movement.TickSeconds;

                // Cliente: input aleatorio que cambia cada medio segundo, predicción y envío (3 por paquete).
                if (tick % 15 == 1) rng.Next();
                var input = new PlayerInput
                {
                    Tick = tick,
                    Move = new Vector2((float)Math.Sin(tick / 20.0), 1),
                    Yaw = (tick / 3) % 360,
                    Jump = tick % 47 == 0,
                };
                client = Movement.SimulateFlat(client, input, frozen: false);
                buffer.Record(tick, input, client);
                up.Send(now, buffer.LatestInputs(3).ToArray());

                // Servidor: procesa lo que llegó y publica su estado con el último tick procesado.
                foreach (var packet in up.Receive(now))
                    foreach (var frame in queue.Accept(packet, now))
                        server = ServerSim(server, frame);
                foreach (var frame in queue.Poll(now)) server = ServerSim(server, frame);
                down.Send(now, new ServerState { Ack = queue.LastProcessedTick, State = server });

                // Cliente: reconcilia con lo último que llegó del servidor.
                foreach (var state in down.Receive(now))
                    client = buffer.Reconcile(state.Ack, state.State, client, Same,
                        (s, i) => Movement.SimulateFlat(s, i, frozen: false), out _);
            }

            // Se deja de jugar: el resto de los paquetes termina de llegar.
            for (var extra = 1; extra <= 60; extra++)
            {
                var now = (steps + extra) * (double)Movement.TickSeconds;
                foreach (var packet in up.Receive(now))
                    foreach (var frame in queue.Accept(packet, now))
                        server = ServerSim(server, frame);
                foreach (var frame in queue.Poll(now)) server = ServerSim(server, frame);
                down.Send(now, new ServerState { Ack = queue.LastProcessedTick, State = server });
                foreach (var state in down.Receive(now))
                    client = buffer.Reconcile(state.Ack, state.State, client, Same,
                        (s, i) => Movement.SimulateFlat(s, i, frozen: false), out _);
            }
            return (client, server, buffer, queue);
        }

        [Fact]
        public void Con_120ms_de_latencia_y_paquetes_desordenados_no_hay_correcciones()
        {
            var (client, server, buffer, queue) = Play(seconds: 10, latency: 0.12, loss: 0);
            Assert.Equal(0, queue.Skipped);
            Assert.Equal(0, queue.Throttled);
            Assert.Equal(0, buffer.Corrections);
            Assert.True(Vector3.Distance(client.Position, server.Position) < 0.01f);
            Assert.True(server.Position.Length() > 30); // de verdad se movió
        }

        [Fact]
        public void Con_10_por_ciento_de_paquetes_perdidos_termina_donde_dice_el_servidor()
        {
            var (client, server, buffer, queue) = Play(seconds: 10, latency: 0.1, loss: 0.10);
            Assert.True(Vector3.Distance(client.Position, server.Position) < 0.01f,
                $"cliente {client.Position} vs servidor {server.Position}");
            // Con 3 inputs por paquete casi nunca se pierde uno del todo.
            Assert.True(queue.Skipped < 5, $"{queue.Skipped} ticks perdidos");
            Assert.True(buffer.Corrections < 10, $"{buffer.Corrections} correcciones");
        }

        [Fact]
        public void Si_el_servidor_ve_algo_distinto_el_cliente_se_corrige()
        {
            var (client, server, buffer, _) = Play(seconds: 5, latency: 0.08, loss: 0, serverWall: 5f);
            Assert.True(buffer.Corrections > 0);
            Assert.True(server.Position.Z <= 5f);
            // Al final el cliente está donde el servidor (contra el muro), no donde predijo.
            Assert.True(Vector3.Distance(client.Position, server.Position) < 0.01f,
                $"cliente {client.Position} vs servidor {server.Position}");
        }
    }
}
