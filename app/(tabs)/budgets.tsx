import { useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { View } from 'react-native';

import { MonthSwitcher } from '@/components/MonthSwitcher';
import { Screen, ScreenHeader } from '@/components/Screen';
import { AppText, Button, Card, EmptyState, IconCircle, ProgressBar } from '@/components/ui';
import { currentMonthKey, daysInMonth, elapsedDays } from '@/lib/date';
import { formatMoney, formatPercent } from '@/lib/money';
import { budgetProgress } from '@/lib/selectors';
import { useFinance } from '@/store/FinanceProvider';
import { spacing, useTheme } from '@/theme';

export default function BudgetsScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const { state } = useFinance();
  const { budgets, categories, transactions, settings } = state;
  const [month, setMonth] = useState(currentMonthKey());

  const rows = useMemo(
    () => budgetProgress(budgets, categories, transactions, month),
    [budgets, categories, transactions, month],
  );

  const totals = useMemo(
    () =>
      rows.reduce(
        (acc, row) => ({ limit: acc.limit + row.limit, spent: acc.spent + row.spent }),
        { limit: 0, spent: 0 },
      ),
    [rows],
  );

  const remaining = totals.limit - totals.spent;
  const daysLeft = Math.max(0, daysInMonth(month) - elapsedDays(month));
  const perDay = daysLeft > 0 ? Math.round(remaining / daysLeft) : remaining;

  const statusColor = (status: 'ok' | 'warning' | 'over') =>
    status === 'over' ? colors.expense : status === 'warning' ? colors.warning : colors.income;

  return (
    <Screen contentStyle={{ paddingTop: spacing.sm }}>
      <ScreenHeader
        title="Presupuestos"
        subtitle="Límites mensuales por categoría"
        actionIcon="add"
        actionLabel="Nuevo presupuesto"
        onAction={() => router.push('/budgets/edit')}
      />

      <MonthSwitcher value={month} onChange={setMonth} />

      {rows.length === 0 ? (
        <Card style={{ marginTop: spacing.xl }}>
          <EmptyState
            icon="speedometer-outline"
            title="Sin presupuestos"
            message="Define un límite mensual por categoría y la app te avisará cuando te acerques al tope."
            action={<Button label="Crear presupuesto" icon="add" onPress={() => router.push('/budgets/edit')} />}
          />
        </Card>
      ) : (
        <>
          {/* Resumen global */}
          <Card style={{ marginTop: spacing.lg }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-end' }}>
              <View>
                <AppText variant="label" muted>
                  Disponible
                </AppText>
                <AppText variant="title" color={remaining >= 0 ? colors.text : colors.expense}>
                  {formatMoney(remaining, settings.currency)}
                </AppText>
              </View>
              <View style={{ alignItems: 'flex-end' }}>
                <AppText variant="caption" muted>
                  de {formatMoney(totals.limit, settings.currency)}
                </AppText>
                <AppText variant="caption" weight="700" color={totals.spent > totals.limit ? colors.expense : colors.income}>
                  {formatPercent(totals.limit ? totals.spent / totals.limit : 0)} usado
                </AppText>
              </View>
            </View>
            <View style={{ marginTop: spacing.md }}>
              <ProgressBar
                progress={totals.limit ? totals.spent / totals.limit : 0}
                color={totals.spent > totals.limit ? colors.expense : colors.primary}
                height={10}
                markerAt={elapsedDays(month) / daysInMonth(month)}
              />
            </View>
            {daysLeft > 0 && month === currentMonthKey() ? (
              <AppText variant="caption" muted style={{ marginTop: spacing.md }}>
                Quedan {daysLeft} día{daysLeft === 1 ? '' : 's'}: puedes gastar{' '}
                {formatMoney(Math.max(0, perDay), settings.currency)} al día sin pasarte.
              </AppText>
            ) : null}
          </Card>

          {/* Presupuesto por categoría */}
          <View style={{ gap: spacing.md, marginTop: spacing.lg }}>
            {rows.map((row) => (
              <Card key={row.budget.id}>
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
                  <IconCircle
                    name={row.category?.icon ?? 'pricetag-outline'}
                    color={row.category?.color ?? colors.textMuted}
                  />
                  <View style={{ flex: 1 }}>
                    <AppText weight="700">{row.category?.name ?? 'Categoría eliminada'}</AppText>
                    <AppText variant="caption" muted>
                      {formatMoney(row.spent, settings.currency)} de {formatMoney(row.limit, settings.currency)}
                    </AppText>
                  </View>
                  <Button
                    label="Editar"
                    variant="ghost"
                    onPress={() =>
                      router.push({ pathname: '/budgets/edit', params: { categoryId: row.budget.categoryId } })
                    }
                  />
                </View>

                <View style={{ marginTop: spacing.md, gap: spacing.sm }}>
                  <ProgressBar
                    progress={row.progress}
                    color={statusColor(row.status)}
                    markerAt={row.limit ? row.pace / row.limit : 0}
                  />
                  <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                    <AppText variant="caption" color={statusColor(row.status)} weight="700">
                      {row.status === 'over'
                        ? `Superado en ${formatMoney(-row.remaining, settings.currency)}`
                        : row.status === 'warning'
                          ? 'Vas por encima del ritmo'
                          : `Quedan ${formatMoney(row.remaining, settings.currency)}`}
                    </AppText>
                    <AppText variant="caption" muted>
                      {formatPercent(row.progress)}
                    </AppText>
                  </View>
                </View>
              </Card>
            ))}
          </View>

          <Button
            label="Nuevo presupuesto"
            icon="add"
            variant="secondary"
            fullWidth
            style={{ marginTop: spacing.lg }}
            onPress={() => router.push('/budgets/edit')}
          />
        </>
      )}
    </Screen>
  );
}
