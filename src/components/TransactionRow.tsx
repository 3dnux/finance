import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { Pressable, View } from 'react-native';

import { formatMoney } from '@/lib/money';
import { spacing, useTheme } from '@/theme';
import type { Account, Category, Transaction } from '@/types';

import { AppText, IconCircle } from './ui';

export function TransactionRow({
  transaction,
  category,
  account,
  toAccount,
  currency,
  onPress,
}: {
  transaction: Transaction;
  category?: Category;
  account?: Account;
  toAccount?: Account;
  currency: string;
  onPress?: () => void;
}) {
  const { colors } = useTheme();
  const isTransfer = transaction.type === 'transfer';
  const isIncome = transaction.type === 'income';

  const tint = isTransfer ? colors.transfer : isIncome ? colors.income : category?.color ?? colors.expense;
  const icon = isTransfer ? 'swap-horizontal-outline' : category?.icon ?? 'ellipsis-horizontal-outline';

  const note = transaction.note?.trim();
  const title = isTransfer ? 'Transferencia' : note || category?.name || 'Sin categoría';
  // Si la nota ya ocupa el título, el subtítulo añade la categoría; si no, no la repetimos.
  const subtitle = isTransfer
    ? `${account?.name ?? 'Cuenta'} → ${toAccount?.name ?? 'Cuenta'}`
    : [note ? category?.name : undefined, account?.name].filter(Boolean).join(' · ');

  const amountColor = isTransfer ? colors.textMuted : isIncome ? colors.income : colors.text;
  // Los gastos se guardan en positivo: aquí se muestran en negativo para el usuario.
  const signedAmount = transaction.type === 'expense' ? -transaction.amount : transaction.amount;
  const amountText = formatMoney(signedAmount, currency, { signed: isIncome });

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${title}, ${amountText}`}
      onPress={onPress}
      style={({ pressed }) => ({
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.md,
        paddingVertical: spacing.md,
        opacity: pressed ? 0.6 : 1,
      })}
    >
      <IconCircle name={icon} color={tint} />
      <View style={{ flex: 1 }}>
        <AppText weight="600" numberOfLines={1}>
          {title}
        </AppText>
        {subtitle ? (
          <AppText variant="caption" muted numberOfLines={1}>
            {subtitle}
          </AppText>
        ) : null}
      </View>
      <View style={{ alignItems: 'flex-end' }}>
        <AppText weight="700" color={amountColor}>
          {amountText}
        </AppText>
        {isTransfer ? <Ionicons name="swap-horizontal" size={13} color={colors.textMuted} /> : null}
      </View>
    </Pressable>
  );
}
