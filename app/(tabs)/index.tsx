import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';

import { DonutChart } from '@/components/charts';
import { MonthSwitcher } from '@/components/MonthSwitcher';
import { Screen } from '@/components/Screen';
import { TransactionRow } from '@/components/TransactionRow';
import { AppText, Button, Card, Divider, EmptyState, IconCircle, ProgressBar, SectionHeader } from '@/components/ui';
import { currentMonthKey, monthLabel } from '@/lib/date';
import { accountTypeIcon } from '@/lib/defaults';
import { formatMoney, formatPercent } from '@/lib/money';
import {
  accountBalances,
  budgetProgress,
  monthSummary,
  projectedExpense,
  totalsByCategory,
} from '@/lib/selectors';
import { useFinance } from '@/store/FinanceProvider';
import { radius, spacing, useTheme } from '@/theme';

export default function DashboardScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const { state } = useFinance();
  const [month, setMonth] = useState(currentMonthKey());
  const currency = state.settings.currency;

  const { accounts, transactions, categories, budgets } = state;

  const balances = useMemo(() => accountBalances(accounts, transactions), [accounts, transactions]);
  const total = useMemo(
    () => accounts.filter((a) => !a.archived).reduce((sum, a) => sum + (balances[a.id] ?? 0), 0),
    [accounts, balances],
  );
  const summary = useMemo(() => monthSummary(transactions, month), [transactions, month]);
  const byCategory = useMemo(
    () => totalsByCategory(transactions, categories, month, 'expense'),
    [transactions, categories, month],
  );
  const budgetRows = useMemo(
    () => budgetProgress(budgets, categories, transactions, month),
    [budgets, categories, transactions, month],
  );
  const recent = useMemo(
    () =>
      [...transactions]
        .sort((a, b) => (a.date === b.date ? (a.createdAt < b.createdAt ? 1 : -1) : a.date < b.date ? 1 : -1))
        .slice(0, 6),
    [transactions],
  );
  const projection = useMemo(() => projectedExpense(transactions, month), [transactions, month]);

  const savingsRate = summary.income > 0 ? summary.balance / summary.income : 0;
  const topSlices = byCategory.slice(0, 5).map((item) => ({
    key: item.category.id || 'none',
    label: item.category.name,
    value: item.total,
    color: item.category.color,
  }));
  const restTotal = byCategory.slice(5).reduce((sum, item) => sum + item.total, 0);
  const slices = restTotal
    ? [...topSlices, { key: 'rest', label: 'Otras', value: restTotal, color: colors.textMuted }]
    : topSlices;

  const atRisk = budgetRows.filter((row) => row.status !== 'ok').slice(0, 3);
  const isEmpty = transactions.length === 0;

  return (
    <Screen contentStyle={{ paddingTop: spacing.xl }}>
      {/* Patrimonio total */}
      <Card style={{ backgroundColor: colors.primary, borderColor: colors.primary }}>
        <AppText variant="label" color={colors.onPrimary} style={{ opacity: 0.8 }}>
          Patrimonio total
        </AppText>
        <AppText variant="display" color={colors.onPrimary} style={{ marginTop: spacing.xs }}>
          {formatMoney(total, currency)}
        </AppText>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Ver cuentas"
          onPress={() => router.push('/accounts')}
          style={{ flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: spacing.sm }}
        >
          <AppText variant="caption" color={colors.onPrimary} style={{ opacity: 0.85 }}>
            {accounts.filter((a) => !a.archived).length} cuentas
          </AppText>
          <Ionicons name="chevron-forward" size={13} color={colors.onPrimary} />
        </Pressable>
      </Card>

      {/* Cuentas en horizontal */}
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={{ gap: spacing.sm, paddingVertical: spacing.lg }}
      >
        {accounts
          .filter((a) => !a.archived)
          .map((account) => (
            <Pressable
              key={account.id}
              accessibilityRole="button"
              accessibilityLabel={`Cuenta ${account.name}`}
              onPress={() => router.push({ pathname: '/accounts/[id]', params: { id: account.id } })}
              style={({ pressed }) => ({
                minWidth: 152,
                padding: spacing.md,
                borderRadius: radius.lg,
                backgroundColor: colors.surface,
                borderWidth: 1,
                borderColor: colors.border,
                gap: spacing.sm,
                opacity: pressed ? 0.7 : 1,
              })}
            >
              <IconCircle name={accountTypeIcon(account.type)} color={account.color} size={32} />
              <AppText variant="caption" muted numberOfLines={1}>
                {account.name}
              </AppText>
              <AppText
                weight="700"
                color={(balances[account.id] ?? 0) < 0 ? colors.expense : undefined}
                numberOfLines={1}
              >
                {formatMoney(balances[account.id] ?? 0, currency)}
              </AppText>
            </Pressable>
          ))}
      </ScrollView>

      <MonthSwitcher value={month} onChange={setMonth} />

      {/* Ingresos / gastos del mes */}
      <View style={{ flexDirection: 'row', gap: spacing.md, marginTop: spacing.lg }}>
        <Card style={{ flex: 1 }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
            <IconCircle name="arrow-down-outline" color={colors.income} size={30} />
            <AppText variant="caption" muted>
              Ingresos
            </AppText>
          </View>
          <AppText variant="subtitle" weight="800" color={colors.income} style={{ marginTop: spacing.sm }}>
            {formatMoney(summary.income, currency)}
          </AppText>
        </Card>
        <Card style={{ flex: 1 }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
            <IconCircle name="arrow-up-outline" color={colors.expense} size={30} />
            <AppText variant="caption" muted>
              Gastos
            </AppText>
          </View>
          <AppText variant="subtitle" weight="800" color={colors.expense} style={{ marginTop: spacing.sm }}>
            {formatMoney(summary.expense, currency)}
          </AppText>
        </Card>
      </View>

      {/* Balance del mes */}
      <Card style={{ marginTop: spacing.md }}>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <AppText variant="caption" muted>
            Balance de {monthLabel(month).toLowerCase()}
          </AppText>
          <AppText weight="800" color={summary.balance >= 0 ? colors.income : colors.expense}>
            {formatMoney(summary.balance, currency, { signed: true })}
          </AppText>
        </View>
        <View style={{ marginTop: spacing.md, gap: spacing.sm }}>
          <ProgressBar
            progress={summary.income ? Math.min(1, summary.expense / summary.income) : summary.expense ? 1 : 0}
            color={summary.expense > summary.income ? colors.expense : colors.income}
          />
          <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
            <AppText variant="caption" muted>
              {summary.income > 0 ? `Ahorras el ${formatPercent(Math.max(0, savingsRate))} de tus ingresos` : 'Sin ingresos este mes'}
            </AppText>
          </View>
          {summary.expense > 0 && month === currentMonthKey() ? (
            <AppText variant="caption" muted>
              Al ritmo actual cerrarás el mes en {formatMoney(projection, currency)} de gasto.
            </AppText>
          ) : null}
        </View>
      </Card>

      {isEmpty ? (
        <Card style={{ marginTop: spacing.xl }}>
          <EmptyState
            icon="wallet-outline"
            title="Aún no hay movimientos"
            message="Registra tu primer gasto o ingreso y verás aquí el resumen de tus finanzas."
            action={<Button label="Añadir movimiento" icon="add" onPress={() => router.push('/transaction/new')} />}
          />
        </Card>
      ) : null}

      {/* Gasto por categoría */}
      {byCategory.length > 0 ? (
        <View style={{ marginTop: spacing.xl }}>
          <SectionHeader title="Gasto por categoría" action="Ver análisis" onAction={() => router.push('/stats')} />
          <Card>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.lg }}>
              <DonutChart
                slices={slices}
                size={132}
                thickness={20}
                centerValue={formatMoney(summary.expense, currency, { hideDecimals: true })}
                centerLabel="gastado"
              />
              <View style={{ flex: 1, gap: spacing.sm }}>
                {slices.map((slice) => (
                  <View key={slice.key} style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
                    <View style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: slice.color }} />
                    <AppText variant="caption" numberOfLines={1} style={{ flex: 1 }}>
                      {slice.label}
                    </AppText>
                    <AppText variant="caption" muted weight="700">
                      {formatPercent(summary.expense ? slice.value / summary.expense : 0)}
                    </AppText>
                  </View>
                ))}
              </View>
            </View>
          </Card>
        </View>
      ) : null}

      {/* Presupuestos en riesgo */}
      {atRisk.length > 0 ? (
        <View style={{ marginTop: spacing.xl }}>
          <SectionHeader title="Presupuestos a vigilar" action="Ver todos" onAction={() => router.push('/budgets')} />
          <Card style={{ gap: spacing.lg }}>
            {atRisk.map((row) => (
              <View key={row.budget.id} style={{ gap: spacing.sm }}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                  <AppText weight="600">{row.category?.name ?? 'Categoría'}</AppText>
                  <AppText variant="caption" color={row.status === 'over' ? colors.expense : colors.warning} weight="700">
                    {row.status === 'over'
                      ? `Superado en ${formatMoney(-row.remaining, currency)}`
                      : `Quedan ${formatMoney(row.remaining, currency)}`}
                  </AppText>
                </View>
                <ProgressBar
                  progress={row.progress}
                  color={row.status === 'over' ? colors.expense : colors.warning}
                  markerAt={row.limit ? row.pace / row.limit : 0}
                />
              </View>
            ))}
          </Card>
        </View>
      ) : null}

      {/* Últimos movimientos */}
      {recent.length > 0 ? (
        <View style={{ marginTop: spacing.xl }}>
          <SectionHeader title="Últimos movimientos" action="Ver todos" onAction={() => router.push('/transactions')} />
          <Card padded={false} style={{ paddingHorizontal: spacing.lg }}>
            {recent.map((tx, index) => (
              <View key={tx.id}>
                {index > 0 ? <Divider /> : null}
                <TransactionRow
                  transaction={tx}
                  currency={currency}
                  category={categories.find((c) => c.id === tx.categoryId)}
                  account={accounts.find((a) => a.id === tx.accountId)}
                  toAccount={accounts.find((a) => a.id === tx.toAccountId)}
                  onPress={() => router.push({ pathname: '/transaction/[id]', params: { id: tx.id } })}
                />
              </View>
            ))}
          </Card>
        </View>
      ) : null}
    </Screen>
  );
}
