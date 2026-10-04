using System;
using System.Collections.Generic;

namespace PagaParaMorir.Rules
{
    /// <summary>
    /// Historial del cliente para la predicción: por cada tick guarda el input que mandó y el
    /// estado que predijo. Cuando llega el estado del servidor para un tick ya confirmado,
    /// si no coincide con lo predicho, parte del estado del servidor y vuelve a aplicar los
    /// inputs que el servidor todavía no procesó (reconciliación).
    /// </summary>
    public sealed class PredictionBuffer<TInput, TState>
    {
        private struct Entry
        {
            public uint Tick;
            public TInput Input;
            public TState State;
        }

        private readonly LinkedList<Entry> _entries = new LinkedList<Entry>();
        private readonly int _capacity;

        /// <summary>Último tick que el servidor confirmó haber procesado.</summary>
        public uint LastAcknowledged { get; private set; }
        public int PendingCount => _entries.Count;
        public int Corrections { get; private set; }

        public PredictionBuffer(int capacity = 256)
        {
            _capacity = capacity;
        }

        /// <summary>Guarda el input de un tick y el estado que resultó de aplicarlo.</summary>
        public void Record(uint tick, TInput input, TState stateAfter)
        {
            if (_entries.Last != null && tick <= _entries.Last.Value.Tick)
                throw new ArgumentException("Los ticks deben ser crecientes.");
            _entries.AddLast(new Entry { Tick = tick, Input = input, State = stateAfter });
            while (_entries.Count > _capacity) _entries.RemoveFirst();
        }

        /// <summary>Inputs más recientes (para reenviar varios por paquete).</summary>
        public List<TInput> LatestInputs(int count)
        {
            var result = new List<TInput>(count);
            for (var node = _entries.Last; node != null && result.Count < count; node = node.Previous)
                result.Insert(0, node.Value.Input);
            return result;
        }

        /// <summary>
        /// Compara el estado del servidor en <paramref name="ackTick"/> con lo predicho.
        /// Devuelve el estado actual corregido (o <paramref name="current"/> si no hizo falta).
        /// </summary>
        /// <param name="matches">¿Lo predicho es lo bastante parecido a lo del servidor?</param>
        /// <param name="simulate">Aplica un input a un estado (mismo código que en el servidor).</param>
        public TState Reconcile(uint ackTick, TState serverState, TState current,
            Func<TState, TState, bool> matches, Func<TState, TInput, TState> simulate, out bool corrected)
        {
            corrected = false;
            // Viejo (llegó tarde) o repetido: el servidor manda su estado cada tick aunque no
            // haya procesado inputs nuevos, y ese tick ya lo comparamos.
            if (ackTick <= LastAcknowledged) return current;
            LastAcknowledged = ackTick;

            var predicted = default(TState);
            var found = false;
            while (_entries.First != null && _entries.First.Value.Tick <= ackTick)
            {
                if (_entries.First.Value.Tick == ackTick)
                {
                    predicted = _entries.First.Value.State;
                    found = true;
                }
                _entries.RemoveFirst();
            }
            if (found && matches(predicted, serverState)) return current;

            // Nos equivocamos (o el servidor nos movió): rehacer desde su estado.
            corrected = true;
            Corrections++;
            var state = serverState;
            for (var node = _entries.First; node != null; node = node.Next)
            {
                state = simulate(state, node.Value.Input);
                var entry = node.Value;
                entry.State = state;
                node.Value = entry;
            }
            return state;
        }

        public void Clear()
        {
            _entries.Clear();
            LastAcknowledged = 0;
        }
    }
}
