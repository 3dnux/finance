export const MONTHS = [
  'enero',
  'febrero',
  'marzo',
  'abril',
  'mayo',
  'junio',
  'julio',
  'agosto',
  'septiembre',
  'octubre',
  'noviembre',
  'diciembre',
];

export const MONTHS_SHORT = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

export const WEEKDAYS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];

const pad = (n: number) => String(n).padStart(2, '0');

/** Fecha local en formato YYYY-MM-DD (no usa UTC para evitar saltos de día). */
export function toISODate(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** Convierte YYYY-MM-DD a Date local (mediodía, para evitar problemas de DST). */
export function fromISODate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, (m ?? 1) - 1, d ?? 1, 12, 0, 0, 0);
}

export function today(): string {
  return toISODate(new Date());
}

/** Clave de mes YYYY-MM a partir de una fecha ISO o de un Date. */
export function monthKey(value: string | Date): string {
  if (typeof value === 'string') return value.slice(0, 7);
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}`;
}

export function currentMonthKey(): string {
  return monthKey(new Date());
}

/** Suma (o resta) meses a una clave YYYY-MM. */
export function addMonths(key: string, delta: number): string {
  const [y, m] = key.split('-').map(Number);
  const date = new Date(y, m - 1 + delta, 1);
  return monthKey(date);
}

/** Devuelve las últimas `count` claves de mes terminando en `key`. */
export function lastMonths(key: string, count: number): string[] {
  return Array.from({ length: count }, (_, i) => addMonths(key, i - (count - 1)));
}

export function monthLabel(key: string, opts: { short?: boolean; withYear?: boolean } = {}): string {
  const [y, m] = key.split('-').map(Number);
  const names = opts.short ? MONTHS_SHORT : MONTHS;
  const name = names[(m ?? 1) - 1] ?? '';
  const capitalized = name.charAt(0).toUpperCase() + name.slice(1);
  return opts.withYear ? `${capitalized} ${y}` : capitalized;
}

/** "12 de agosto de 2026". */
export function formatLongDate(iso: string): string {
  const d = fromISODate(iso);
  return `${d.getDate()} de ${MONTHS[d.getMonth()]} de ${d.getFullYear()}`;
}

/** "Hoy", "Ayer" o "mié, 12 ago". */
export function formatRelativeDay(iso: string, reference = today()): string {
  if (iso === reference) return 'Hoy';
  const ref = fromISODate(reference);
  const yesterday = new Date(ref);
  yesterday.setDate(ref.getDate() - 1);
  if (iso === toISODate(yesterday)) return 'Ayer';

  const d = fromISODate(iso);
  const weekday = WEEKDAYS[d.getDay()].slice(0, 3);
  const label = `${weekday}, ${d.getDate()} ${MONTHS_SHORT[d.getMonth()]}`;
  return d.getFullYear() === ref.getFullYear() ? label : `${label} ${d.getFullYear()}`;
}

export function isSameMonth(iso: string, key: string): boolean {
  return iso.slice(0, 7) === key;
}

/** Número de días del mes de una clave YYYY-MM. */
export function daysInMonth(key: string): number {
  const [y, m] = key.split('-').map(Number);
  return new Date(y, m, 0).getDate();
}

/** Días transcurridos del mes (el mes actual se corta en el día de hoy). */
export function elapsedDays(key: string, reference = today()): number {
  if (key < monthKey(reference)) return daysInMonth(key);
  if (key > monthKey(reference)) return 0;
  return fromISODate(reference).getDate();
}
