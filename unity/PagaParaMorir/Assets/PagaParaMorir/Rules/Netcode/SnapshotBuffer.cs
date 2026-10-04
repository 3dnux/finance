using System;
using System.Collections.Generic;
using System.Numerics;

namespace PagaParaMorir.Rules
{
    /// <summary>
    /// Interpolación de los demás jugadores: guarda las posiciones que llegan del servidor y
    /// los dibuja un poco en el pasado (<see cref="Delay"/>), entre dos posiciones conocidas.
    /// Así se mueven suave aunque los paquetes lleguen a saltos.
    /// </summary>
    public sealed class SnapshotBuffer
    {
        private struct Snapshot
        {
            public double Time;
            public Vector3 Position;
            public float Yaw;
        }

        private readonly List<Snapshot> _snapshots = new List<Snapshot>();
        private const int MaxSnapshots = 32;

        /// <summary>Cuánto en el pasado se dibuja (segundos). ~3 paquetes a 30 Hz.</summary>
        public double Delay { get; set; } = 0.1;
        /// <summary>Si los paquetes dejan de llegar, se sigue la última dirección como mucho este tiempo.</summary>
        public double MaxExtrapolation { get; set; } = 0.1;

        public int Count => _snapshots.Count;

        public void Add(double time, Vector3 position, float yaw)
        {
            var snapshot = new Snapshot { Time = time, Position = position, Yaw = yaw };
            var index = _snapshots.Count;
            while (index > 0 && _snapshots[index - 1].Time > time) index--; // llegó desordenado
            _snapshots.Insert(index, snapshot);
            while (_snapshots.Count > MaxSnapshots) _snapshots.RemoveAt(0);
        }

        /// <summary>Posición a dibujar en <paramref name="now"/>. Falso si todavía no hay datos.</summary>
        public bool Sample(double now, out Vector3 position, out float yaw)
        {
            position = default;
            yaw = 0;
            if (_snapshots.Count == 0) return false;

            var renderTime = now - Delay;
            var first = _snapshots[0];
            if (_snapshots.Count == 1 || renderTime <= first.Time)
            {
                position = first.Position;
                yaw = first.Yaw;
                return true;
            }

            for (var i = 1; i < _snapshots.Count; i++)
            {
                var b = _snapshots[i];
                if (renderTime > b.Time) continue;
                var a = _snapshots[i - 1];
                var t = (float)((renderTime - a.Time) / Math.Max(1e-6, b.Time - a.Time));
                position = Vector3.Lerp(a.Position, b.Position, t);
                yaw = LerpAngle(a.Yaw, b.Yaw, t);
                return true;
            }

            // Más allá del último dato: seguir un poco la última velocidad y luego quedarse quieto.
            var last = _snapshots[_snapshots.Count - 1];
            var prev = _snapshots[_snapshots.Count - 2];
            var ahead = Math.Min(renderTime - last.Time, MaxExtrapolation);
            var span = Math.Max(1e-6, last.Time - prev.Time);
            position = last.Position + (last.Position - prev.Position) * (float)(ahead / span);
            yaw = last.Yaw;
            return true;
        }

        public void Clear() => _snapshots.Clear();

        /// <summary>Interpola ángulos en grados por el camino más corto (350° → 10° pasa por 0°).</summary>
        public static float LerpAngle(float a, float b, float t)
        {
            var delta = ((b - a) % 360f + 540f) % 360f - 180f;
            return a + delta * t;
        }
    }
}
