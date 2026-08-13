export type TransactionType = 'expense' | 'income' | 'transfer';

export type AccountType = 'cash' | 'bank' | 'card' | 'savings' | 'investment';

export interface Account {
  id: string;
  name: string;
  type: AccountType;
  color: string;
  /** Saldo de partida en céntimos (puede ser negativo en tarjetas de crédito). */
  initialBalance: number;
  archived?: boolean;
  createdAt: string;
}

export interface Category {
  id: string;
  name: string;
  kind: Exclude<TransactionType, 'transfer'>;
  /** Nombre de icono de @expo/vector-icons/Ionicons. */
  icon: string;
  color: string;
  custom?: boolean;
}

export interface Transaction {
  id: string;
  type: TransactionType;
  /** Importe siempre positivo, en céntimos. */
  amount: number;
  /** Fecha en formato YYYY-MM-DD. */
  date: string;
  /** Cuenta de origen (gasto/transferencia) o destino (ingreso). */
  accountId: string;
  /** Cuenta destino, solo para transferencias. */
  toAccountId?: string;
  /** Categoría, no aplica a transferencias. */
  categoryId?: string;
  note?: string;
  createdAt: string;
}

export interface Budget {
  id: string;
  categoryId: string;
  /** Límite mensual en céntimos. */
  limit: number;
}

export interface Settings {
  currency: string;
  themeMode: 'system' | 'light' | 'dark';
  /** Muestra el gatito que reacciona a ingresos y gastos. */
  mascot: boolean;
  onboarded: boolean;
}

export interface AppState {
  version: number;
  accounts: Account[];
  categories: Category[];
  transactions: Transaction[];
  budgets: Budget[];
  settings: Settings;
}
