using System.Text.Json;

namespace PagaParaMorir.Backend;

/// <summary>
/// Lo poco que el backend guarda en disco: el próximo id de sala, para no reutilizar
/// ids de salas que ya se cerraron (y cuyas cuentas ya no existen on-chain).
/// </summary>
public sealed class StateStore(string path, ulong firstMatchId)
{
    private sealed class State
    {
        public ulong NextMatchId { get; set; }
    }

    private readonly object _lock = new();

    /// <summary>Reserva un id mayor que cualquiera usado antes (en disco o en la cadena).</summary>
    public ulong AllocateMatchId(IEnumerable<ulong> existingIds)
    {
        lock (_lock)
        {
            var state = Load();
            var id = Math.Max(Math.Max(state.NextMatchId, firstMatchId), existingIds.DefaultIfEmpty(0UL).Max() + 1);
            state.NextMatchId = id + 1;
            Save(state);
            return id;
        }
    }

    private State Load()
    {
        if (!File.Exists(path)) return new State();
        return JsonSerializer.Deserialize<State>(File.ReadAllText(path)) ?? new State();
    }

    private void Save(State state)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state));
        File.Move(temp, path, overwrite: true);
    }
}
