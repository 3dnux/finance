import { useRouter } from 'expo-router';
import React, { useMemo } from 'react';
import { View } from 'react-native';

import { Screen } from '@/components/Screen';
import { AppText, Button, Card, Divider, IconCircle, ListRow } from '@/components/ui';
import { accountTypeIcon, accountTypeLabel } from '@/lib/defaults';
import { formatMoney } from '@/lib/money';
import { accountBalances } from '@/lib/selectors';
import { useFinance } from '@/store/FinanceProvider';
import { spacing, useTheme } from '@/theme';

export default function AccountsScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const { state } = useFinance();
  const { accounts, transactions, settings } = state;

  const balances = useMemo(() => accountBalances(accounts, transactions), [accounts, transactions]);
  const total = accounts.filter((a) => !a.archived).reduce((sum, a) => sum + (balances[a.id] ?? 0), 0);

  const movementCount = (accountId: string) =>
    transactions.filter((tx) => tx.accountId === accountId || tx.toAccountId === accountId).length;

  return (
    <Screen contentStyle={{ paddingTop: spacing.lg }}>
      <Card style={{ backgroundColor: colors.primary, borderColor: colors.primary, marginBottom: spacing.lg }}>
        <AppText variant="label" color={colors.onPrimary} style={{ opacity: 0.8 }}>
          Suma de tus cuentas
        </AppText>
        <AppText variant="title" color={colors.onPrimary} style={{ marginTop: spacing.xs }}>
          {formatMoney(total, settings.currency)}
        </AppText>
      </Card>

      <Card padded={false} style={{ paddingHorizontal: spacing.lg }}>
        {accounts.map((account, index) => (
          <View key={account.id}>
            {index > 0 ? <Divider /> : null}
            <ListRow
              title={account.name}
              subtitle={`${accountTypeLabel(account.type)} · ${movementCount(account.id)} movimientos`}
              left={<IconCircle name={accountTypeIcon(account.type)} color={account.color} />}
              right={
                <AppText weight="700" color={(balances[account.id] ?? 0) < 0 ? colors.expense : undefined}>
                  {formatMoney(balances[account.id] ?? 0, settings.currency)}
                </AppText>
              }
              chevron
              onPress={() => router.push({ pathname: '/accounts/[id]', params: { id: account.id } })}
            />
          </View>
        ))}
      </Card>

      <Button
        label="Nueva cuenta"
        icon="add"
        fullWidth
        style={{ marginTop: spacing.lg }}
        onPress={() => router.push({ pathname: '/accounts/[id]', params: { id: 'new' } })}
      />

      <AppText variant="caption" muted style={{ marginTop: spacing.lg, textAlign: 'center' }}>
        El saldo se calcula a partir del saldo inicial más todos los movimientos registrados.
      </AppText>
    </Screen>
  );
}
