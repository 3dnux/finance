export interface CurrencyInfo {
  code: string;
  symbol: string;
  name: string;
  /** true si el símbolo va delante del importe. */
  prefix: boolean;
}

export const CURRENCIES: CurrencyInfo[] = [
  { code: 'EUR', symbol: '€', name: 'Euro', prefix: false },
  { code: 'USD', symbol: '$', name: 'Dólar estadounidense', prefix: true },
  { code: 'MXN', symbol: '$', name: 'Peso mexicano', prefix: true },
  { code: 'COP', symbol: '$', name: 'Peso colombiano', prefix: true },
  { code: 'ARS', symbol: '$', name: 'Peso argentino', prefix: true },
  { code: 'CLP', symbol: '$', name: 'Peso chileno', prefix: true },
  { code: 'PEN', symbol: 'S/', name: 'Sol peruano', prefix: true },
  { code: 'BRL', symbol: 'R$', name: 'Real brasileño', prefix: true },
  { code: 'GBP', symbol: '£', name: 'Libra esterlina', prefix: true },
];

export function currencyInfo(code: string): CurrencyInfo {
  return CURRENCIES.find((c) => c.code === code) ?? CURRENCIES[0];
}

/** Agrupa los millares con punto y usa coma decimal (formato es-ES). */
function groupDigits(intPart: string): string {
  return intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
}

export interface FormatOptions {
  /** Oculta los decimales cuando el importe es redondo o siempre. */
  hideDecimals?: boolean;
  /** Añade el signo + a los importes positivos. */
  signed?: boolean;
  /** Oculta el símbolo de moneda. */
  noSymbol?: boolean;
}

/** Convierte céntimos a un texto legible: 123456 -> "1.234,56 €". */
export function formatMoney(cents: number, currency = 'EUR', options: FormatOptions = {}): string {
  const info = currencyInfo(currency);
  const negative = cents < 0;
  const abs = Math.abs(Math.round(cents));

  // Sin decimales redondeamos al entero más cercano: 42,50 € se muestra como 43 €,
  // no como 42 €, que daría la sensación de haber perdido dinero por el camino.
  let body: string;
  if (options.hideDecimals) {
    body = groupDigits(String(Math.round(abs / 100)));
  } else {
    body = `${groupDigits(String(Math.floor(abs / 100)))},${String(abs % 100).padStart(2, '0')}`;
  }

  // Menos tipográfico (U+2212): se alinea con los dígitos mejor que el guion.
  const sign = negative ? '−' : options.signed ? '+' : '';
  if (options.noSymbol) return `${sign}${body}`;
  return info.prefix ? `${sign}${info.symbol}${body}` : `${sign}${body} ${info.symbol}`;
}

/** Versión compacta para ejes de gráficos: 1234567 -> "12,3 mil". */
export function formatCompact(cents: number, currency = 'EUR'): string {
  const info = currencyInfo(currency);
  const units = Math.round(cents / 100);
  const abs = Math.abs(units);
  const sign = units < 0 ? '−' : '';
  let body: string;
  if (abs >= 1_000_000) body = `${(abs / 1_000_000).toFixed(abs >= 10_000_000 ? 0 : 1).replace('.', ',')}M`;
  else if (abs >= 1000) body = `${(abs / 1000).toFixed(abs >= 10_000 ? 0 : 1).replace('.', ',')}k`;
  else body = String(abs);
  return info.prefix ? `${sign}${info.symbol}${body}` : `${sign}${body} ${info.symbol}`;
}

/**
 * Interpreta lo escrito en el teclado ("1234,5") y lo pasa a céntimos.
 * Devuelve 0 si el texto no es un número válido.
 */
export function parseAmount(input: string): number {
  const normalized = input.replace(/\s/g, '').replace(/\./g, '').replace(',', '.');
  if (!normalized || normalized === '.') return 0;
  const value = Number(normalized);
  if (!Number.isFinite(value) || value < 0) return 0;
  return Math.round(value * 100);
}

/** Pasa céntimos al texto editable del teclado: 123450 -> "1234,5". */
export function amountToInput(cents: number): string {
  if (!cents) return '';
  const units = Math.floor(Math.abs(cents) / 100);
  const decimals = Math.abs(cents) % 100;
  if (decimals === 0) return String(units);
  const decimalText = decimals % 10 === 0 ? String(decimals / 10) : String(decimals).padStart(2, '0');
  return `${units},${decimalText}`;
}

/** Formatea el texto en curso del teclado para mostrarlo con separadores. */
export function formatInputDisplay(input: string): string {
  if (!input) return '0';
  const [intPart, decimalPart] = input.split(',');
  const grouped = groupDigits(intPart || '0');
  return decimalPart === undefined ? grouped : `${grouped},${decimalPart}`;
}

/** Porcentaje 0..1 protegido frente a divisiones por cero. */
export function ratio(part: number, total: number): number {
  if (!total) return 0;
  return part / total;
}

export function formatPercent(value: number, decimals = 0): string {
  return `${(value * 100).toFixed(decimals).replace('.', ',')} %`;
}
