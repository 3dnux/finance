import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { ScrollView, SectionList, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { MonthSwitcher } from '@/components/MonthSwitcher';
import { ScreenHeader } from '@/components/Screen';
import { Sheet } from '@/components/Sheet';
import { TransactionRow } from '@/components/TransactionRow';
import { AppText, Card, Chip, Divider, EmptyState, IconCircle, ListRow } from '@/components/ui';
import { currentMonthKey, formatRelativeDay } from '@/lib/date';
import { accountTypeIcon } from '@/lib/defaults';
import { formatMoney } from '@/lib/money';
import { filterTransactions, groupByDay, monthSummary } from '@/lib/selectors';
import { useFinance } from '@/store/FinanceProvider';
import { radius, spacing, useTheme } from '@/theme';
import type { TransactionType } from '@/types';

const TYPE_FILTERS: { value: TransactionType; label: string; icon: string }[] = [
  { value: 'expense', label: 'Gastos', icon: 'arrow-up-outline' },
  { value: 'income', label: 'Ingresos', icon: 'arrow-down-outline' },
  { value: 'transfer', label: 'Transferencias', icon: 'swap-horizontal-outline' },
];

export default function TransactionsScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { state } = useFinance();
  const { accounts, categories, transactions, settings } = state;

  const [month, setMonth] = useState(currentMonthKey());
  const [search, setSearch] = useState('');
  const [types, setTypes] = useState<TransactionType[]>([]);
  const [accountId, setAccountId] = useState<string | undefined>();
  const [categoryId, setCategoryId] = useState<string | undefined>();
  const [filtersOpen, setFiltersOpen] = useState(false);

  const filtered = useMemo(
    () => filterTransactions(transactions, categories, { search, types, accountId, categoryId, monthKey: month }),
    [transactions, categories, search, types, accountId, categoryId, month],
  );

  const sections = useMemo(
    () => groupByDay(filtered).map((group) => ({ title: group.date, total: group.total, data: group.items })),
    [filtered],
  );

  const summary = useMemo(() => monthSummary(filtered, month), [filtered, month]);
  const activeFilters = (accountId ? 1 : 0) + (categoryId ? 1 : 0) + (types.length ? 1 : 0);

  const toggleType = (type: TransactionType) =>
    setTypes((prev) => (prev.includes(type) ? prev.filter((t) => t !== type) : [...prev, type]));

  const clearFilters = () => {
    setTypes([]);
    setAccountId(undefined);
    setCategoryId(undefined);
  };

  return (
    <View style={{ flex: 1, backgroundColor: colors.background }}>
      <View style={{ paddingHorizontal: spacing.lg, paddingTop: spacing.sm, gap: spacing.md }}>
        <ScreenHeader title="Movimientos" subtitle="Todo lo que entra y sale" />
        <MonthSwitcher value={month} onChange={setMonth} />

        <View style={{ flexDirection: 'row', gap: spacing.sm, alignItems: 'center' }}>
          <View
            style={{
              flex: 1,
              flexDirection: 'row',
              alignItems: 'center',
              gap: spacing.sm,
              backgroundColor: colors.surface,
              borderRadius: radius.md,
              borderWidth: 1,
              borderColor: colors.border,
              paddingHorizontal: spacing.md,
            }}
          >
            <Ionicons name="search" size={18} color={colors.textMuted} />
            <TextInput
              accessibilityLabel="Buscar movimientos"
              value={search}
              onChangeText={setSearch}
              placeholder="Buscar por nota o categoría"
              placeholderTextColor={colors.textMuted}
              style={{ flex: 1, paddingVertical: 11, color: colors.text, fontSize: 15 }}
            />
            {search ? (
              <Ionicons name="close-circle" size={18} color={colors.textMuted} onPress={() => setSearch('')} />
            ) : null}
          </View>
          <Chip
            label={activeFilters ? `Filtros (${activeFilters})` : 'Filtros'}
            icon="options-outline"
            active={activeFilters > 0}
            onPress={() => setFiltersOpen(true)}
          />
        </View>

        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm }}>
          {TYPE_FILTERS.map((filter) => (
            <Chip
              key={filter.value}
              label={filter.label}
              icon={filter.icon}
              active={types.includes(filter.value)}
              onPress={() => toggleType(filter.value)}
            />
          ))}
          {activeFilters > 0 ? <Chip label="Limpiar" icon="close" onPress={clearFilters} color={colors.expense} /> : null}
        </ScrollView>

        <View style={{ flexDirection: 'row', justifyContent: 'space-between', paddingHorizontal: spacing.xs }}>
          <AppText variant="caption" muted>
            {filtered.length} movimiento{filtered.length === 1 ? '' : 's'}
          </AppText>
          <AppText variant="caption" weight="700" color={summary.balance >= 0 ? colors.income : colors.expense}>
            {formatMoney(summary.balance, settings.currency, { signed: true })}
          </AppText>
        </View>
      </View>

      <SectionList
        sections={sections}
        keyExtractor={(item) => item.id}
        contentContainerStyle={{
          paddingHorizontal: spacing.lg,
          paddingTop: spacing.md,
          paddingBottom: insets.bottom + 120,
        }}
        stickySectionHeadersEnabled={false}
        showsVerticalScrollIndicator={false}
        ItemSeparatorComponent={() => <Divider />}
        renderSectionHeader={({ section }) => (
          <View
            style={{
              flexDirection: 'row',
              justifyContent: 'space-between',
              paddingTop: spacing.lg,
              paddingBottom: spacing.xs,
            }}
          >
            <AppText variant="label" muted>
              {formatRelativeDay(section.title)}
            </AppText>
            <AppText variant="caption" muted weight="700">
              {formatMoney(section.total, settings.currency, { signed: true })}
            </AppText>
          </View>
        )}
        renderItem={({ item }) => (
          <TransactionRow
            transaction={item}
            currency={settings.currency}
            category={categories.find((c) => c.id === item.categoryId)}
            account={accounts.find((a) => a.id === item.accountId)}
            toAccount={accounts.find((a) => a.id === item.toAccountId)}
            onPress={() => router.push({ pathname: '/transaction/[id]', params: { id: item.id } })}
          />
        )}
        ListEmptyComponent={
          <Card style={{ marginTop: spacing.xl }}>
            <EmptyState
              icon="receipt-outline"
              title="Sin movimientos"
              message={
                search || activeFilters
                  ? 'Prueba a cambiar los filtros o el mes seleccionado.'
                  : 'Pulsa el botón + para registrar tu primer movimiento de este mes.'
              }
            />
          </Card>
        }
      />

      <Sheet visible={filtersOpen} onClose={() => setFiltersOpen(false)} title="Filtrar movimientos">
        <AppText variant="label" muted style={{ marginBottom: spacing.sm }}>
          Cuenta
        </AppText>
        <ListRow
          title="Todas las cuentas"
          left={<IconCircle name="albums-outline" color={colors.primary} size={34} />}
          right={accountId ? undefined : <Ionicons name="checkmark" size={20} color={colors.primary} />}
          onPress={() => setAccountId(undefined)}
        />
        {accounts.map((account) => (
          <ListRow
            key={account.id}
            title={account.name}
            left={<IconCircle name={accountTypeIcon(account.type)} color={account.color} size={34} />}
            right={
              accountId === account.id ? <Ionicons name="checkmark" size={20} color={colors.primary} /> : undefined
            }
            onPress={() => setAccountId(account.id)}
          />
        ))}

        <AppText variant="label" muted style={{ marginTop: spacing.lg, marginBottom: spacing.sm }}>
          Categoría
        </AppText>
        <ListRow
          title="Todas las categorías"
          left={<IconCircle name="apps-outline" color={colors.primary} size={34} />}
          right={categoryId ? undefined : <Ionicons name="checkmark" size={20} color={colors.primary} />}
          onPress={() => setCategoryId(undefined)}
        />
        {categories.map((category) => (
          <ListRow
            key={category.id}
            title={category.name}
            subtitle={category.kind === 'expense' ? 'Gasto' : 'Ingreso'}
            left={<IconCircle name={category.icon} color={category.color} size={34} />}
            right={
              categoryId === category.id ? <Ionicons name="checkmark" size={20} color={colors.primary} /> : undefined
            }
            onPress={() => setCategoryId(category.id)}
          />
        ))}
      </Sheet>
    </View>
  );
}
