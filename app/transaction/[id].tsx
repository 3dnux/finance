import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams, useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { Alert, Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DatePicker } from '@/components/DatePicker';
import { SelectField, TextField } from '@/components/Field';
import { Keypad } from '@/components/Keypad';
import { Sheet } from '@/components/Sheet';
import { AppText, Button, IconCircle, ListRow, Segmented } from '@/components/ui';
import { formatRelativeDay, today } from '@/lib/date';
import { accountTypeIcon } from '@/lib/defaults';
import { amountToInput, currencyInfo, formatInputDisplay, parseAmount } from '@/lib/money';
import { useFinance } from '@/store/FinanceProvider';
import { fontSize, radius, spacing, useTheme } from '@/theme';
import type { TransactionType } from '@/types';

export default function TransactionScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { id } = useLocalSearchParams<{ id: string }>();
  const { state, addTransaction, updateTransaction, deleteTransaction } = useFinance();
  const { accounts, categories, settings } = state;

  const existing = useMemo(
    () => (id && id !== 'new' ? state.transactions.find((tx) => tx.id === id) : undefined),
    [id, state.transactions],
  );
  const isEditing = !!existing;

  const [type, setType] = useState<TransactionType>(existing?.type ?? 'expense');
  const [amountInput, setAmountInput] = useState(existing ? amountToInput(existing.amount) : '');
  const [date, setDate] = useState(existing?.date ?? today());
  const [accountId, setAccountId] = useState(existing?.accountId ?? accounts[0]?.id ?? '');
  const [toAccountId, setToAccountId] = useState(existing?.toAccountId ?? accounts[1]?.id ?? '');
  const [categoryId, setCategoryId] = useState(existing?.categoryId);
  const [note, setNote] = useState(existing?.note ?? '');

  const [picker, setPicker] = useState<null | 'category' | 'account' | 'toAccount' | 'note'>(null);
  const [dateOpen, setDateOpen] = useState(false);

  const amount = parseAmount(amountInput);
  const isTransfer = type === 'transfer';
  const kind = type === 'income' ? 'income' : 'expense';
  const availableCategories = categories.filter((c) => c.kind === kind);
  const category = categories.find((c) => c.id === categoryId);
  const account = accounts.find((a) => a.id === accountId);
  const toAccount = accounts.find((a) => a.id === toAccountId);
  const currency = currencyInfo(settings.currency);

  const accent = isTransfer ? colors.transfer : type === 'income' ? colors.income : colors.expense;

  const changeType = (next: TransactionType) => {
    setType(next);
    if (next !== 'transfer') {
      const stillValid = categories.find((c) => c.id === categoryId && c.kind === (next === 'income' ? 'income' : 'expense'));
      if (!stillValid) setCategoryId(undefined);
    }
  };

  const errors: string[] = [];
  if (amount <= 0) errors.push('Introduce un importe mayor que cero.');
  if (!accountId) errors.push('Selecciona una cuenta.');
  if (isTransfer && !toAccountId) errors.push('Selecciona la cuenta de destino.');
  if (isTransfer && accountId === toAccountId) errors.push('El origen y el destino deben ser cuentas distintas.');
  if (!isTransfer && !categoryId) errors.push('Selecciona una categoría.');
  const canSave = errors.length === 0;

  const save = () => {
    if (!canSave) {
      Alert.alert('Faltan datos', errors[0]);
      return;
    }
    const payload = {
      type,
      amount,
      date,
      accountId,
      toAccountId: isTransfer ? toAccountId : undefined,
      categoryId: isTransfer ? undefined : categoryId,
      note: note.trim() || undefined,
    };

    if (existing) updateTransaction({ ...existing, ...payload });
    else addTransaction(payload);
    router.back();
  };

  const confirmDelete = () => {
    if (!existing) return;
    Alert.alert('Eliminar movimiento', '¿Seguro que quieres eliminar este movimiento? No se puede deshacer.', [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Eliminar',
        style: 'destructive',
        onPress: () => {
          deleteTransaction(existing.id);
          router.back();
        },
      },
    ]);
  };

  return (
    <View style={{ flex: 1, backgroundColor: colors.background, paddingTop: insets.top }}>
      {/* Cabecera */}
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingHorizontal: spacing.lg,
          paddingVertical: spacing.md,
        }}
      >
        <Pressable accessibilityRole="button" accessibilityLabel="Cancelar" onPress={() => router.back()} hitSlop={10}>
          <Ionicons name="close" size={26} color={colors.text} />
        </Pressable>
        <AppText variant="subtitle">{isEditing ? 'Editar movimiento' : 'Nuevo movimiento'}</AppText>
        {isEditing ? (
          <Pressable accessibilityRole="button" accessibilityLabel="Eliminar" onPress={confirmDelete} hitSlop={10}>
            <Ionicons name="trash-outline" size={22} color={colors.expense} />
          </Pressable>
        ) : (
          <View style={{ width: 26 }} />
        )}
      </View>

      <ScrollView
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
        contentContainerStyle={{ paddingHorizontal: spacing.lg, paddingBottom: spacing.xl }}
      >
        <Segmented
          value={type}
          onChange={changeType}
          options={[
            { value: 'expense', label: 'Gasto', color: colors.expense },
            { value: 'income', label: 'Ingreso', color: colors.income },
            { value: 'transfer', label: 'Transferencia', color: colors.transfer },
          ]}
        />

        {/* Importe */}
        <View style={{ alignItems: 'center', paddingVertical: spacing.xl }}>
          <AppText variant="label" muted>
            Importe
          </AppText>
          <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: 6, marginTop: spacing.sm }}>
            {currency.prefix ? (
              <AppText style={{ fontSize: fontSize.xl, fontWeight: '700' }} color={accent}>
                {currency.symbol}
              </AppText>
            ) : null}
            <AppText style={{ fontSize: 44, fontWeight: '800', letterSpacing: -1.5 }} color={accent}>
              {formatInputDisplay(amountInput)}
            </AppText>
            {!currency.prefix ? (
              <AppText style={{ fontSize: fontSize.xl, fontWeight: '700' }} color={accent}>
                {currency.symbol}
              </AppText>
            ) : null}
          </View>
        </View>

        <View style={{ gap: spacing.sm }}>
          {isTransfer ? (
            <>
              <SelectField
                label="Desde"
                icon={account ? accountTypeIcon(account.type) : 'wallet-outline'}
                iconColor={account?.color}
                value={account?.name}
                onPress={() => setPicker('account')}
              />
              <SelectField
                label="Hasta"
                icon={toAccount ? accountTypeIcon(toAccount.type) : 'wallet-outline'}
                iconColor={toAccount?.color}
                value={toAccount?.name}
                onPress={() => setPicker('toAccount')}
              />
            </>
          ) : (
            <>
              <SelectField
                label="Categoría"
                icon={category?.icon ?? 'pricetag-outline'}
                iconColor={category?.color}
                value={category?.name}
                onPress={() => setPicker('category')}
              />
              <SelectField
                label="Cuenta"
                icon={account ? accountTypeIcon(account.type) : 'wallet-outline'}
                iconColor={account?.color}
                value={account?.name}
                onPress={() => setPicker('account')}
              />
            </>
          )}
          <SelectField
            label="Fecha"
            icon="calendar-outline"
            value={`${formatRelativeDay(date)}`}
            onPress={() => setDateOpen(true)}
          />
          <SelectField
            label="Nota"
            icon="create-outline"
            value={note.trim() || undefined}
            placeholder="Añadir"
            onPress={() => setPicker('note')}
          />
        </View>

        <View style={{ marginTop: spacing.md }}>
          <Keypad value={amountInput} onChange={setAmountInput} />
        </View>
      </ScrollView>

      {/* Guardar */}
      <View
        style={{
          padding: spacing.lg,
          paddingBottom: insets.bottom + spacing.md,
          borderTopWidth: 1,
          borderTopColor: colors.border,
          backgroundColor: colors.surface,
        }}
      >
        <Button
          label={isEditing ? 'Guardar cambios' : 'Añadir movimiento'}
          icon="checkmark"
          fullWidth
          disabled={!canSave}
          onPress={save}
        />
      </View>

      {/* Selector de categoría */}
      <Sheet visible={picker === 'category'} onClose={() => setPicker(null)} title="Elegir categoría">
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
          {availableCategories.map((item) => {
            const selected = item.id === categoryId;
            return (
              <Pressable
                key={item.id}
                accessibilityRole="button"
                accessibilityState={{ selected }}
                accessibilityLabel={item.name}
                onPress={() => {
                  setCategoryId(item.id);
                  setPicker(null);
                }}
                style={{
                  width: '30%',
                  alignItems: 'center',
                  gap: 6,
                  paddingVertical: spacing.md,
                  borderRadius: radius.md,
                  backgroundColor: selected ? `${item.color}1F` : 'transparent',
                  borderWidth: 1,
                  borderColor: selected ? item.color : 'transparent',
                }}
              >
                <IconCircle name={item.icon} color={item.color} size={44} />
                <AppText variant="caption" numberOfLines={1} style={{ textAlign: 'center' }}>
                  {item.name}
                </AppText>
              </Pressable>
            );
          })}
        </View>
      </Sheet>

      {/* Selector de cuenta (origen o destino) */}
      <Sheet
        visible={picker === 'account' || picker === 'toAccount'}
        onClose={() => setPicker(null)}
        title={picker === 'toAccount' ? 'Cuenta de destino' : 'Elegir cuenta'}
      >
        {accounts
          .filter((item) => !item.archived)
          .map((item) => {
            const selectedId = picker === 'toAccount' ? toAccountId : accountId;
            return (
              <ListRow
                key={item.id}
                title={item.name}
                left={<IconCircle name={accountTypeIcon(item.type)} color={item.color} size={38} />}
                right={item.id === selectedId ? <Ionicons name="checkmark" size={20} color={colors.primary} /> : undefined}
                onPress={() => {
                  if (picker === 'toAccount') setToAccountId(item.id);
                  else setAccountId(item.id);
                  setPicker(null);
                }}
              />
            );
          })}
      </Sheet>

      {/* Nota */}
      <Sheet visible={picker === 'note'} onClose={() => setPicker(null)} title="Nota">
        <TextField
          label="Descripción"
          value={note}
          onChangeText={setNote}
          placeholder="Ej. Compra semanal"
          maxLength={80}
          autoFocus
        />
        <Button label="Listo" fullWidth style={{ marginTop: spacing.lg }} onPress={() => setPicker(null)} />
      </Sheet>

      <DatePicker visible={dateOpen} value={date} onClose={() => setDateOpen(false)} onSelect={setDate} />
    </View>
  );
}
