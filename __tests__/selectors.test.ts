import {
  accountBalance,
  accountBalances,
  budgetProgress,
  filterTransactions,
  groupByDay,
  monthlySeries,
  monthSummary,
  totalBalance,
  totalsByCategory,
  transactionEffect,
} from '@/lib/selectors';
import type { Account, Budget, Category, Transaction } from '@/types';

const accounts: Account[] = [
  { id: 'a1', name: 'Banco', type: 'bank', color: '#000', initialBalance: 100_000, createdAt: '2026-01-01' },
  { id: 'a2', name: 'Ahorro', type: 'savings', color: '#111', initialBalance: 50_000, createdAt: '2026-01-01' },
  { id: 'a3', name: 'Tarjeta', type: 'card', color: '#222', initialBalance: -20_000, createdAt: '2026-01-01' },
];

const categories: Category[] = [
  { id: 'c1', name: 'Alimentación', kind: 'expense', icon: 'cart', color: '#0F9D58' },
  { id: 'c2', name: 'Ocio', kind: 'expense', icon: 'game', color: '#7C3AED' },
  { id: 'c3', name: 'Nómina', kind: 'income', icon: 'briefcase', color: '#2563EB' },
];

const tx = (over: Partial<Transaction> & Pick<Transaction, 'id' | 'type' | 'amount' | 'date' | 'accountId'>): Transaction => ({
  createdAt: `2026-08-01T10:00:00.000Z`,
  ...over,
});

const transactions: Transaction[] = [
  tx({ id: 't1', type: 'income', amount: 200_000, date: '2026-08-01', accountId: 'a1', categoryId: 'c3' }),
  tx({ id: 't2', type: 'expense', amount: 30_000, date: '2026-08-02', accountId: 'a1', categoryId: 'c1' }),
  tx({ id: 't3', type: 'expense', amount: 10_000, date: '2026-08-02', accountId: 'a3', categoryId: 'c2', note: 'Cine' }),
  tx({ id: 't4', type: 'transfer', amount: 40_000, date: '2026-08-03', accountId: 'a1', toAccountId: 'a2' }),
  tx({ id: 't5', type: 'expense', amount: 5_000, date: '2026-07-15', accountId: 'a1', categoryId: 'c1' }),
];

describe('transactionEffect', () => {
  it('resta los gastos de su cuenta y no afecta a las demás', () => {
    expect(transactionEffect(transactions[1], 'a1')).toBe(-30_000);
    expect(transactionEffect(transactions[1], 'a2')).toBe(0);
  });

  it('suma los ingresos a su cuenta', () => {
    expect(transactionEffect(transactions[0], 'a1')).toBe(200_000);
  });

  it('mueve el importe de origen a destino en las transferencias', () => {
    expect(transactionEffect(transactions[3], 'a1')).toBe(-40_000);
    expect(transactionEffect(transactions[3], 'a2')).toBe(40_000);
    expect(transactionEffect(transactions[3], 'a3')).toBe(0);
  });
});

describe('saldos', () => {
  it('calcula el saldo de una cuenta desde su saldo inicial', () => {
    // 100.000 + 200.000 − 30.000 − 40.000 − 5.000
    expect(accountBalance(accounts[0], transactions)).toBe(225_000);
    expect(accountBalance(accounts[1], transactions)).toBe(90_000);
    expect(accountBalance(accounts[2], transactions)).toBe(-30_000);
  });

  it('accountBalances coincide con accountBalance cuenta a cuenta', () => {
    const map = accountBalances(accounts, transactions);
    accounts.forEach((account) => {
      expect(map[account.id]).toBe(accountBalance(account, transactions));
    });
  });

  it('las transferencias no alteran el patrimonio total', () => {
    const sinTransferencia = transactions.filter((t) => t.type !== 'transfer');
    expect(totalBalance(accounts, transactions)).toBe(totalBalance(accounts, sinTransferencia));
  });

  it('excluye las cuentas archivadas del total', () => {
    const conArchivada = accounts.map((a) => (a.id === 'a2' ? { ...a, archived: true } : a));
    expect(totalBalance(conArchivada, transactions)).toBe(225_000 + -30_000);
  });
});

describe('monthSummary', () => {
  it('suma ingresos y gastos del mes ignorando transferencias', () => {
    expect(monthSummary(transactions, '2026-08')).toEqual({
      income: 200_000,
      expense: 40_000,
      balance: 160_000,
    });
  });

  it('aísla cada mes', () => {
    expect(monthSummary(transactions, '2026-07')).toEqual({ income: 0, expense: 5_000, balance: -5_000 });
  });

  it('devuelve ceros en un mes sin datos', () => {
    expect(monthSummary(transactions, '2026-06')).toEqual({ income: 0, expense: 0, balance: 0 });
  });
});

