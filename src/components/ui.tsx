import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
  type StyleProp,
  type TextStyle,
  type ViewStyle,
} from 'react-native';

import { fontSize, radius, spacing, useTheme } from '@/theme';

type IconName = keyof typeof Ionicons.glyphMap;

export function iconName(name: string): IconName {
  return name as IconName;
}

/* ------------------------------------------------------------------ Texto */

interface AppTextProps {
  children: React.ReactNode;
  variant?: 'display' | 'title' | 'subtitle' | 'body' | 'caption' | 'label';
  color?: string;
  muted?: boolean;
  weight?: '400' | '500' | '600' | '700' | '800';
  style?: StyleProp<TextStyle>;
  numberOfLines?: number;
}

export function AppText({ children, variant = 'body', color, muted, weight, style, numberOfLines }: AppTextProps) {
  const { colors } = useTheme();
  const variants: Record<NonNullable<AppTextProps['variant']>, TextStyle> = {
    display: { fontSize: fontSize.display, fontWeight: '800', letterSpacing: -1 },
    title: { fontSize: fontSize.xl, fontWeight: '700', letterSpacing: -0.4 },
    subtitle: { fontSize: fontSize.lg, fontWeight: '600' },
    body: { fontSize: fontSize.md, fontWeight: '500' },
    caption: { fontSize: fontSize.sm, fontWeight: '500' },
    label: { fontSize: fontSize.xs, fontWeight: '700', letterSpacing: 0.8, textTransform: 'uppercase' },
  };

  return (
    <Text
      numberOfLines={numberOfLines}
      style={[
        variants[variant],
        { color: color ?? (muted ? colors.textMuted : colors.text) },
        weight ? { fontWeight: weight } : null,
        style,
      ]}
    >
      {children}
    </Text>
  );
}

/* ---------------------------------------------------------------- Tarjeta */

export function Card({
  children,
  style,
  padded = true,
}: {
  children: React.ReactNode;
  style?: StyleProp<ViewStyle>;
  padded?: boolean;
}) {
  const { colors } = useTheme();
  return (
    <View
      style={[
        {
          backgroundColor: colors.surface,
          borderRadius: radius.lg,
          borderWidth: StyleSheet.hairlineWidth,
          borderColor: colors.border,
          padding: padded ? spacing.lg : 0,
        },
        style,
      ]}
    >
      {children}
    </View>
  );
}

/* ----------------------------------------------------------------- Botón */

interface ButtonProps {
  label: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  icon?: string;
  disabled?: boolean;
  loading?: boolean;
  fullWidth?: boolean;
  style?: StyleProp<ViewStyle>;
}

export function Button({
  label,
  onPress,
  variant = 'primary',
  icon,
  disabled,
  loading,
  fullWidth,
  style,
}: ButtonProps) {
  const { colors } = useTheme();

  const palette: Record<NonNullable<ButtonProps['variant']>, { bg: string; fg: string; border: string }> = {
    primary: { bg: colors.primary, fg: colors.onPrimary, border: colors.primary },
    secondary: { bg: colors.surfaceAlt, fg: colors.text, border: colors.border },
    ghost: { bg: 'transparent', fg: colors.primary, border: 'transparent' },
    danger: { bg: colors.expenseSoft, fg: colors.expense, border: colors.expenseSoft },
  };
  const tone = palette[variant];

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: !!disabled || !!loading }}
      disabled={disabled || loading}
      onPress={onPress}
      style={({ pressed }) => [
        {
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'center',
          gap: spacing.sm,
          backgroundColor: tone.bg,
          borderColor: tone.border,
          borderWidth: 1,
          borderRadius: radius.md,
          paddingVertical: 14,
          paddingHorizontal: spacing.lg,
          opacity: disabled ? 0.5 : pressed ? 0.85 : 1,
          alignSelf: fullWidth ? 'stretch' : 'flex-start',
        },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator size="small" color={tone.fg} />
      ) : (
        <>
          {icon ? <Ionicons name={iconName(icon)} size={18} color={tone.fg} /> : null}
          <Text style={{ color: tone.fg, fontSize: fontSize.md, fontWeight: '700' }}>{label}</Text>
        </>
      )}
    </Pressable>
  );
}

/* ------------------------------------------------------------ Icono redondo */

export function IconCircle({
  name,
  color,
  size = 42,
  background,
}: {
  name: string;
  color: string;
  size?: number;
  background?: string;
}) {
  return (
    <View
      style={{
        width: size,
        height: size,
        borderRadius: size / 2,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: background ?? `${color}22`,
      }}
    >
      <Ionicons name={iconName(name)} size={size * 0.5} color={color} />
    </View>
  );
}

/* ------------------------------------------------------------ Barra progreso */

export function ProgressBar({
  progress,
  color,
  height = 8,
  track,
  markerAt,
}: {
  progress: number;
  color: string;
  height?: number;
  track?: string;
  /** Marca opcional (0..1), usada para señalar el ritmo esperado del mes. */
  markerAt?: number;
}) {
  const { colors } = useTheme();
  const clamped = Math.max(0, Math.min(1, progress));
  return (
    <View
      accessibilityRole="progressbar"
      accessibilityValue={{ min: 0, max: 100, now: Math.round(clamped * 100) }}
      style={{
        height,
        borderRadius: height / 2,
        backgroundColor: track ?? colors.surfaceAlt,
        overflow: 'hidden',
        justifyContent: 'center',
      }}
    >
      <View style={{ width: `${clamped * 100}%`, height: '100%', borderRadius: height / 2, backgroundColor: color }} />
      {markerAt !== undefined && markerAt > 0 && markerAt < 1 ? (
        <View
          style={{
            position: 'absolute',
            left: `${markerAt * 100}%`,
            width: 2,
            height: '100%',
            backgroundColor: colors.text,
            opacity: 0.35,
          }}
        />
      ) : null}
    </View>
  );
}

