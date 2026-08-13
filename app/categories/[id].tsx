import { useLocalSearchParams, useNavigation, useRouter } from 'expo-router';
import React, { useLayoutEffect, useMemo, useState } from 'react';
import { Alert, Pressable, View } from 'react-native';

import { ColorPicker, TextField } from '@/components/Field';
import { Screen } from '@/components/Screen';
import { AppText, Button, IconCircle, Segmented } from '@/components/ui';
import { useFinance } from '@/store/FinanceProvider';
import { radius, spacing, swatches, useTheme } from '@/theme';

/** Iconos disponibles al crear categorías propias. */
const ICONS = [
  'cart-outline',
  'restaurant-outline',
  'home-outline',
  'flash-outline',
  'car-outline',
  'bus-outline',
  'medkit-outline',
  'fitness-outline',
  'game-controller-outline',
  'shirt-outline',
  'school-outline',
  'airplane-outline',
  'wifi-outline',
  'phone-portrait-outline',
  'paw-outline',
  'gift-outline',
  'briefcase-outline',
  'laptop-outline',
  'trending-up-outline',
  'cash-outline',
  'card-outline',
  'construct-outline',
  'book-outline',
  'ellipsis-horizontal-outline',
];

export default function CategoryFormScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const navigation = useNavigation();
  const params = useLocalSearchParams<{ id: string; kind?: string }>();
  const { state, addCategory, updateCategory, deleteCategory } = useFinance();

  const existing = useMemo(
    () => (params.id && params.id !== 'new' ? state.categories.find((c) => c.id === params.id) : undefined),
    [params.id, state.categories],
  );

  const [name, setName] = useState(existing?.name ?? '');
  const [kind, setKind] = useState<'expense' | 'income'>(
    existing?.kind ?? (params.kind === 'income' ? 'income' : 'expense'),
  );
  const [icon, setIcon] = useState(existing?.icon ?? ICONS[0]);
  const [color, setColor] = useState(existing?.color ?? swatches[0]);

  useLayoutEffect(() => {
    navigation.setOptions({ title: existing ? 'Editar categoría' : 'Nueva categoría' });
  }, [navigation, existing]);

  const save = () => {
    if (!name.trim()) {
      Alert.alert('Falta el nombre', 'Escribe un nombre para la categoría.');
      return;
    }
    if (existing) updateCategory({ ...existing, name: name.trim(), kind, icon, color });
    else addCategory({ name: name.trim(), kind, icon, color });
    router.back();
  };

  const confirmDelete = () => {
    if (!existing) return;
    const linked = state.transactions.filter((tx) => tx.categoryId === existing.id).length;
    Alert.alert(
      'Eliminar categoría',
      linked
        ? `${linked} movimiento(s) pasarán a la categoría «Otros». ¿Continuar?`
        : '¿Seguro que quieres eliminar esta categoría?',
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Eliminar',
          style: 'destructive',
          onPress: () => {
            deleteCategory(existing.id);
            router.back();
          },
        },
      ],
    );
  };

  return (
    <Screen contentStyle={{ paddingTop: spacing.lg, gap: spacing.lg }}>
      <View style={{ alignItems: 'center' }}>
        <IconCircle name={icon} color={color} size={72} />
        <AppText variant="subtitle" style={{ marginTop: spacing.sm }}>
          {name.trim() || 'Nueva categoría'}
        </AppText>
      </View>

      <TextField label="Nombre" value={name} onChangeText={setName} placeholder="Ej. Cafetería" maxLength={30} />

      <View style={{ gap: spacing.sm }}>
        <AppText variant="label" muted>
          Tipo
        </AppText>
        <Segmented
          value={kind}
          onChange={setKind}
          options={[
            { value: 'expense', label: 'Gasto', color: colors.expense },
            { value: 'income', label: 'Ingreso', color: colors.income },
          ]}
        />
      </View>

      <View style={{ gap: spacing.sm }}>
        <AppText variant="label" muted>
          Color
        </AppText>
        <ColorPicker colors={swatches} value={color} onChange={setColor} />
      </View>

      <View style={{ gap: spacing.sm }}>
        <AppText variant="label" muted>
          Icono
        </AppText>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {ICONS.map((option) => {
            const selected = option === icon;
            return (
              <Pressable
                key={option}
                accessibilityRole="button"
                accessibilityLabel={`Icono ${option}`}
                accessibilityState={{ selected }}
                onPress={() => setIcon(option)}
                style={{
                  borderRadius: radius.md,
                  padding: 4,
                  borderWidth: 2,
                  borderColor: selected ? color : 'transparent',
                }}
              >
                <IconCircle name={option} color={selected ? color : colors.textMuted} size={40} />
              </Pressable>
            );
          })}
        </View>
      </View>

      <Button label={existing ? 'Guardar cambios' : 'Crear categoría'} icon="checkmark" fullWidth onPress={save} />

      {existing ? (
        <Button label="Eliminar categoría" variant="danger" icon="trash-outline" fullWidth onPress={confirmDelete} />
      ) : null}
    </Screen>
  );
}
