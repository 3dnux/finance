import { initialState, sampleState } from '@/lib/defaults';
import { accountBalances, monthSummary } from '@/lib/selectors';
import { migrate, parseImport } from '@/store/persistence';
import { reducer } from '@/store/reducer';
import type { AppState, Transaction } from '@/types';

const base: AppState = {
  ...initialState(),
  transactions: [
    {
      id: 't1',
      type: 'expense',
      amount: 5_000,
      date: '2026-08-01',
      accountId: 'acc-bank',
      categoryId: 'cat-food',
      createdAt: '2026-08-01T09:00:00.000Z',
    },
    {
      id: 't2',
      type: 'transfer',
      amount: 10_000,
      date: '2026-08-02',
      accountId: 'acc-bank',
      toAccountId: 'acc-cash',
      createdAt: '2026-08-02T09:00:00.000Z',
    },
  ],
  budgets: [{ id: 'b1', categoryId: 'cat-food', limit: 20_000 }],
};

const nueva: Transaction = {
  id: 't3',
  type: 'income',
  amount: 100_000,
  date: '2026-08-03',
  accountId: 'acc-bank',
  categoryId: 'cat-salary',
  createdAt: '2026-08-03T09:00:00.000Z',
};

describe('reducer de movimientos', () => {
  it('añade, actualiza y elimina sin mutar el estado anterior', () => {
    const added = reducer(base, { type: 'addTransaction', transaction: nueva });
    expect(added.transactions).toHaveLength(3);
    expect(base.transactions).toHaveLength(2);

    const updated = reducer(added, {
      type: 'updateTransaction',
      transaction: { ...nueva, amount: 150_000 },
    });
    expect(updated.transactions.find((t) => t.id === 't3')!.amount).toBe(150_000);

    const deleted = reducer(updated, { type: 'deleteTransaction', id: 't3' });
    expect(deleted.transactions.map((t) => t.id)).toEqual(['t1', 't2']);
  });
});

describe('reducer de cuentas', () => {
  it('al borrar una cuenta elimina sus movimientos', () => {
    const result = reducer(base, { type: 'deleteAccount', id: 'acc-bank' });
    expect(result.accounts.map((a) => a.id)).toEqual(['acc-cash']);
    expect(result.transactions).toHaveLength(0);
  });

  it('convierte en gasto la transferencia cuyo destino desaparece', () => {
    const result = reducer(base, { type: 'deleteAccount', id: 'acc-cash' });
    const convertida = result.transactions.find((t) => t.id === 't2')!;
    expect(convertida.type).toBe('expense');
    expect(convertida.toAccountId).toBeUndefined();
    expect(convertida.categoryId).toBe('cat-other-expense');
  });

  it('nunca deja el estado sin cuentas', () => {
    const unaSola: AppState = { ...base, accounts: [base.accounts[0]], transactions: [] };
    expect(reducer(unaSola, { type: 'deleteAccount', id: unaSola.accounts[0].id })).toBe(unaSola);
  });
});

describe('reducer de categorías', () => {
  it('reasigna los movimientos y borra el presupuesto asociado', () => {
    const result = reducer(base, { type: 'deleteCategory', id: 'cat-food' });
    expect(result.transactions.find((t) => t.id === 't1')!.categoryId).toBe('cat-other-expense');
    expect(result.budgets).toHaveLength(0);
    expect(result.categories.find((c) => c.id === 'cat-food')).toBeUndefined();
  });

  it('no pierde importes al reasignar', () => {
    const result = reducer(base, { type: 'deleteCategory', id: 'cat-food' });
    expect(monthSummary(result.transactions, '2026-08').expense).toBe(
      monthSummary(base.transactions, '2026-08').expense,
    );
  });
});

describe('reducer de presupuestos', () => {
  it('upsertBudget actualiza en lugar de duplicar la misma categoría', () => {
    const result = reducer(base, {
      type: 'upsertBudget',
      budget: { id: 'b2', categoryId: 'cat-food', limit: 45_000 },
    });
    expect(result.budgets).toHaveLength(1);
    expect(result.budgets[0].limit).toBe(45_000);
  });

  it('añade el presupuesto cuando la categoría es nueva', () => {
    const result = reducer(base, {
      type: 'upsertBudget',
      budget: { id: 'b2', categoryId: 'cat-transport', limit: 8_000 },
    });
    expect(result.budgets).toHaveLength(2);
  });
});

describe('ajustes y reinicio', () => {
  it('updateSettings hace merge parcial', () => {
    const result = reducer(base, { type: 'updateSettings', settings: { currency: 'MXN' } });
    expect(result.settings.currency).toBe('MXN');
    expect(result.settings.themeMode).toBe(base.settings.themeMode);
  });

  it('reset vacía los datos pero conserva los ajustes', () => {
    const conAjustes = reducer(base, { type: 'updateSettings', settings: { currency: 'USD', themeMode: 'dark' } });
    const result = reducer(conAjustes, { type: 'reset' });
    expect(result.transactions).toHaveLength(0);
    expect(result.budgets).toHaveLength(0);
    expect(result.settings.currency).toBe('USD');
    expect(result.settings.themeMode).toBe('dark');
  });
});

describe('persistencia', () => {
  it('migrate rellena los campos que falten', () => {
    const result = migrate({ transactions: [] });
    expect(result.accounts.length).toBeGreaterThan(0);
    expect(result.categories.length).toBeGreaterThan(0);
    expect(result.settings.currency).toBe('EUR');
  });

  it('migrate tolera basura', () => {
    expect(migrate(null).transactions).toEqual([]);
    expect(migrate('texto').accounts.length).toBeGreaterThan(0);
  });

  it('parseImport rechaza JSON inválido o ajeno a la app', () => {
    expect(() => parseImport('no soy json')).toThrow(/JSON/);
    expect(() => parseImport('{"foo": 1}')).toThrow(/copia de seguridad/);
  });

  it('parseImport acepta una exportación real', () => {
    const exported = JSON.stringify(sampleState());
    const result = parseImport(exported);
    expect(result.transactions.length).toBeGreaterThan(0);
    expect(result.accounts.length).toBe(4);
  });
});

describe('datos de ejemplo', () => {
  it('genera cuentas con saldos coherentes', () => {
    const state = sampleState();
    const balances = accountBalances(state.accounts, state.transactions);
    expect(Object.keys(balances)).toHaveLength(4);
    // El ahorro solo recibe transferencias, así que crece respecto al inicio.
    expect(balances['acc-savings']).toBeGreaterThan(450_000);
  });

  it('todos los movimientos apuntan a cuentas y categorías existentes', () => {
    const state = sampleState();
    const accountIds = new Set(state.accounts.map((a) => a.id));
    const categoryIds = new Set(state.categories.map((c) => c.id));
    state.transactions.forEach((tx) => {
      expect(accountIds.has(tx.accountId)).toBe(true);
      if (tx.toAccountId) expect(accountIds.has(tx.toAccountId)).toBe(true);
      if (tx.categoryId) expect(categoryIds.has(tx.categoryId)).toBe(true);
    });
  });

  it('no crea movimientos con fecha futura', () => {
    const state = sampleState();
    const hoy = new Date();
    const iso = `${hoy.getFullYear()}-${String(hoy.getMonth() + 1).padStart(2, '0')}-${String(hoy.getDate()).padStart(2, '0')}`;
    state.transactions.forEach((tx) => {
      expect(tx.date <= iso).toBe(true);
    });
  });
});
