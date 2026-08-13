import React, { createContext, useCallback, useContext, useEffect, useMemo, useReducer, useRef, useState } from 'react';

import { createId } from '@/lib/id';
import { initialState, sampleState } from '@/lib/defaults';
import type { Account, AppState, Budget, Category, Settings, Transaction } from '@/types';

import { clearState, loadState, saveState } from './persistence';
import { reducer, type Action } from './reducer';

interface FinanceContextValue {
  state: AppState;
  ready: boolean;
  dispatch: React.Dispatch<Action>;
  addTransaction: (input: Omit<Transaction, 'id' | 'createdAt'>) => Transaction;
  updateTransaction: (transaction: Transaction) => void;
  deleteTransaction: (id: string) => void;
  addAccount: (input: Omit<Account, 'id' | 'createdAt'>) => Account;
  updateAccount: (account: Account) => void;
  deleteAccount: (id: string) => void;
  addCategory: (input: Omit<Category, 'id'>) => Category;
  updateCategory: (category: Category) => void;
  deleteCategory: (id: string) => void;
  setBudget: (categoryId: string, limit: number) => void;
  deleteBudget: (id: string) => void;
  updateSettings: (settings: Partial<Settings>) => void;
  replaceAll: (state: AppState) => void;
  loadSampleData: () => void;
  resetAll: () => Promise<void>;
}

const FinanceContext = createContext<FinanceContextValue | null>(null);

export function FinanceProvider({ children }: { children: React.ReactNode }) {
  const [state, dispatch] = useReducer(reducer, undefined, initialState);
  const [ready, setReady] = useState(false);
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    let active = true;
    loadState().then((loaded) => {
      if (!active) return;
      dispatch({ type: 'hydrate', state: loaded });
      setReady(true);
    });
    return () => {
      active = false;
    };
  }, []);

  // Guardado diferido: agrupa ráfagas de cambios en una sola escritura.
  useEffect(() => {
    if (!ready) return;
    if (saveTimer.current) clearTimeout(saveTimer.current);
    saveTimer.current = setTimeout(() => {
      void saveState(state);
    }, 250);
    return () => {
      if (saveTimer.current) clearTimeout(saveTimer.current);
    };
  }, [state, ready]);

  const addTransaction = useCallback((input: Omit<Transaction, 'id' | 'createdAt'>) => {
    const transaction: Transaction = { ...input, id: createId('tx-'), createdAt: new Date().toISOString() };
    dispatch({ type: 'addTransaction', transaction });
    return transaction;
  }, []);

  const addAccount = useCallback((input: Omit<Account, 'id' | 'createdAt'>) => {
    const account: Account = { ...input, id: createId('acc-'), createdAt: new Date().toISOString() };
    dispatch({ type: 'addAccount', account });
    return account;
  }, []);

  const addCategory = useCallback((input: Omit<Category, 'id'>) => {
    const category: Category = { ...input, id: createId('cat-'), custom: true };
    dispatch({ type: 'addCategory', category });
    return category;
  }, []);

  const setBudget = useCallback((categoryId: string, limit: number) => {
    const budget: Budget = { id: createId('bud-'), categoryId, limit };
    dispatch({ type: 'upsertBudget', budget });
  }, []);

  const resetAll = useCallback(async () => {
    dispatch({ type: 'reset' });
    await clearState();
  }, []);

  const value = useMemo<FinanceContextValue>(
    () => ({
      state,
      ready,
      dispatch,
      addTransaction,
      updateTransaction: (transaction) => dispatch({ type: 'updateTransaction', transaction }),
      deleteTransaction: (id) => dispatch({ type: 'deleteTransaction', id }),
      addAccount,
      updateAccount: (account) => dispatch({ type: 'updateAccount', account }),
      deleteAccount: (id) => dispatch({ type: 'deleteAccount', id }),
      addCategory,
      updateCategory: (category) => dispatch({ type: 'updateCategory', category }),
      deleteCategory: (id) => dispatch({ type: 'deleteCategory', id }),
      setBudget,
      deleteBudget: (id) => dispatch({ type: 'deleteBudget', id }),
      updateSettings: (settings) => dispatch({ type: 'updateSettings', settings }),
      replaceAll: (next) => dispatch({ type: 'replaceAll', state: next }),
      loadSampleData: () => dispatch({ type: 'replaceAll', state: sampleState() }),
      resetAll,
    }),
    [state, ready, addTransaction, addAccount, addCategory, setBudget, resetAll],
  );

  return <FinanceContext.Provider value={value}>{children}</FinanceContext.Provider>;
}

export function useFinance() {
  const context = useContext(FinanceContext);
  if (!context) throw new Error('useFinance debe usarse dentro de <FinanceProvider>');
  return context;
}

/** Atajo para la moneda configurada. */
export function useCurrency() {
  return useFinance().state.settings.currency;
}