/* ------------------------------------------------------------------ Chips */

export function Chip({
  label,
  active,
  onPress,
  color,
  icon,
}: {
  label: string;
  active?: boolean;
  onPress: () => void;
  color?: string;
  icon?: string;
}) {
  const { colors } = useTheme();
  const tint = color ?? colors.primary;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected: !!active }}
      onPress={onPress}
      style={({ pressed }) => ({
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.sm,
        borderRadius: radius.pill,
        borderWidth: 1,
        borderColor: active ? tint : colors.border,
        backgroundColor: active ? `${tint}1F` : colors.surface,
        opacity: pressed ? 0.8 : 1,
      })}
    >
      {icon ? <Ionicons name={iconName(icon)} size={14} color={active ? tint : colors.textMuted} /> : null}
      <Text
        style={{
          color: active ? tint : colors.textMuted,
          fontSize: fontSize.sm,
          fontWeight: active ? '700' : '600',
        }}
      >
        {label}
      </Text>
    </Pressable>
  );
}

/* ------------------------------------------------------- Control segmentado */

export function Segmented<T extends string>({
  options,
  value,
  onChange,
}: {
  options: { value: T; label: string; color?: string }[];
  value: T;
  onChange: (value: T) => void;
}) {
  const { colors } = useTheme();
  return (
    <View
      style={{
        flexDirection: 'row',
        backgroundColor: colors.surfaceAlt,
        borderRadius: radius.md,
        padding: 4,
        gap: 4,
      }}
    >
      {options.map((option) => {
        const active = option.value === value;
        const tint = option.color ?? colors.primary;
        return (
          <Pressable
            key={option.value}
            accessibilityRole="tab"
            accessibilityState={{ selected: active }}
            onPress={() => onChange(option.value)}
            style={{
              flex: 1,
              alignItems: 'center',
              paddingVertical: 10,
              borderRadius: radius.sm,
              backgroundColor: active ? colors.surface : 'transparent',
            }}
          >
            <Text
              style={{
                color: active ? tint : colors.textMuted,
                fontWeight: active ? '700' : '600',
                fontSize: fontSize.sm,
              }}
            >
              {option.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

/* -------------------------------------------------------------- Fila lista */

export function ListRow({
  title,
  subtitle,
  left,
  right,
  onPress,
  danger,
  chevron,
}: {
  title: string;
  subtitle?: string;
  left?: React.ReactNode;
  right?: React.ReactNode;
  onPress?: () => void;
  danger?: boolean;
  chevron?: boolean;
}) {
  const { colors } = useTheme();
  const content = (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingVertical: spacing.md }}>
      {left}
      <View style={{ flex: 1 }}>
        <AppText weight="600" color={danger ? colors.expense : undefined} numberOfLines={1}>
          {title}
        </AppText>
        {subtitle ? (
          <AppText variant="caption" muted numberOfLines={1}>
            {subtitle}
          </AppText>
        ) : null}
      </View>
      {right}
      {chevron ? <Ionicons name="chevron-forward" size={18} color={colors.textMuted} /> : null}
    </View>
  );

  if (!onPress) return content;
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={({ pressed }) => ({ opacity: pressed ? 0.6 : 1 })}>
      {content}
    </Pressable>
  );
}

export function Divider() {
  const { colors } = useTheme();
  return <View style={{ height: StyleSheet.hairlineWidth, backgroundColor: colors.border }} />;
}

/* --------------------------------------------------------------- Vacíos */

export function EmptyState({
  icon,
  title,
  message,
  action,
}: {
  icon: string;
  title: string;
  message?: string;
  action?: React.ReactNode;
}) {
  const { colors } = useTheme();
  return (
    <View style={{ alignItems: 'center', paddingVertical: spacing.xxl, gap: spacing.sm }}>
      <IconCircle name={icon} color={colors.textMuted} size={56} background={colors.surfaceAlt} />
      <AppText variant="subtitle">{title}</AppText>
      {message ? (
        <AppText variant="caption" muted style={{ textAlign: 'center', maxWidth: 280 }}>
          {message}
        </AppText>
      ) : null}
      {action ? <View style={{ marginTop: spacing.sm }}>{action}</View> : null}
    </View>
  );
}

/* -------------------------------------------------------- Cabecera sección */

export function SectionHeader({
  title,
  action,
  onAction,
}: {
  title: string;
  action?: string;
  onAction?: () => void;
}) {
  const { colors } = useTheme();
  return (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        marginBottom: spacing.md,
      }}
    >
      <AppText variant="label" muted>
        {title}
      </AppText>
      {action && onAction ? (
        <Pressable accessibilityRole="button" onPress={onAction} hitSlop={8}>
          <Text style={{ color: colors.primary, fontSize: fontSize.sm, fontWeight: '700' }}>{action}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}