describe('totalsByCategory', () => {
  it('ordena las categorías por importe y calcula su peso', () => {
    const totals = totalsByCategory(transactions, categories, '2026-08', 'expense');
    expect(totals.map((t) => t.category.id)).toEqual(['c1', 'c2']);
    expect(totals[0].total).toBe(30_000);
    expect(totals[0].share).toBeCloseTo(0.75);
    expect(totals[1].share).toBeCloseTo(0.25);
  });

  it('los pesos suman 1', () => {
    const totals = totalsByCategory(transactions, categories, '2026-08', 'expense');
    expect(totals.reduce((sum, t) => sum + t.share, 0)).toBeCloseTo(1);
  });

  it('puede desglosar también los ingresos', () => {
    const totals = totalsByCategory(transactions, categories, '2026-08', 'income');
    expect(totals).toHaveLength(1);
    expect(totals[0].category.id).toBe('c3');
  });
});

describe('monthlySeries', () => {
  it('devuelve una entrada por mes, aunque esté vacío', () => {
    const series = monthlySeries(transactions, '2026-08', 3);
    expect(series.map((p) => p.key)).toEqual(['2026-06', '2026-07', '2026-08']);
    expect(series[0]).toEqual({ key: '2026-06', income: 0, expense: 0 });
    expect(series[1].expense).toBe(5_000);
    expect(series[2]).toEqual({ key: '2026-08', income: 200_000, expense: 40_000 });
  });
});

describe('budgetProgress', () => {
  const budgets: Budget[] = [
    { id: 'b1', categoryId: 'c1', limit: 25_000 },
    { id: 'b2', categoryId: 'c2', limit: 60_000 },
  ];

  it('marca como superado el presupuesto que se pasa del límite', () => {
    const rows = budgetProgress(budgets, categories, transactions, '2026-08');
    const alimentacion = rows.find((r) => r.budget.id === 'b1')!;
    expect(alimentacion.spent).toBe(30_000);
    expect(alimentacion.progress).toBeCloseTo(1.2);
    expect(alimentacion.remaining).toBe(-5_000);
    expect(alimentacion.status).toBe('over');
  });

  it('ordena de mayor a menor consumo', () => {
    const rows = budgetProgress(budgets, categories, transactions, '2026-08');
    expect(rows[0].budget.id).toBe('b1');
  });

  it('ignora los gastos de otros meses', () => {
    const rows = budgetProgress(budgets, categories, transactions, '2026-07');
    expect(rows.find((r) => r.budget.id === 'b1')!.spent).toBe(5_000);
  });
});

describe('groupByDay', () => {
  it('agrupa por fecha descendente con el balance de cada día', () => {
    const groups = groupByDay(transactions);
    expect(groups.map((g) => g.date)).toEqual(['2026-08-03', '2026-08-02', '2026-08-01', '2026-07-15']);
    // El día 2 tiene dos gastos: −30.000 y −10.000.
    expect(groups[1].total).toBe(-40_000);
    // Una transferencia no cambia el balance del día.
    expect(groups[0].total).toBe(0);
  });
});

describe('filterTransactions', () => {
  it('filtra por tipo', () => {
    const result = filterTransactions(transactions, categories, { types: ['expense'] });
    expect(result).toHaveLength(3);
  });

  it('filtra por cuenta incluyendo transferencias entrantes', () => {
    const result = filterTransactions(transactions, categories, { accountId: 'a2' });
    expect(result.map((t) => t.id)).toEqual(['t4']);
  });

  it('busca en la nota y en el nombre de la categoría', () => {
    expect(filterTransactions(transactions, categories, { search: 'cine' }).map((t) => t.id)).toEqual(['t3']);
    expect(filterTransactions(transactions, categories, { search: 'aliment' }).map((t) => t.id)).toEqual(['t2', 't5']);
  });

  it('combina mes y categoría', () => {
    const result = filterTransactions(transactions, categories, { monthKey: '2026-08', categoryId: 'c1' });
    expect(result.map((t) => t.id)).toEqual(['t2']);
  });

  it('ordena de más reciente a más antiguo', () => {
    const result = filterTransactions(transactions, categories, {});
    expect(result[0].date >= result[result.length - 1].date).toBe(true);
    expect(result[0].id).toBe('t4');
  });
});
