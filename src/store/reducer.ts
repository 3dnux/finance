import { fallbackCategoryId, initialState } from '@/lib/defaults';
import type { Account, AppState, Budget, Category, Settings, Transaction } from '@/types';

export type Action =
  | { type: 'hydrate'; state: AppState }
  | { type: 'reset' }
  | { type: 'replaceAll'; state: AppState }
  | { type: 'addTransaction'; transaction: Transaction }
  | { type: 'updateTransaction'; transaction: Transaction }
  | { type: 'deleteTransaction'; id: string }
  | { type: 'addAccount'; account: Account }
  | { type: 'updateAccount'; account: Account }
  | { type: 'deleteAccount'; id: string }
  | { type: 'addCategory'; category: Category }
  | { type: 'updateCategory'; category: Category }
  | { type: 'deleteCategory'; id: string }
  | { type: 'upsertBudget'; budget: Budget }
  | { type: 'deleteBudget'; id: string }
  | { type: 'updateSettings'; settings: Partial<Settings> };

export function reducer(state: AppState, action: Action): AppState {
  switch (action.type) {
    case 'hydrate':
    case 'replaceAll':
      return action.state;

    case 'reset':
      return { ...initialState(), settings: { ...state.settings, onboarded: true } };

    case 'addTransaction':
      return { ...state, transactions: [action.transaction, ...state.transactions] };

    case 'updateTransaction':
      return {
        ...state,
        transactions: state.transactions.map((tx) =>
          tx.id === action.transaction.id ? action.transaction : tx,
        ),
      };

    case 'deleteTransaction':
      return { ...state, transactions: state.transactions.filter((tx) => tx.id !== action.id) };

    case 'addAccount':
      return { ...state, accounts: [...state.accounts, action.account] };

    case 'updateAccount':
      return {
        ...state,
        accounts: state.accounts.map((a) => (a.id === action.account.id ? action.account : a)),
      };

    case 'deleteAccount': {
      // No dejamos movimientos huérfanos: al borrar una cuenta se borran sus movimientos
      // y las transferencias que la usaban como destino pasan a ser movimientos simples.
      if (state.accounts.length <= 1) return state;
      const transactions = state.transactions
        .filter((tx) => tx.accountId !== action.id)
        .map((tx) =>
          tx.type === 'transfer' && tx.toAccountId === action.id
            ? { ...tx, type: 'expense' as const, toAccountId: undefined, categoryId: fallbackCategoryId('expense') }
            : tx,
        );
      return { ...state, accounts: state.accounts.filter((a) => a.id !== action.id), transactions };
    }

    case 'addCategory':
      return { ...state, categories: [...state.categories, action.category] };

    case 'updateCategory':
      return {
        ...state,
        categories: state.categories.map((c) => (c.id === action.category.id ? action.category : c)),
      };

    case 'deleteCategory': {
      const removed = state.categories.find((c) => c.id === action.id);
      if (!removed) return state;
      const replacement = fallbackCategoryId(removed.kind);
      return {
        ...state,
        categories: state.categories.filter((c) => c.id !== action.id),
        transactions: state.transactions.map((tx) =>
          tx.categoryId === action.id ? { ...tx, categoryId: replacement } : tx,
        ),
        budgets: state.budgets.filter((b) => b.categoryId !== action.id),
      };
    }

    case 'upsertBudget': {
      const exists = state.budgets.some((b) => b.categoryId === action.budget.categoryId);
      return {
        ...state,
        budgets: exists
          ? state.budgets.map((b) => (b.categoryId === action.budget.categoryId ? action.budget : b))
          : [...state.budgets, action.budget],
      };
    }

    case 'deleteBudget':
      return { ...state, budgets: state.budgets.filter((b) => b.id !== action.id) };

    case 'updateSettings':
      return { ...state, settings: { ...state.settings, ...action.settings } };

    default:
      return state;
  }
}
