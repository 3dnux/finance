import type { Account, AccountType, AppState, Category, Transaction } from '@/types';

export const ACCOUNT_TYPES: { value: AccountType; label: string; icon: string }[] = [
  { value: 'cash', label: 'Efectivo', icon: 'cash-outline' },
  { value: 'bank', label: 'Cuenta bancaria', icon: 'business-outline' },
  { value: 'card', label: 'Tarjeta de crédito', icon: 'card-outline' },
  { value: 'savings', label: 'Ahorro', icon: 'wallet-outline' },
  { value: 'investment', label: 'Inversión', icon: 'trending-up-outline' },
];

export function accountTypeLabel(type: AccountType): string {
  return ACCOUNT_TYPES.find((t) => t.value === type)?.label ?? 'Cuenta';
}

export function accountTypeIcon(type: AccountType): string {
  return ACCOUNT_TYPES.find((t) => t.value === type)?.icon ?? 'wallet-outline';
}

export const DEFAULT_CATEGORIES: Category[] = [
  // Gastos
  { id: 'cat-food', name: 'Alimentación', kind: 'expense', icon: 'cart-outline', color: '#0F9D58' },
  { id: 'cat-dining', name: 'Restaurantes', kind: 'expense', icon: 'restaurant-outline', color: '#EA580C' },
  { id: 'cat-home', name: 'Hogar', kind: 'expense', icon: 'home-outline', color: '#2563EB' },
  { id: 'cat-bills', name: 'Facturas', kind: 'expense', icon: 'flash-outline', color: '#D97706' },
  { id: 'cat-transport', name: 'Transporte', kind: 'expense', icon: 'car-outline', color: '#0D9488' },
  { id: 'cat-health', name: 'Salud', kind: 'expense', icon: 'medkit-outline', color: '#DB2777' },
  { id: 'cat-leisure', name: 'Ocio', kind: 'expense', icon: 'game-controller-outline', color: '#7C3AED' },
  { id: 'cat-shopping', name: 'Compras', kind: 'expense', icon: 'shirt-outline', color: '#E23D3D' },
  { id: 'cat-education', name: 'Educación', kind: 'expense', icon: 'school-outline', color: '#0284C7' },
  { id: 'cat-travel', name: 'Viajes', kind: 'expense', icon: 'airplane-outline', color: '#65A30D' },
  { id: 'cat-subscriptions', name: 'Suscripciones', kind: 'expense', icon: 'wifi-outline', color: '#4F46E5' },
  { id: 'cat-other-expense', name: 'Otros gastos', kind: 'expense', icon: 'ellipsis-horizontal-outline', color: '#64748B' },
  // Ingresos
  { id: 'cat-salary', name: 'Nómina', kind: 'income', icon: 'briefcase-outline', color: '#0F9D58' },
  { id: 'cat-freelance', name: 'Autónomo', kind: 'income', icon: 'laptop-outline', color: '#0D9488' },
  { id: 'cat-investment', name: 'Inversiones', kind: 'income', icon: 'trending-up-outline', color: '#2563EB' },
  { id: 'cat-gift', name: 'Regalos', kind: 'income', icon: 'gift-outline', color: '#DB2777' },
  { id: 'cat-other-income', name: 'Otros ingresos', kind: 'income', icon: 'ellipsis-horizontal-outline', color: '#64748B' },
];

export function defaultAccounts(): Account[] {
  const createdAt = new Date().toISOString();
  return [
    { id: 'acc-cash', name: 'Efectivo', type: 'cash', color: '#0F9D58', initialBalance: 0, createdAt },
    { id: 'acc-bank', name: 'Cuenta bancaria', type: 'bank', color: '#4F46E5', initialBalance: 0, createdAt },
  ];
}

export const STATE_VERSION = 1;

export function initialState(): AppState {
  return {
    version: STATE_VERSION,
    accounts: defaultAccounts(),
    categories: DEFAULT_CATEGORIES,
    transactions: [],
    budgets: [],
    settings: {
      currency: 'EUR',
      themeMode: 'system',
      onboarded: false,
    },
  };
}

/** Categoría por defecto para un tipo, usada cuando la original fue borrada. */
export function fallbackCategoryId(kind: 'expense' | 'income'): string {
  return kind === 'expense' ? 'cat-other-expense' : 'cat-other-income';
}

