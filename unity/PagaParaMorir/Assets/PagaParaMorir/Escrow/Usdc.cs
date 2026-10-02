using System.Globalization;

namespace PagaParaMorir.Escrow
{
    /// <summary>Conversión entre unidades mínimas de USDC (6 decimales) y texto para la UI.</summary>
    public static class Usdc
    {
        public const int Decimals = 6;
        public const ulong OneUsdc = 1_000_000;

        public static decimal ToDecimal(ulong units) => (decimal)units / OneUsdc;

        public static ulong FromDecimal(decimal amount) => (ulong)decimal.Round(amount * OneUsdc);

        /// <summary>Ej.: 5_000_000 → "5.00 USDC"; 1_234_567 → "1.234567 USDC".</summary>
        public static string Format(ulong units)
        {
            var value = ToDecimal(units);
            var text = value == decimal.Round(value, 2)
                ? value.ToString("0.00", CultureInfo.InvariantCulture)
                : value.ToString("0.######", CultureInfo.InvariantCulture);
            return text + " USDC";
        }
    }
}
