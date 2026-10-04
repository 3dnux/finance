using System;
using System.Collections.Generic;

namespace PagaParaMorir.Rules
{
    /// <summary>
    /// Inputs que llegan al servidor de un jugador. Cada paquete trae los últimos ticks
    /// (por si se perdió alguno). Esto:
    /// <list type="bullet">
    /// <item>descarta repetidos y viejos, y los entrega en orden;</item>
    /// <item>si falta un tick, lo espera un momento (puede venir en un paquete desordenado)
    /// y si no llega lo salta;</item>
    /// <item>limita cuántos ticks se simulan por segundo para que nadie se mueva más rápido
    /// mandando inputs de más (speed hack).</item>
    /// </list>
    /// </summary>
    public sealed class ServerInputQueue
    {
        private readonly double _ticksPerSecond;
        private readonly double _maxBurst;
        private readonly double _maxWaitForMissing;
        private readonly SortedDictionary<uint, (PlayerInput input, double arrived)> _pending =
            new SortedDictionary<uint, (PlayerInput, double)>();
        private double _budget;
        private double _lastRefill = double.NaN;

        public uint LastProcessedTick { get; private set; }
        /// <summary>Ticks descartados por llegar más rápido de lo permitido.</summary>
        public int Throttled { get; private set; }
        /// <summary>Ticks que nunca llegaron y se saltaron.</summary>
        public int Skipped { get; private set; }

        /// <param name="ticksPerSecond">Ritmo normal del cliente.</param>
        /// <param name="maxBurst">Ticks que se aceptan de golpe (p. ej. tras un pico de lag).</param>
        /// <param name="maxWaitForMissing">Segundos que se espera un tick faltante antes de saltarlo.</param>
        public ServerInputQueue(double ticksPerSecond = Movement.TickRate, double maxBurst = 10,
            double maxWaitForMissing = 0.1)
        {
            _ticksPerSecond = ticksPerSecond;
            _maxBurst = maxBurst;
            _maxWaitForMissing = maxWaitForMissing;
            _budget = maxBurst;
        }

        /// <summary>Recibe un paquete y devuelve, en orden, los inputs que hay que simular ahora.</summary>
        public List<PlayerInput> Accept(IEnumerable<PlayerInput> frames, double now)
        {
            foreach (var frame in frames)
                if (frame.Tick > LastProcessedTick && !_pending.ContainsKey(frame.Tick))
                    _pending[frame.Tick] = (frame, now);
            return Drain(now);
        }

        /// <summary>Sin paquetes nuevos: entrega lo que ya esperó suficiente (llamar cada tick).</summary>
        public List<PlayerInput> Poll(double now) => Drain(now);

        private List<PlayerInput> Drain(double now)
        {
            Refill(now);
            var ready = new List<PlayerInput>();
            while (_pending.Count > 0)
            {
                var (tick, entry) = First();
                if (tick != LastProcessedTick + 1)
                {
                    // Hueco: esperar un poco por si el tick que falta viene en camino.
                    if (now - entry.arrived < _maxWaitForMissing) break;
                    Skipped += (int)(tick - LastProcessedTick - 1);
                }
                _pending.Remove(tick);
                LastProcessedTick = tick;
                if (_budget < 1)
                {
                    // Se marca como procesado pero no se simula: el cliente será corregido.
                    Throttled++;
                    continue;
                }
                _budget -= 1;
                ready.Add(entry.input);
            }
            return ready;
        }

        private (uint tick, (PlayerInput input, double arrived) entry) First()
        {
            foreach (var pair in _pending) return (pair.Key, pair.Value);
            throw new InvalidOperationException();
        }

        private void Refill(double now)
        {
            if (!double.IsNaN(_lastRefill))
                _budget = Math.Min(_maxBurst, _budget + (now - _lastRefill) * _ticksPerSecond);
            _lastRefill = now;
        }
    }
}
