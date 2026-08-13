import type { Account, AppState, Budget, Category, Transaction } from '@/types';

import { currentMonthKey, daysInMonth, elapsedDays, isSameMonth, lastMonths, monthKey } from './date';

/**
 * Efecto de un movimiento sobre una cuenta concreta, en céntimos.
 * Un gasto resta de su cuenta, un ingreso suma, y una transferencia
 * resta en origen y suma en destino.
 */
export function transactionEffect(tx: Transaction, accountId: string): number {
  if (tx.type === 'expense') return tx.accountId === accountId ? -tx.amount : 0;
  if (tx.type === 'income') return tx.accountId === accountId ? tx.amount : 0;
  if (tx.accountId === accountId) return -tx.amount;
  if (tx.toAccountId === accountId) return tx.amount;
  return 0;
}

export function accountBalance(account: Account, transactions: Transaction[]): number {
  return transactions.reduce((sum, tx) => sum + transactionEffect(tx, account.id), account.initialBalance);
}

/** Saldos por cuenta, indexados por id. */
export function accountBalances(accounts: Account[], transactions: Transaction[]): Record<string, number> {
  const balances: Record<string, number> = {};
  accounts.forEach((account) => {
    balances[account.id] = account.initialBalance;
  });
  transactions.forEach((tx) => {
    if (tx.type === 'expense' && balances[tx.accountId] !== undefined) balances[tx.accountId] -= tx.amount;
    else if (tx.type === 'income' && balances[tx.accountId] !== undefined) balances[tx.accountId] += tx.amount;
    else if (tx.type === 'transfer') {
      if (balances[tx.accountId] !== undefined) balances[tx.accountId] -= tx.amount;
      if (tx.toAccountId && balances[tx.toAccountId] !== undefined) balances[tx.toAccountId] += tx.amount;
    }
  });
  return balances;
}

/** Patrimonio total: suma de los saldos de todas las cuentas activas. */
export function totalBalance(accounts: Account[], transactions: Transaction[]): number {
  const balances = accountBalances(accounts, transactions);
  return accounts.filter((a) => !a.archived).reduce((sum, a) => sum + (balances[a.id] ?? 0), 0);
}

export interface MonthSummary {
  income: number;
  expense: number;
  balance: number;
}

/** Ingresos y gastos de un mes. Las transferencias no cuentan: mueven dinero, no lo crean. */
export function monthSummary(transactions: Transaction[], key = currentMonthKey()): MonthSummary {
  let income = 0;
  let expense = 0;
  transactions.forEach((tx) => {
    if (!isSameMonth(tx.date, key)) return;
    if (tx.type === 'income') income += tx.amount;
    else if (tx.type === 'expense') expense += tx.amount;
  });
  return { income, expense, balance: income - expense };
}

export interface CategoryTotal {
  category: Category;
  total: number;
  count: number;
  share: number;
}

/** Gasto (o ingreso) agrupado por categoría en un mes, de mayor a menor. */
export function totalsByCategory(
  transactions: Transaction[],
  categories: Category[],
  key: string,
  kind: 'expense' | 'income' = 'expense',
): CategoryTotal[] {
  const byId = new Map<string, { total: number; count: number }>();
  let grandTotal = 0;

  transactions.forEach((tx) => {
    if (tx.type !== kind || !isSameMonth(tx.date, key)) return;
    const id = tx.categoryId ?? '';
    const entry = byId.get(id) ?? { total: 0, count: 0 };
    entry.total += tx.amount;
    entry.count += 1;
    byId.set(id, entry);
    grandTotal += tx.amount;
  });

  const unknown: Category = {
    id: '',
    name: 'Sin categoría',
    kind,
    icon: 'help-circle-outline',
    color: '#64748B',
  };

  return [...byId.entries()]
    .map(([id, entry]) => ({
      category: categories.find((c) => c.id === id) ?? unknown,
      total: entry.total,
      count: entry.count,
      share: grandTotal ? entry.total / grandTotal : 0,
    }))
    .sort((a, b) => b.total - a.total);
}

export interface MonthPoint {
  key: string;
  income: number;
  expense: number;
}

/** Serie de ingresos/gastos de los últimos `count` meses, terminando en `key`. */
export function monthlySeries(transactions: Transaction[], key: string, count = 6): MonthPoint[] {
  const keys = lastMonths(key, count);
  const index = new Map(keys.map((k) => [k, { key: k, income: 0, expense: 0 }]));
  transactions.forEach((tx) => {
    const entry = index.get(monthKey(tx.date));
    if (!entry) return;
    if (tx.type === 'income') entry.income += tx.amount;
    else if (tx.type === 'expense') entry.expense += tx.amount;
  });
  return keys.map((k) => index.get(k)!);
}

