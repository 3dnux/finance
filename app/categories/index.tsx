import { useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { View } from 'react-native';

import { Screen } from '@/components/Screen';
import { AppText, Button, Card, Divider, IconCircle, ListRow, Segmented } from '@/components/ui';
import { useFinance } from '@/store/FinanceProvider';
import { spacing } from '@/theme';

export default function CategoriesScreen() {
  const router = useRouter();
  const { state } = useFinance();
  const [kind, setKind] = useState<'expense' | 'income'>('expense');

  const list = useMemo(() => state.categories.filter((c) => c.kind === kind), [state.categories, kind]);
  const usage = useMemo(() => {
    const counts = new Map<string, number>();
    state.transactions.forEach((tx) => {
      if (!tx.categoryId) return;
      counts.set(tx.categoryId, (counts.get(tx.categoryId) ?? 0) + 1);
    });
    return counts;
  }, [state.transactions]);

  return (
    <Screen contentStyle={{ paddingTop: spacing.lg }}>
      <Segmented
        value={kind}
        onChange={setKind}
        options={[
          { value: 'expense', label: 'Gastos' },
          { value: 'income', label: 'Ingresos' },
        ]}
      />

      <Card padded={false} style={{ paddingHorizontal: spacing.lg, marginTop: spacing.lg }}>
        {list.map((category, index) => (
          <View key={category.id}>
            {index > 0 ? <Divider /> : null}
            <ListRow
              title={category.name}
              subtitle={`${usage.get(category.id) ?? 0} movimientos${category.custom ? ' · personalizada' : ''}`}
              left={<IconCircle name={category.icon} color={category.color} />}
              chevron
              onPress={() => router.push({ pathname: '/categories/[id]', params: { id: category.id } })}
            />
          </View>
        ))}
      </Card>

      <Button
        label="Nueva categoría"
        icon="add"
        fullWidth
        style={{ marginTop: spacing.lg }}
        onPress={() => router.push({ pathname: '/categories/[id]', params: { id: 'new', kind } })}
      />

      <AppText variant="caption" muted style={{ marginTop: spacing.lg, textAlign: 'center' }}>
        Al eliminar una categoría, sus movimientos pasan a «Otros» y no se pierde ningún importe.
      </AppText>
    </Screen>
  );
}
