import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams, useNavigation, useRouter } from 'expo-router';
import React, { useLayoutEffect, useMemo, useState } from 'react';
import { Alert, Pressable, View } from 'react-native';

import { ColorPicker, TextField } from '@/components/Field';
import { Screen } from '@/components/Screen';
import { AppText, Button, Card, IconCircle } from '@/components/ui';
import { ACCOUNT_TYPES } from '@/lib/defaults';
import { amountToInput, formatMoney, parseAmount } from '@/lib/money';
import { accountBalance } from '@/lib/selectors';
import { useFinance } from '@/store/FinanceProvider';
import { radius, spacing, swatches, useTheme } from '@/theme';
import type { AccountType } from '@/types';

export default function AccountFormScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const navigation = useNavigation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const { state, addAccount, updateAccount, deleteAccount } = useFinance();

  const existing = useMemo(
    () => (id && id !== 'new' ? state.accounts.find((a) => a.id === id) : undefined),
    [id, state.accounts],
  );

  const [name, setName] = useState(existing?.name ?? '');
  const [type, setType] = useState<AccountType>(existing?.type ?? 'bank');
  const [color, setColor] = useState(existing?.color ?? swatches[0]);
  const [initial, setInitial] = useState(existing ? amountToInput(existing.initialBalance) : '');
  const [negative, setNegative] = useState((existing?.initialBalance ?? 0) < 0);

  useLayoutEffect(() => {
    navigation.setOptions({ title: existing ? 'Editar cuenta' : 'Nueva cuenta' });
  }, [navigation, existing]);

  const balance = existing ? accountBalance(existing, state.transactions) : 0;
  const canSave = name.trim().length > 0;

  const save = () => {
    if (!canSave) {
      Alert.alert('Falta el nombre', 'Escribe un nombre para la cuenta.');
      return;
    }
    const magnitude = parseAmount(initial);
    const initialBalance = negative ? -magnitude : magnitude;

    if (existing) updateAccount({ ...existing, name: name.trim(), type, color, initialBalance });
    else addAccount({ name: name.trim(), type, color, initialBalance });
    router.back();
  };

  const confirmDelete = () => {
    if (!existing) return;
    if (state.accounts.length <= 1) {
      Alert.alert('No se puede eliminar', 'Debe quedar al menos una cuenta.');
      return;
    }
    const linked = state.transactions.filter((tx) => tx.accountId === existing.id).length;
    Alert.alert(
      'Eliminar cuenta',
      linked
        ? `Se eliminarán también los ${linked} movimientos de esta cuenta. Esta acción no se puede deshacer.`
        : '¿Seguro que quieres eliminar esta cuenta?',
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Eliminar',
          style: 'destructive',
          onPress: () => {
            deleteAccount(existing.id);
            router.back();
          },
        },
      ],
    );
  };

  return (
    <Screen contentStyle={{ paddingTop: spacing.lg, gap: spacing.lg }}>
      {existing ? (
        <Card style={{ alignItems: 'center', gap: spacing.sm }}>
          <IconCircle name={ACCOUNT_TYPES.find((t) => t.value === type)?.icon ?? 'wallet-outline'} color={color} size={56} />
          <AppText variant="title" color={balance < 0 ? colors.expense : undefined}>
            {formatMoney(balance, state.settings.currency)}
          </AppText>
          <AppText variant="caption" muted>
            Saldo actual
          </AppText>
        </Card>
      ) : null}

      <TextField label="Nombre" value={name} onChangeText={setName} placeholder="Ej. Cuenta nómina" maxLength={40} />

      <View style={{ gap: spacing.sm }}>
        <AppText variant="label" muted>
          Tipo de cuenta
        </AppText>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {ACCOUNT_TYPES.map((option) => {
            const selected = option.value === type;
            return (
              <Pressable
                key={option.value}
                accessibilityRole="button"
                accessibilityState={{ selected }}
                onPress={() => setType(option.value)}
                style={{
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: 6,
                  paddingHorizontal: spacing.md,
                  paddingVertical: spacing.sm,
                  borderRadius: radius.pill,
                  borderWidth: 1,
                  borderColor: selected ? color : colors.border,
                  backgroundColor: selected ? `${color}1F` : colors.surface,
                }}
              >
                <Ionicons name={option.icon as never} size={16} color={selected ? color : colors.textMuted} />
                <AppText variant="caption" weight={selected ? '700' : '500'} color={selected ? color : colors.textMuted}>
                  {option.label}
                </AppText>
              </Pressable>
            );
          })}
        </View>
      </View>

      <View style={{ gap: spacing.sm }}>
        <AppText variant="label" muted>
          Color
        </AppText>
        <ColorPicker colors={swatches} value={color} onChange={setColor} />
      </View>

      <View style={{ gap: spacing.sm }}>
        <TextField
          label="Saldo inicial"
          value={initial}
          onChangeText={setInitial}
          placeholder="0"
          keyboardType="decimal-pad"
          helper="El punto de partida antes de registrar movimientos."
        />
        <Pressable
          accessibilityRole="checkbox"
          accessibilityState={{ checked: negative }}
          onPress={() => setNegative((v) => !v)}
          style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.xs }}
        >
          <Ionicons
            name={negative ? 'checkbox' : 'square-outline'}
            size={20}
            color={negative ? colors.expense : colors.textMuted}
          />
          <AppText variant="caption" muted>
            Es un saldo negativo (deuda de tarjeta o préstamo)
          </AppText>
        </Pressable>
      </View>

      <Button label={existing ? 'Guardar cambios' : 'Crear cuenta'} icon="checkmark" fullWidth onPress={save} />

      {existing ? (
        <Button label="Eliminar cuenta" variant="danger" icon="trash-outline" fullWidth onPress={confirmDelete} />
      ) : null}
    </Screen>
  );
}
