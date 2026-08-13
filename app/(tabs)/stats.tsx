import { useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { Pressable, useWindowDimensions, View } from 'react-native';

import { DonutChart, GroupedBarChart, LineChart } from '@/components/charts';
import { MonthSwitcher } from '@/components/MonthSwitcher';
import { Screen, ScreenHeader } from '@/components/Screen';
import { AppText, Card, Divider, EmptyState, IconCircle, ProgressBar, Segmented } from '@/components/ui';
import { currentMonthKey, monthLabel } from '@/lib/date';
import { formatMoney, formatPercent } from '@/lib/money';
import { dailyAverageExpense, monthlySeries, monthSummary, totalsByCategory } from '@/lib/selectors';
import { useFinance } from '@/store/FinanceProvider';
import { spacing, useTheme } from '@/theme';

export default function StatsScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const { width } = useWindowDimensions();
  const { state } = useFinance();
  const { transactions, categories, settings } = state;

  const [month, setMonth] = useState(currentMonthKey());
  const [kind, setKind] = useState<'expense' | 'income'>('expense');

  const summary = useMemo(() => monthSummary(transactions, month), [transactions, month]);
  const series = useMemo(() => monthlySeries(transactions, month, 6), [transactions, month]);
  const totals = useMemo(
    () => totalsByCategory(transactions, categories, month, kind),
    [transactions, categories, month, kind],
  );

  const grandTotal = kind === 'expense' ? summary.expense : summary.income;
  const average = useMemo(() => dailyAverageExpense(transactions, month), [transactions, month]);

  const previous = series[series.length - 2];
  const currentPoint = series[series.length - 1];
  const change =
    previous && previous.expense > 0 && currentPoint
      ? (currentPoint.expense - previous.expense) / previous.expense
      : 0;

  const balanceSeries = series.map((point) => point.income - point.expense);
  const monthsWithData = series.filter((p) => p.income > 0 || p.expense > 0).length;

  if (transactions.length === 0) {
    return (
      <Screen>
        <ScreenHeader title="Análisis" subtitle="Tus finanzas en gráficos" />
        <Card>
          <EmptyState
            icon="bar-chart-outline"
            title="Nada que analizar todavía"
            message="Cuando registres movimientos verás aquí la evolución mes a mes y el desglose por categoría."
          />
        </Card>
      </Screen>
    );
  }

  return (
    <Screen contentStyle={{ paddingTop: spacing.sm }}>
      <ScreenHeader title="Análisis" subtitle="Tus finanzas en gráficos" />

      <MonthSwitcher value={month} onChange={setMonth} />

      {/* Evolución de 6 meses */}
      <Card style={{ marginTop: spacing.lg }}>
        <AppText variant="label" muted>
          Ingresos y gastos · últimos 6 meses
        </AppText>
        <View style={{ marginTop: spacing.lg }}>
          <GroupedBarChart
            currency={settings.currency}
            groups={series.map((point) => ({
              label: monthLabel(point.key, { short: true }),
              highlighted: point.key === month,
              values: [
                { value: point.income, color: colors.income },
                { value: point.expense, color: colors.expense },
              ],
            }))}
          />
        </View>
        <View style={{ flexDirection: 'row', gap: spacing.lg, marginTop: spacing.md }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
            <View style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: colors.income }} />
            <AppText variant="caption" muted>
              Ingresos
            </AppText>
          </View>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
            <View style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: colors.expense }} />
            <AppText variant="caption" muted>
              Gastos
            </AppText>
          </View>
        </View>
      </Card>

      {/* Indicadores del mes */}
      <View style={{ flexDirection: 'row', gap: spacing.md, marginTop: spacing.md }}>
        <Card style={{ flex: 1 }}>
          <AppText variant="label" muted>
            Media diaria
          </AppText>
          <AppText variant="subtitle" weight="800" style={{ marginTop: spacing.xs }}>
            {formatMoney(average, settings.currency)}
          </AppText>
          <AppText variant="caption" muted>
            de gasto
          </AppText>
        </Card>
        <Card style={{ flex: 1 }}>
          <AppText variant="label" muted>
            Vs. mes anterior
          </AppText>
          <AppText
            variant="subtitle"
            weight="800"
            color={change > 0 ? colors.expense : change < 0 ? colors.income : undefined}
            style={{ marginTop: spacing.xs }}
          >
            {change === 0 ? '—' : `${change > 0 ? '+' : '−'}${formatPercent(Math.abs(change))}`}
          </AppText>
          <AppText variant="caption" muted>
            en gastos
          </AppText>
        </Card>
      </View>

      {/* Evolución del balance mensual */}
      {monthsWithData > 1 ? (
        <Card style={{ marginTop: spacing.md }}>
          <AppText variant="label" muted>
            Balance mensual (ingresos − gastos)
          </AppText>
          <View style={{ marginTop: spacing.md, alignItems: 'center' }}>
            <LineChart
              points={balanceSeries}
              labels={series.map((p) => monthLabel(p.key, { short: true }))}
              width={width - spacing.lg * 2 - spacing.lg * 2}
              currency={settings.currency}
              color={colors.primary}
            />
          </View>
        </Card>
      ) : null}

      {/* Desglose por categoría */}
      <View style={{ marginTop: spacing.xl }}>
        <Segmented
          value={kind}
          onChange={setKind}
          options={[
            { value: 'expense', label: 'Gastos', color: colors.expense },
            { value: 'income', label: 'Ingresos', color: colors.income },
          ]}
        />

        <Card style={{ marginTop: spacing.md }}>
          {totals.length === 0 ? (
            <EmptyState
              icon="pie-chart-outline"
              title={kind === 'expense' ? 'Sin gastos este mes' : 'Sin ingresos este mes'}
              message="Cambia de mes o registra un movimiento para ver el desglose."
            />
          ) : (
            <>
              <View style={{ alignItems: 'center', marginBottom: spacing.lg }}>
                <DonutChart
                  size={168}
                  slices={totals.slice(0, 8).map((item) => ({
                    key: item.category.id || 'none',
                    label: item.category.name,
                    value: item.total,
                    color: item.category.color,
                  }))}
                  centerValue={formatMoney(grandTotal, settings.currency, { hideDecimals: true })}
                  centerLabel={monthLabel(month)}
                />
              </View>

              {totals.map((item, index) => (
                <View key={item.category.id || `none-${index}`}>
                  {index > 0 ? <Divider /> : null}
                  <View
                    style={{
                      flexDirection: 'row',
                      alignItems: 'center',
                      gap: spacing.md,
                      paddingVertical: spacing.md,
                    }}
                  >
                    <IconCircle name={item.category.icon} color={item.category.color} size={38} />
                    <View style={{ flex: 1, gap: 6 }}>
                      <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                        <AppText weight="600">{item.category.name}</AppText>
                        <AppText weight="700">{formatMoney(item.total, settings.currency)}</AppText>
                      </View>
                      <ProgressBar progress={item.share} color={item.category.color} height={6} />
                      <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                        <AppText variant="caption" muted>
                          {item.count} movimiento{item.count === 1 ? '' : 's'}
                        </AppText>
                        <AppText variant="caption" muted>
                          {formatPercent(item.share, 1)}
                        </AppText>
                      </View>
                    </View>
                  </View>
                </View>
              ))}
            </>
          )}
        </Card>
      </View>

      <Pressable
        accessibilityRole="link"
        onPress={() => router.push('/transactions')}
        style={{ marginTop: spacing.lg, alignSelf: 'center' }}
      >
        <AppText variant="caption" color={colors.primary} weight="700">
          Ver el detalle de movimientos
        </AppText>
      </Pressable>
    </Screen>
  );
}
