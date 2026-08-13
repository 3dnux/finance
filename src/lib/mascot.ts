import type { MonthSummary } from './selectors';
import type { Transaction, TransactionType } from '@/types';

/** Estados de ánimo del gatito, de más contento a más enfadado. */
export type CatMood = 'love' | 'happy' | 'neutral' | 'angry' | 'furious';

export function isHappyMood(mood: CatMood): boolean {
  return mood === 'love' || mood === 'happy';
}

export function isAngryMood(mood: CatMood): boolean {
  return mood === 'angry' || mood === 'furious';
}

/** Importe a partir del cual la reacción es intensa cuando no hay historial. */
const DEFAULT_INTENSE_AMOUNT = 20_000;

/**
 * Importe típico de los movimientos de un tipo, usado para saber si uno concreto
 * es «grande» para esta persona. Sin historial devuelve 0.
 */
export function averageAmount(transactions: Transaction[], type: TransactionType): number {
  const amounts = transactions.filter((tx) => tx.type === type).map((tx) => tx.amount);
  if (!amounts.length) return 0;
  return Math.round(amounts.reduce((sum, value) => sum + value, 0) / amounts.length);
}

/**
 * Cómo se pone el gatito al registrar un movimiento: contento con los ingresos,
 * enfadado con los gastos, e indiferente con las transferencias (el dinero solo
 * cambia de sitio). La reacción se intensifica si el importe es grande respecto
 * a lo habitual de esa persona.
 */
export function moodForTransaction(type: TransactionType, amount: number, reference = 0): CatMood {
  if (type === 'transfer') return 'neutral';
  const threshold = reference > 0 ? reference * 2 : DEFAULT_INTENSE_AMOUNT;
  const intense = amount >= threshold;
  if (type === 'income') return intense ? 'love' : 'happy';
  return intense ? 'furious' : 'angry';
}

/**
 * Cómo se siente el gatito con el mes en conjunto: manda lo que se sale de
 * madre (presupuestos superados o gastar más de lo que entra).
 */
export function moodForMonth(summary: MonthSummary, budgetsOver = 0): CatMood {
  if (summary.income === 0 && summary.expense === 0) return 'neutral';
  if (summary.expense > summary.income) return 'furious';
  if (budgetsOver > 0) return 'angry';
  const savingsRate = summary.income > 0 ? summary.balance / summary.income : 0;
  if (savingsRate >= 0.3) return 'love';
  if (savingsRate > 0) return 'happy';
  return 'neutral';
}

const PHRASES: Record<CatMood, string[]> = {
  love: ['¡Qué maravilla!', '¡Miau millonario!', '¡Así se hace!', '¡Ronroneo de felicidad!'],
  happy: ['¡Bien hecho!', '¡Miau!', 'Me gusta esto', 'Un poquito más de atún'],
  neutral: ['Vale, anotado', 'Aquí no ha pasado nada', 'Mmm…', 'De un bolsillo a otro'],
  angry: ['¡Grrr!', 'Otra vez no…', 'Ese dinero me gustaba', '¡Miau de reproche!'],
  furious: ['¡MIAAAU!', '¡Esto es un escándalo!', '¡Se acabó el atún!', '¡Bufido máximo!'],
};

/**
 * Frase del gatito. `index` permite elegirla (la pantalla pasa un número al azar)
 * y siempre cae dentro del rango.
 */
export function phraseFor(mood: CatMood, index = 0): string {
  const options = PHRASES[mood];
  const safe = Number.isFinite(index) ? Math.abs(Math.trunc(index)) : 0;
  return options[safe % options.length];
}

/** Comentario del gatito sobre el mes, más informativo que la frase corta. */
export function monthCommentFor(mood: CatMood, summary: MonthSummary, budgetsOver = 0): string {
  if (summary.income === 0 && summary.expense === 0) return 'Aún no hay nada este mes. Estoy echando la siesta.';
  if (mood === 'furious') return 'Estás gastando más de lo que entra. Vigila el mes.';
  if (mood === 'angry') {
    return budgetsOver === 1
      ? 'Un presupuesto se te ha ido de las manos.'
      : `${budgetsOver} presupuestos se te han ido de las manos.`;
  }
  if (mood === 'love') return 'Vas ahorrando de lo lindo. Ronroneo de aprobación.';
  if (mood === 'happy') return 'Vas ahorrando, aunque hay margen de mejora.';
  return 'Ni fu ni fa: este mes vas justo de balance.';
}
