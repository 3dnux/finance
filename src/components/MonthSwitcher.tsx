import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { Pressable, View } from 'react-native';

import { addMonths, currentMonthKey, monthLabel } from '@/lib/date';
import { radius, spacing, useTheme } from '@/theme';

import { AppText } from './ui';

/** Navegador de mes: ‹ Agosto 2026 ›. No permite avanzar más allá del mes actual. */
export function MonthSwitcher({
  value,
  onChange,
  allowFuture = false,
}: {
  value: string;
  onChange: (key: string) => void;
  allowFuture?: boolean;
}) {
  const { colors } = useTheme();
  const nextKey = addMonths(value, 1);
  const canGoForward = allowFuture || nextKey <= currentMonthKey();

  const arrow = (icon: 'chevron-back' | 'chevron-forward', onPress: () => void, disabled: boolean, label: string) => (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      hitSlop={8}
      style={({ pressed }) => ({
        width: 34,
        height: 34,
        borderRadius: 17,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: colors.surfaceAlt,
        opacity: disabled ? 0.35 : pressed ? 0.7 : 1,
      })}
    >
      <Ionicons name={icon} size={18} color={colors.text} />
    </Pressable>
  );

  return (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        backgroundColor: colors.surface,
        borderRadius: radius.pill,
        padding: spacing.xs,
        paddingHorizontal: spacing.sm,
        borderWidth: 1,
        borderColor: colors.border,
      }}
    >
      {arrow('chevron-back', () => onChange(addMonths(value, -1)), false, 'Mes anterior')}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Ir al mes actual"
        onPress={() => onChange(currentMonthKey())}
        hitSlop={8}
      >
        <AppText weight="700">{monthLabel(value, { withYear: true })}</AppText>
      </Pressable>
      {arrow('chevron-forward', () => onChange(nextKey), !canGoForward, 'Mes siguiente')}
    </View>
  );
}