/** Datos de ejemplo de los últimos tres meses, para probar la app. */
export function sampleState(): AppState {
  const base = initialState();
  const createdAt = new Date().toISOString();
  const now = new Date();

  const accounts: Account[] = [
    { id: 'acc-cash', name: 'Efectivo', type: 'cash', color: '#0F9D58', initialBalance: 12_000, createdAt },
    { id: 'acc-bank', name: 'Cuenta nómina', type: 'bank', color: '#4F46E5', initialBalance: 185_000, createdAt },
    { id: 'acc-card', name: 'Tarjeta de crédito', type: 'card', color: '#E23D3D', initialBalance: -24_000, createdAt },
    { id: 'acc-savings', name: 'Ahorro', type: 'savings', color: '#0284C7', initialBalance: 450_000, createdAt },
  ];

  const plan: { day: number; categoryId: string; amount: number; note: string; accountId: string }[] = [
    { day: 1, categoryId: 'cat-home', amount: 68_000, note: 'Alquiler', accountId: 'acc-bank' },
    { day: 3, categoryId: 'cat-food', amount: 8_450, note: 'Supermercado', accountId: 'acc-card' },
    { day: 5, categoryId: 'cat-bills', amount: 5_230, note: 'Luz y agua', accountId: 'acc-bank' },
    { day: 7, categoryId: 'cat-transport', amount: 4_500, note: 'Gasolina', accountId: 'acc-card' },
    { day: 9, categoryId: 'cat-dining', amount: 3_180, note: 'Cena con amigos', accountId: 'acc-cash' },
    { day: 12, categoryId: 'cat-food', amount: 9_120, note: 'Compra semanal', accountId: 'acc-card' },
    { day: 14, categoryId: 'cat-subscriptions', amount: 1_299, note: 'Streaming', accountId: 'acc-bank' },
    { day: 17, categoryId: 'cat-leisure', amount: 2_400, note: 'Cine', accountId: 'acc-cash' },
    { day: 19, categoryId: 'cat-food', amount: 7_640, note: 'Supermercado', accountId: 'acc-card' },
    { day: 21, categoryId: 'cat-health', amount: 3_900, note: 'Farmacia', accountId: 'acc-bank' },
    { day: 24, categoryId: 'cat-shopping', amount: 5_990, note: 'Ropa', accountId: 'acc-card' },
    { day: 26, categoryId: 'cat-transport', amount: 2_150, note: 'Transporte público', accountId: 'acc-cash' },
  ];

  const transactions: Transaction[] = [];
  for (let back = 2; back >= 0; back -= 1) {
    const month = new Date(now.getFullYear(), now.getMonth() - back, 1);
    const year = month.getFullYear();
    const monthIndex = month.getMonth();
    const lastDay = new Date(year, monthIndex + 1, 0).getDate();
    const maxDay = back === 0 ? now.getDate() : lastDay;
    const iso = (day: number) =>
      `${year}-${String(monthIndex + 1).padStart(2, '0')}-${String(Math.min(day, lastDay)).padStart(2, '0')}`;

    transactions.push({
      id: `sample-income-${back}`,
      type: 'income' as const,
      amount: 210_000,
      date: iso(1),
      accountId: 'acc-bank',
      categoryId: 'cat-salary',
      note: 'Nómina mensual',
      createdAt,
    });

    if (maxDay >= 2) {
      transactions.push({
        id: `sample-transfer-${back}`,
        type: 'transfer' as const,
        amount: 30_000,
        date: iso(2),
        accountId: 'acc-bank',
        toAccountId: 'acc-savings',
        note: 'Ahorro automático',
        createdAt,
      });
    }

    plan
      .filter((item) => item.day <= maxDay)
      .forEach((item, index) => {
        // Pequeña variación determinista entre meses para que los gráficos no sean planos.
        const drift = 1 + ((index % 3) - 1) * 0.08 * (back + 1);
        transactions.push({
          id: `sample-${back}-${index}`,
          type: 'expense' as const,
          amount: Math.round(item.amount * drift),
          date: iso(item.day),
          accountId: item.accountId,
          categoryId: item.categoryId,
          note: item.note,
          createdAt,
        });
      });
  }

  return {
    ...base,
    accounts,
    transactions,
    budgets: [
      { id: 'bud-food', categoryId: 'cat-food', limit: 30_000 },
      { id: 'bud-dining', categoryId: 'cat-dining', limit: 10_000 },
      { id: 'bud-transport', categoryId: 'cat-transport', limit: 8_000 },
      { id: 'bud-leisure', categoryId: 'cat-leisure', limit: 6_000 },
      { id: 'bud-shopping', categoryId: 'cat-shopping', limit: 7_000 },
    ],
    settings: { ...base.settings, onboarded: true },
  };
}
