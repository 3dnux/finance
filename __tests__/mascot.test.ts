import {
  averageAmount,
  isAngryMood,
  isHappyMood,
  monthCommentFor,
  moodForMonth,
  moodForTransaction,
  phraseFor,
  type CatMood,
} from '@/lib/mascot';
import type { Transaction } from '@/types';

const tx = (type: Transaction['type'], amount: number, id = String(amount)): Transaction => ({
  id,
  type,
  amount,
  date: '2026-08-01',
  accountId: 'a1',
  createdAt: '2026-08-01T10:00:00.000Z',
});

describe('moodForTransaction', () => {
  it('se alegra con los ingresos y se enfada con los gastos', () => {
    expect(moodForTransaction('income', 5_000)).toBe('happy');
    expect(moodForTransaction('expense', 5_000)).toBe('angry');
  });

  it('las transferencias le dan igual: el dinero solo cambia de sitio', () => {
    expect(moodForTransaction('transfer', 500_000)).toBe('neutral');
  });

  it('intensifica la reacción con importes grandes para esa persona', () => {
    // Gasto medio de 10 €: 25 € ya es mucho.
    expect(moodForTransaction('expense', 2_500, 1_000)).toBe('furious');
    expect(moodForTransaction('expense', 1_500, 1_000)).toBe('angry');
    expect(moodForTransaction('income', 2_500, 1_000)).toBe('love');
    expect(moodForTransaction('income', 1_500, 1_000)).toBe('happy');
  });

  it('sin historial usa un umbral fijo de 200 €', () => {
    expect(moodForTransaction('expense', 19_999)).toBe('angry');
    expect(moodForTransaction('expense', 20_000)).toBe('furious');
    expect(moodForTransaction('income', 20_000)).toBe('love');
  });

  it('el importe justo en el umbral ya cuenta como intenso', () => {
    expect(moodForTransaction('expense', 2_000, 1_000)).toBe('furious');
  });
});

describe('averageAmount', () => {
  it('calcula la media por tipo de movimiento', () => {
    const transactions = [tx('expense', 1_000), tx('expense', 3_000), tx('income', 200_000)];
    expect(averageAmount(transactions, 'expense')).toBe(2_000);
    expect(averageAmount(transactions, 'income')).toBe(200_000);
  });

  it('devuelve 0 cuando no hay historial de ese tipo', () => {
    expect(averageAmount([], 'expense')).toBe(0);
    expect(averageAmount([tx('income', 100)], 'expense')).toBe(0);
  });
});

describe('moodForMonth', () => {
  it('se enfurece si se gasta más de lo que entra', () => {
    expect(moodForMonth({ income: 100_000, expense: 150_000, balance: -50_000 })).toBe('furious');
  });

  it('se enfada si hay presupuestos superados aunque el balance sea positivo', () => {
    expect(moodForMonth({ income: 200_000, expense: 100_000, balance: 100_000 }, 2)).toBe('angry');
  });

  it('se enamora cuando se ahorra mucho', () => {
    expect(moodForMonth({ income: 200_000, expense: 100_000, balance: 100_000 })).toBe('love');
  });

  it('se conforma con un ahorro pequeño', () => {
    expect(moodForMonth({ income: 200_000, expense: 190_000, balance: 10_000 })).toBe('happy');
  });

  it('está neutral sin movimientos o con el balance a cero', () => {
    expect(moodForMonth({ income: 0, expense: 0, balance: 0 })).toBe('neutral');
    expect(moodForMonth({ income: 100_000, expense: 100_000, balance: 0 })).toBe('neutral');
  });
});

describe('clasificación de estados', () => {
  it('agrupa los ánimos en contentos y enfadados', () => {
    const moods: CatMood[] = ['love', 'happy', 'neutral', 'angry', 'furious'];
    expect(moods.filter(isHappyMood)).toEqual(['love', 'happy']);
    expect(moods.filter(isAngryMood)).toEqual(['angry', 'furious']);
  });
});

describe('phraseFor', () => {
  it('devuelve siempre una frase, sea cual sea el índice', () => {
    const moods: CatMood[] = ['love', 'happy', 'neutral', 'angry', 'furious'];
    moods.forEach((mood) => {
      [0, 3, 7, 99, -5, NaN].forEach((index) => {
        expect(phraseFor(mood, index).length).toBeGreaterThan(0);
      });
    });
  });

  it('el mismo índice da siempre la misma frase', () => {
    expect(phraseFor('angry', 2)).toBe(phraseFor('angry', 2));
    expect(phraseFor('happy', 0)).not.toBe(phraseFor('angry', 0));
  });
});

describe('monthCommentFor', () => {
  it('explica el motivo del enfado', () => {
    expect(monthCommentFor('furious', { income: 10, expense: 20, balance: -10 })).toMatch(/más de lo que entra/);
    expect(monthCommentFor('angry', { income: 100, expense: 50, balance: 50 }, 1)).toMatch(/Un presupuesto/);
    expect(monthCommentFor('angry', { income: 100, expense: 50, balance: 50 }, 3)).toMatch(/^3 presupuestos/);
  });

  it('avisa cuando el mes está vacío', () => {
    expect(monthCommentFor('neutral', { income: 0, expense: 0, balance: 0 })).toMatch(/siesta/);
  });
});
