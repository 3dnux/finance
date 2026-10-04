using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PagaParaMorir.Escrow
{
    /// <summary>Errores del programa (enum <c>GameError</c> en Rust, códigos desde 6000).</summary>
    public enum EscrowErrorCode
    {
        FeeTooHigh = 6000,
        InvalidTimeout = 6001,
        InvalidEntryFee = 6002,
        InvalidMaxPlayers = 6003,
        Paused = 6004,
        InvalidState = 6005,
        MatchFull = 6006,
        AlreadyJoined = 6007,
        NotAPlayer = 6008,
        NotEnoughPlayers = 6009,
        CancelNotAllowed = 6010,
        RefundsPending = 6011,
    }

    /// <summary>Una transacción del escrow falló. <see cref="Message"/> está listo para mostrar al jugador.</summary>
    public class EscrowException : Exception
    {
        /// <summary>Código del programa si el error vino del escrow; <c>null</c> si fue otra cosa.</summary>
        public EscrowErrorCode? Code { get; }
        public IReadOnlyList<string> Logs { get; }

        public EscrowException(string message, EscrowErrorCode? code = null, IReadOnlyList<string> logs = null)
            : base(message)
        {
            Code = code;
            Logs = logs ?? Array.Empty<string>();
        }
    }

    public static class EscrowErrors
    {
        private static readonly Regex AnchorErrorNumber = new Regex(@"Error Number: (\d+)");
        private static readonly Regex CustomProgramError = new Regex(@"custom program error: 0x([0-9a-fA-F]+)");

        /// <summary>Mensaje en español para cada error del programa.</summary>
        public static string Describe(EscrowErrorCode code)
        {
            switch (code)
            {
                case EscrowErrorCode.Paused: return "El juego está en pausa. Intenta más tarde.";
                case EscrowErrorCode.InvalidState: return "La partida ya no acepta esta acción.";
                case EscrowErrorCode.MatchFull: return "La sala está llena.";
                case EscrowErrorCode.AlreadyJoined: return "Ya estás en esta sala.";
                case EscrowErrorCode.NotAPlayer: return "No estás en esta partida.";
                case EscrowErrorCode.NotEnoughPlayers: return "Faltan jugadores para empezar.";
                case EscrowErrorCode.CancelNotAllowed: return "Todavía no se puede cancelar esta partida.";
                case EscrowErrorCode.RefundsPending: return "Aún hay reembolsos pendientes.";
                case EscrowErrorCode.FeeTooHigh: return "La comisión supera el máximo permitido.";
                case EscrowErrorCode.InvalidTimeout: return "Tiempo límite inválido.";
                case EscrowErrorCode.InvalidEntryFee: return "La entrada debe ser mayor a cero.";
                case EscrowErrorCode.InvalidMaxPlayers: return "Número de jugadores inválido.";
                default: return "Error desconocido del juego.";
            }
        }

        /// <summary>Busca en los logs de la transacción el error que devolvió el programa.</summary>
        public static EscrowErrorCode? FindCode(IEnumerable<string> logs)
        {
            if (logs == null) return null;
            foreach (var line in logs)
            {
                if (line == null) continue;
                var anchor = AnchorErrorNumber.Match(line);
                if (anchor.Success && TryCode(int.Parse(anchor.Groups[1].Value, CultureInfo.InvariantCulture), out var c1))
                    return c1;
                var custom = CustomProgramError.Match(line);
                if (custom.Success && TryCode(int.Parse(custom.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), out var c2))
                    return c2;
            }
            return null;
        }

        /// <summary>Convierte un fallo de transacción en un <see cref="EscrowException"/> con un mensaje claro.</summary>
        public static EscrowException FromFailure(string reason, IReadOnlyList<string> logs)
        {
            var code = FindCode(logs);
            if (code.HasValue) return new EscrowException(Describe(code.Value), code, logs);
            if (ContainsInLogs(logs, "insufficient funds"))
                return new EscrowException("No tienes suficiente USDC.", null, logs);
            if (ContainsInLogs(logs, "AccountNotInitialized") || ContainsInLogs(logs, "AccountNotFound")
                || (reason != null && reason.Contains("AccountNotFound")))
                return new EscrowException("Tu billetera no tiene SOL o USDC todavía.", null, logs);
            return new EscrowException("La transacción falló: " + (reason ?? "error desconocido"), null, logs);
        }

        private static bool TryCode(int value, out EscrowErrorCode code)
        {
            code = (EscrowErrorCode)value;
            return Enum.IsDefined(typeof(EscrowErrorCode), value);
        }

        private static bool ContainsInLogs(IEnumerable<string> logs, string text)
        {
            if (logs == null) return false;
            foreach (var line in logs)
                if (line != null && line.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
