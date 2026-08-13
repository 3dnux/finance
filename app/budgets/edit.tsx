import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams, useNavigation, useRouter } from 'expo-router';
import React, { useLayoutEffect, useMemo, useState } from 'react';
import { Alert, Pressable, View } from 'react-native';

import { TextField } from '@/components/Field';
import { Screen } from '@/components/Screen';
import { AppText, Button, Card, IconCircle } from '@/components/ui';
import { currentMonthKey } from '@/lib/date';
import { amountToInput, formatMoney, parseAmount } from '@/lib/money';
import { totalsByCategory } from '@/lib/selectors';
import { useFinance } from '@/store/FinanceProvider';
import { radius, spacing, useTheme } from '@/theme';

export default function BudgetEditScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const navigation = useNavigation();
  const params = useLocalSearchParams<{ categoryId?: string }>();
  const { state, setBudget, deleteBudget } = useFinance();
  const { categories, budgets, transactions, settings } = state;

  const existing = useMemo(
    () => (params.categoryId ? budgets.find((b) => b.categoryId === params.categoryId) : undefined),
    [params.categoryId, budgets],
  );

  const [categoryId, setCategoryId] = useState(params.categoryId ?? '');
  const [limit, setLimit] = useState(existing ? amountToInput(existing.limit) : '');

  useLayoutEffect(() => {
    navigation.setOptions({ title: existing ? 'Editar presupuesto' : 'Nuevo presupuesto' });
  }, [navigation, existing]);

  const expenseCategories = categories.filter((c) => c.kind === 'expense');
  const budgetedIds = new Set(budgets.map((b) => b.categoryId));
  const selected = categories.find((c) => c.id === categoryId);

  // Gasto medio de la categoría en el mes actual, como sugerencia de límite.
  const currentSpend = useMemo(() => {
    const totals = totalsByCategory(transactions, categories, currentMonthKey(), 'expense');
    return totals.find((t) => t.category.id === categoryId)?.total ?? 0;
  }, [transactions, categories, categoryId]);

  const save = () => {
    if (!categoryId) {
      Alert.alert('Falta la categoría', 'Elige la categoría que quieres limitar.');
      return;
    }
    const amount = parseAmount(limit);
    if (amount <= 0) {
      Alert.alert('Importe no válido', 'Introduce un límite mayor que cero.');
      return;
    }
    setBudget(categoryId, amount);
    router.back();
  };

  const confirmDelete = () => {
    if (!existing) return;
    Alert.alert('Eliminar presupuesto', '¿Quieres dejar de controlar el gasto de esta categoría?', [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Eliminar',
        style: 'destructive',
        onPress: () => {
          deleteBudget(existing.id);
          router.back();
        },
      },
    ]);
  };

  return (
    <Screen contentStyle={{ paddingTop: spacing.lg, gap: spacing.lg }}>
      <View style={{ gap: spacing.sm }}>
        <AppText variant="label" muted>
          Categoría
        </AppText>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {expenseCategories.map((category) => {
            const isSelected = category.id === categoryId;
            const alreadyBudgeted = budgetedIds.has(category.id) && category.id !== params.categoryId;
            return (
              <Pressable
                key={category.id}
                accessibilityRole="button"
                accessibilityState={{ selected: isSelected, disabled: alreadyBudgeted }}
                disabled={alreadyBudgeted}
                onPress={() => setCategoryId(category.id)}
                style={{
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: 6,
                  paddingHorizontal: spacing.md,
                  paddingVertical: spacing.sm,
                  borderRadius: radius.pill,
                  borderWidth: 1,
                  borderColor: isSelected ? category.color : colors.border,
                  backgroundColor: isSelected ? `${category.color}1F` : colors.surface,
                  opacity: alreadyBudgeted ? 0.4 : 1,
                }}
              >
                <Ionicons
                  name={category.icon as never}
                  size={16}
                  color={isSelected ? category.color : colors.textMuted}
                />
                <AppText
                  variant="caption"
                  weight={isSelected ? '700' : '500'}
                  color={isSelected ? category.color : colors.textMuted}
                >
                  {category.name}
                </AppText>
              </Pressable>
            );
          })}
        </View>
        {budgetedIds.size > 0 ? (
          <AppText variant="caption" muted>
            Las categorías atenuadas ya tienen un presupuesto asignado.
          </AppText>
        ) : null}
      </View>

      <TextField
        label="Límite mensual"
        value={limit}
        onChangeText={setLimit}
        placeholder="0"
        keyboardType="decimal-pad"
        helper={`Moneda: ${settings.currency}`}
      />

      {selected && currentSpend > 0 ? (
        <Card>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
            <IconCircle name={selected.icon} color={selected.color} />
            <View style={{ flex: 1 }}>
              <AppText variant="caption" muted>
                Este mes ya llevas gastado en {selected.name}
              </AppText>
              <AppText weight="700">{formatMoney(currentSpend, settings.currency)}</AppText>
            </View>
          </View>
        </Card>
      ) : null}

      <Button label={existing ? 'Guardar presupuesto' : 'Crear presupuesto'} icon="checkmark" fullWidth onPress={save} />

      {existing ? (
        <Button label="Eliminar presupuesto" variant="danger" icon="trash-outline" fullWidth onPress={confirmDelete} />
      ) : null}
    </Screen>
  );
}
