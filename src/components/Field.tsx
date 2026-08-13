import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { Pressable, TextInput, View, type KeyboardTypeOptions } from 'react-native';

import { fontSize, radius, spacing, useTheme } from '@/theme';

import { AppText, iconName } from './ui';

/** Fila pulsable de formulario: etiqueta a la izquierda, valor y chevron a la derecha. */
export function SelectField({
  label,
  value,
  icon,
  iconColor,
  onPress,
  placeholder = 'Seleccionar',
}: {
  label: string;
  value?: string;
  icon?: string;
  iconColor?: string;
  onPress: () => void;
  placeholder?: string;
}) {
  const { colors } = useTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${label}: ${value ?? placeholder}`}
      onPress={onPress}
      style={({ pressed }) => ({
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.md,
        paddingVertical: spacing.md,
        paddingHorizontal: spacing.lg,
        backgroundColor: colors.surface,
        borderRadius: radius.md,
        borderWidth: 1,
        borderColor: colors.border,
        opacity: pressed ? 0.7 : 1,
      })}
    >
      {icon ? <Ionicons name={iconName(icon)} size={20} color={iconColor ?? colors.textMuted} /> : null}
      <AppText muted style={{ flex: 1 }}>
        {label}
      </AppText>
      <AppText weight="600" color={value ? undefined : colors.textMuted} numberOfLines={1} style={{ maxWidth: '55%' }}>
        {value ?? placeholder}
      </AppText>
      <Ionicons name="chevron-forward" size={16} color={colors.textMuted} />
    </Pressable>
  );
}

/** Campo de texto con etiqueta encima. */
export function TextField({
  label,
  value,
  onChangeText,
  placeholder,
  keyboardType,
  autoFocus,
  multiline,
  maxLength,
  helper,
}: {
  label: string;
  value: string;
  onChangeText: (text: string) => void;
  placeholder?: string;
  keyboardType?: KeyboardTypeOptions;
  autoFocus?: boolean;
  multiline?: boolean;
  maxLength?: number;
  helper?: string;
}) {
  const { colors } = useTheme();
  return (
    <View style={{ gap: spacing.xs }}>
      <AppText variant="label" muted>
        {label}
      </AppText>
      <TextInput
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={colors.textMuted}
        keyboardType={keyboardType}
        autoFocus={autoFocus}
        multiline={multiline}
        maxLength={maxLength}
        style={{
          backgroundColor: colors.surface,
          borderRadius: radius.md,
          borderWidth: 1,
          borderColor: colors.border,
          paddingHorizontal: spacing.lg,
          paddingVertical: multiline ? spacing.md : 14,
          minHeight: multiline ? 88 : undefined,
          textAlignVertical: multiline ? 'top' : 'center',
          color: colors.text,
          fontSize: fontSize.md,
          fontWeight: '500',
        }}
      />
      {helper ? (
        <AppText variant="caption" muted>
          {helper}
        </AppText>
      ) : null}
    </View>
  );
}

/** Selector de color en rejilla. */
export function ColorPicker({
  colors: swatchList,
  value,
  onChange,
}: {
  colors: string[];
  value: string;
  onChange: (color: string) => void;
}) {
  const { colors } = useTheme();
  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
      {swatchList.map((swatch) => (
        <Pressable
          key={swatch}
          accessibilityRole="button"
          accessibilityLabel={`Color ${swatch}`}
          accessibilityState={{ selected: swatch === value }}
          onPress={() => onChange(swatch)}
          style={{
            width: 38,
            height: 38,
            borderRadius: 19,
            backgroundColor: swatch,
            alignItems: 'center',
            justifyContent: 'center',
            borderWidth: swatch === value ? 3 : 0,
            borderColor: colors.text,
          }}
        >
          {swatch === value ? <Ionicons name="checkmark" size={18} color="#FFFFFF" /> : null}
        </Pressable>
      ))}
    </View>
  );
}
