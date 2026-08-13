import { Ionicons } from '@expo/vector-icons';
import * as Haptics from 'expo-haptics';
import React from 'react';
import { Platform, Pressable, View } from 'react-native';

import { fontSize, radius, spacing, useTheme } from '@/theme';

import { AppText } from './ui';

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', ',', '0', 'del'];

/**
 * Teclado numérico propio: evita el teclado del sistema, que en importes
 * resulta lento y no garantiza la coma decimal en todos los idiomas.
 */
export function Keypad({ value, onChange }: { value: string; onChange: (next: string) => void }) {
  const { colors } = useTheme();

  const press = (key: string) => {
    if (Platform.OS !== 'web') void Haptics.selectionAsync();

    if (key === 'del') {
      onChange(value.slice(0, -1));
      return;
    }
    if (key === ',') {
      if (value.includes(',')) return;
      onChange(value === '' ? '0,' : `${value},`);
      return;
    }
    const [, decimals] = value.split(',');
    if (decimals !== undefined && decimals.length >= 2) return;
    // Sin ceros a la izquierda: "0" + "5" debe quedar en "5".
    if (value === '0') {
      onChange(key);
      return;
    }
    if (value.replace(',', '').length >= 12) return;
    onChange(value + key);
  };

  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap' }}>
      {KEYS.map((key) => (
        <Pressable
          key={key}
          accessibilityRole="button"
          accessibilityLabel={key === 'del' ? 'Borrar' : key === ',' ? 'Coma decimal' : key}
          onPress={() => press(key)}
          onLongPress={key === 'del' ? () => onChange('') : undefined}
          style={{ width: '33.33%', paddingVertical: spacing.md, paddingHorizontal: spacing.xs }}
        >
          {({ pressed }) => (
            <View
              style={{
                alignItems: 'center',
                justifyContent: 'center',
                paddingVertical: 14,
                borderRadius: radius.md,
                backgroundColor: pressed ? colors.surfaceAlt : 'transparent',
              }}
            >
              {key === 'del' ? (
                <Ionicons name="backspace-outline" size={24} color={colors.text} />
              ) : (
                <AppText style={{ fontSize: fontSize.xl, fontWeight: '600' }}>{key}</AppText>
              )}
            </View>
          )}
        </Pressable>
      ))}
    </View>
  );
}