export interface BudgetProgress {
  budget: Budget;
  category: Category | undefined;
  spent: number;
  limit: number;
  /** Proporción gastada; puede superar 1 si hay exceso. */
  progress: number;
  remaining: number;
  /** Gasto que "tocaría" a estas alturas del mes si el ritmo fuera constante. */
  pace: number;
  status: 'ok' | 'warning' | 'over';
}

export function budgetProgress(
  budgets: Budget[],
  categories: Category[],
  transactions: Transaction[],
  key = currentMonthKey(),
): BudgetProgress[] {
  const spentByCategory = new Map<string, number>();
  transactions.forEach((tx) => {
    if (tx.type !== 'expense' || !isSameMonth(tx.date, key) || !tx.categoryId) return;
    spentByCategory.set(tx.categoryId, (spentByCategory.get(tx.categoryId) ?? 0) + tx.amount);
  });

  const paceRatio = elapsedDays(key) / daysInMonth(key);

  return budgets
    .map((budget) => {
      const spent = spentByCategory.get(budget.categoryId) ?? 0;
      const progress = budget.limit ? spent / budget.limit : 0;
      const pace = Math.round(budget.limit * paceRatio);
      const status: BudgetProgress['status'] = progress >= 1 ? 'over' : spent > pace ? 'warning' : 'ok';
      return {
        budget,
        category: categories.find((c) => c.id === budget.categoryId),
        spent,
        limit: budget.limit,
        progress,
        remaining: budget.limit - spent,
        pace,
        status,
      };
    })
    .sort((a, b) => b.progress - a.progress);
}

export interface DayGroup {
  date: string;
  total: number;
  items: Transaction[];
}

/** Agrupa movimientos por día, de más reciente a más antiguo. */
export function groupByDay(transactions: Transaction[]): DayGroup[] {
  const groups = new Map<string, Transaction[]>();
  transactions.forEach((tx) => {
    const list = groups.get(tx.date) ?? [];
    list.push(tx);
    groups.set(tx.date, list);
  });

  return [...groups.entries()]
    .sort((a, b) => (a[0] < b[0] ? 1 : -1))
    .map(([date, items]) => ({
      date,
      total: items.reduce((sum, tx) => {
        if (tx.type === 'income') return sum + tx.amount;
        if (tx.type === 'expense') return sum - tx.amount;
        return sum;
      }, 0),
      items: [...items].sort((a, b) => (a.createdAt < b.createdAt ? 1 : -1)),
    }));
}

export interface TransactionFilter {
  search?: string;
  types?: Transaction['type'][];
  accountId?: string;
  categoryId?: string;
  monthKey?: string;
}

export function filterTransactions(
  transactions: Transaction[],
  categories: Category[],
  filter: TransactionFilter,
): Transaction[] {
  const search = filter.search?.trim().toLowerCase();
  const categoryNames = new Map(categories.map((c) => [c.id, c.name.toLowerCase()]));

  return transactions
    .filter((tx) => {
      if (filter.monthKey && !isSameMonth(tx.date, filter.monthKey)) return false;
      if (filter.types?.length && !filter.types.includes(tx.type)) return false;
      if (filter.accountId && tx.accountId !== filter.accountId && tx.toAccountId !== filter.accountId) return false;
      if (filter.categoryId && tx.categoryId !== filter.categoryId) return false;
      if (search) {
        const haystack = `${tx.note ?? ''} ${categoryNames.get(tx.categoryId ?? '') ?? ''}`.toLowerCase();
        if (!haystack.includes(search)) return false;
      }
      return true;
    })
    .sort((a, b) => (a.date === b.date ? (a.createdAt < b.createdAt ? 1 : -1) : a.date < b.date ? 1 : -1));
}

/** Meses con movimientos, de más reciente a más antiguo (incluye siempre el actual). */
export function availableMonths(transactions: Transaction[]): string[] {
  const keys = new Set<string>([currentMonthKey()]);
  transactions.forEach((tx) => keys.add(monthKey(tx.date)));
  return [...keys].sort((a, b) => (a < b ? 1 : -1));
}

/** Media de gasto diario del mes, útil para proyecciones. */
export function dailyAverageExpense(transactions: Transaction[], key: string): number {
  const { expense } = monthSummary(transactions, key);
  const days = Math.max(1, elapsedDays(key));
  return Math.round(expense / days);
}

/** Proyección de gasto al cierre del mes al ritmo actual. */
export function projectedExpense(transactions: Transaction[], key: string): number {
  return dailyAverageExpense(transactions, key) * daysInMonth(key);
}

export function findAccount(state: AppState, id: string | undefined): Account | undefined {
  return id ? state.accounts.find((a) => a.id === id) : undefined;
}

export function findCategory(state: AppState, id: string | undefined): Category | undefined {
  return id ? state.categories.find((c) => c.id === id) : undefined;
}
