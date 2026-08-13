import AsyncStorage from '@react-native-async-storage/async-storage';

import { STATE_VERSION, initialState } from '@/lib/defaults';
import type { AppState } from '@/types';

export const STORAGE_KEY = '@finanzas/state';

/**
 * Rellena los huecos de un estado leído de disco (o importado) para que la app
 * nunca arranque con campos indefinidos aunque el JSON venga de otra versión.
 */
export function migrate(raw: unknown): AppState {
  const base = initialState();
  if (!raw || typeof raw !== 'object') return base;
  const data = raw as Partial<AppState>;

  return {
    version: STATE_VERSION,
    accounts: Array.isArray(data.accounts) && data.accounts.length ? data.accounts : base.accounts,
    categories: Array.isArray(data.categories) && data.categories.length ? data.categories : base.categories,
    transactions: Array.isArray(data.transactions) ? data.transactions : [],
    budgets: Array.isArray(data.budgets) ? data.budgets : [],
    settings: { ...base.settings, ...(data.settings ?? {}) },
  };
}

export async function loadState(): Promise<AppState> {
  try {
    const raw = await AsyncStorage.getItem(STORAGE_KEY);
    if (!raw) return initialState();
    return migrate(JSON.parse(raw));
  } catch {
    // Un JSON corrupto no debe impedir abrir la app: empezamos limpios.
    return initialState();
  }
}

export async function saveState(state: AppState): Promise<void> {
  await AsyncStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

export async function clearState(): Promise<void> {
  await AsyncStorage.removeItem(STORAGE_KEY);
}

/** Serializa el estado para compartirlo como copia de seguridad. */
export function exportState(state: AppState): string {
  return JSON.stringify({ ...state, exportedAt: new Date().toISOString() }, null, 2);
}

/** Lee una copia de seguridad. Lanza un error con mensaje legible si no es válida. */
export function parseImport(text: string): AppState {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    throw new Error('El texto no es un JSON válido.');
  }
  const data = parsed as Partial<AppState>;
  if (!data || typeof data !== 'object' || !Array.isArray(data.transactions) || !Array.isArray(data.accounts)) {
    throw new Error('El archivo no parece una copia de seguridad de Finanzas.');
  }
  return migrate(parsed);
}
