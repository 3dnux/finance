using System;
using System.Collections.Generic;

namespace PagaParaMorir.Rules
{
    /// <summary>
    /// La zona segura: un círculo que se cierra por fases. Fuera de él se pierde vida cada segundo.
    /// Los tiempos cuentan desde que empieza la partida (fase Playing).
    /// </summary>
    public sealed class ZoneSchedule
    {
        public sealed class Phase
        {
            /// <summary>Segundos que el círculo se queda quieto antes de cerrarse.</summary>
            public double HoldSeconds { get; }
            /// <summary>Segundos que tarda en cerrarse hasta <see cref="EndRadius"/>.</summary>
            public double ShrinkSeconds { get; }
            public float EndRadius { get; }
            /// <summary>Daño por segundo fuera de la zona durante esta fase.</summary>
            public float DamagePerSecond { get; }

            public Phase(double holdSeconds, double shrinkSeconds, float endRadius, float damagePerSecond)
            {
                HoldSeconds = holdSeconds;
                ShrinkSeconds = shrinkSeconds;
                EndRadius = endRadius;
                DamagePerSecond = damagePerSecond;
            }
        }

        public float InitialRadius { get; }
        public IReadOnlyList<Phase> Phases { get; }

        public ZoneSchedule(float initialRadius, IReadOnlyList<Phase> phases)
        {
            if (phases == null || phases.Count == 0) throw new ArgumentException("La zona necesita al menos una fase.");
            InitialRadius = initialRadius;
            Phases = phases;
        }

        /// <summary>Zona para la arena del prototipo (radio 60 m, ~3 minutos hasta cerrarse).</summary>
        public static ZoneSchedule Default() => new ZoneSchedule(60f, new[]
        {
            new Phase(30, 30, 40f, 2f),
            new Phase(20, 25, 22f, 4f),
            new Phase(15, 20, 10f, 7f),
            new Phase(10, 15, 0f, 12f),
        });

        /// <summary>Duración total hasta que el círculo llega a su tamaño final.</summary>
        public double TotalSeconds
        {
            get
            {
                double total = 0;
                foreach (var p in Phases) total += p.HoldSeconds + p.ShrinkSeconds;
                return total;
            }
        }

        public float RadiusAt(double seconds)
        {
            var start = InitialRadius;
            var t = Math.Max(0, seconds);
            foreach (var p in Phases)
            {
                if (t < p.HoldSeconds) return start;
                t -= p.HoldSeconds;
                if (t < p.ShrinkSeconds)
                    return start + (p.EndRadius - start) * (float)(t / p.ShrinkSeconds);
                t -= p.ShrinkSeconds;
                start = p.EndRadius;
            }
            return start;
        }

        public float DamagePerSecondAt(double seconds)
        {
            var t = Math.Max(0, seconds);
            foreach (var p in Phases)
            {
                var length = p.HoldSeconds + p.ShrinkSeconds;
                if (t < length) return p.DamagePerSecond;
                t -= length;
            }
            return Phases[Phases.Count - 1].DamagePerSecond;
        }
    }
}
