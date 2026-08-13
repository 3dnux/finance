import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { Pressable, View } from 'react-native';

import { MONTHS, addMonths, daysInMonth, fromISODate, monthKey, monthLabel, today } from '@/lib/date';
import { radius, spacing, useTheme } from '@/theme';

import { Sheet } from './Sheet';
import { AppText, Button } from './ui';

const WEEKDAY_INITIALS = ['L', 'M', 'X', 'J', 'V', 'S', 'D'];

/** Índice 0..6 (lunes primero) del primer día del mes. */
function firstWeekdayOffset(key: string): number {
  const [y, m] = key.split('-').map(Number);
  const day = new Date(y, m - 1, 1).getDay();
  return (day + 6) % 7;
}

/** Calendario propio: sin dependencias nativas y con semana empezando en lunes. */
export function DatePicker({
  visible,
  value,
  onClose,
  onSelect,
}: {
  visible: boolean;
  value: string;
  onClose: () => void;
  onSelect: (iso: string) => void;
}) {
  const { colors } = useTheme();
  const [view, setView] = useState(() => monthKey(value));

  // Al reabrir, el calendario vuelve al mes de la fecha seleccionada.
  React.useEffect(() => {
    if (visible) setView(monthKey(value));
  }, [visible, value]);

  const total = daysInMonth(view);
  const offset = firstWeekdayOffset(view);
  const cells: (number | null)[] = [
    ...Array.from({ length: offset }, () => null),
    ...Array.from({ length: total }, (_, i) => i + 1),
  ];
  const todayIso = today();

  const isoFor = (day: number) => `${view}-${String(day).padStart(2, '0')}`;

  return (
    <Sheet visible={visible} onClose={onClose} title="Elegir fecha" maxHeightRatio={0.75}>
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: spacing.md,
        }}
      >
        <Pressable accessibilityRole="button" accessibilityLabel="Mes anterior" onPress={() => setView(addMonths(view, -1))} hitSlop={10}>
          <Ionicons name="chevron-back" size={22} color={colors.text} />
        </Pressable>
        <AppText weight="700">{monthLabel(view, { withYear: true })}</AppText>
        <Pressable accessibilityRole="button" accessibilityLabel="Mes siguiente" onPress={() => setView(addMonths(view, 1))} hitSlop={10}>
          <Ionicons name="chevron-forward" size={22} color={colors.text} />
        </Pressable>
      </View>

      <View style={{ flexDirection: 'row', marginBottom: spacing.sm }}>
        {WEEKDAY_INITIALS.map((initial, index) => (
          <View key={`${initial}-${index}`} style={{ flex: 1, alignItems: 'center' }}>
            <AppText variant="caption" muted weight="700">
              {initial}
            </AppText>
          </View>
        ))}
      </View>

      <View style={{ flexDirection: 'row', flexWrap: 'wrap' }}>
        {cells.map((day, index) => {
          if (day === null) return <View key={`empty-${index}`} style={{ width: `${100 / 7}%`, height: 44 }} />;
          const iso = isoFor(day);
          const selected = iso === value;
          const isToday = iso === todayIso;
          return (
            <Pressable
              key={iso}
              accessibilityRole="button"
              accessibilityLabel={`${day} de ${MONTHS[Number(view.slice(5, 7)) - 1]}`}
              accessibilityState={{ selected }}
              onPress={() => {
                onSelect(iso);
                onClose();
              }}
              style={{ width: `${100 / 7}%`, height: 44, alignItems: 'center', justifyContent: 'center' }}
            >
              <View
                style={{
                  width: 36,
                  height: 36,
                  borderRadius: 18,
                  alignItems: 'center',
                  justifyContent: 'center',
                  backgroundColor: selected ? colors.primary : 'transparent',
                  borderWidth: isToday && !selected ? 1 : 0,
                  borderColor: colors.primary,
                }}
              >
                <AppText color={selected ? colors.onPrimary : undefined} weight={selected || isToday ? '700' : '500'}>
                  {day}
                </AppText>
              </View>
            </Pressable>
          );
        })}
      </View>

      <View style={{ flexDirection: 'row', gap: spacing.sm, marginTop: spacing.lg }}>
        <Button
          label="Hoy"
          variant="secondary"
          style={{ flex: 1 }}
          onPress={() => {
            onSelect(todayIso);
            onClose();
          }}
        />
        <Button
          label="Ayer"
          variant="secondary"
          style={{ flex: 1 }}
          onPress={() => {
            const d = fromISODate(todayIso);
            d.setDate(d.getDate() - 1);
            onSelect(`${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`);
            onClose();
          }}
        />
      </View>
      <View style={{ height: radius.sm }} />
    </Sheet>
  );
}
