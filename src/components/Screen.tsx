import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { Pressable, ScrollView, View, type StyleProp, type ViewStyle } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { spacing, useTheme } from '@/theme';

import { AppText } from './ui';

/** Contenedor de pantalla con fondo del tema y márgenes seguros. */
export function Screen({
  children,
  scroll = true,
  style,
  contentStyle,
  // Deja hueco para que el botón flotante no tape el final del contenido.
  bottomInset = 96,
}: {
  children: React.ReactNode;
  scroll?: boolean;
  style?: StyleProp<ViewStyle>;
  contentStyle?: StyleProp<ViewStyle>;
  bottomInset?: number;
}) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const padding = {
    paddingHorizontal: spacing.lg,
    paddingBottom: bottomInset + insets.bottom,
  };

  if (!scroll) {
    return <View style={[{ flex: 1, backgroundColor: colors.background }, padding, style]}>{children}</View>;
  }

  return (
    <ScrollView
      style={[{ flex: 1, backgroundColor: colors.background }, style]}
      contentContainerStyle={[padding, contentStyle]}
      keyboardShouldPersistTaps="handled"
      showsVerticalScrollIndicator={false}
    >
      {children}
    </ScrollView>
  );
}

/** Cabecera con título grande y acción opcional a la derecha. */
export function ScreenHeader({
  title,
  subtitle,
  actionIcon,
  onAction,
  actionLabel,
}: {
  title: string;
  subtitle?: string;
  actionIcon?: string;
  onAction?: () => void;
  actionLabel?: string;
}) {
  const { colors } = useTheme();
  return (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: spacing.sm,
        paddingBottom: spacing.lg,
        gap: spacing.md,
      }}
    >
      <View style={{ flex: 1 }}>
        <AppText variant="title">{title}</AppText>
        {subtitle ? (
          <AppText variant="caption" muted>
            {subtitle}
          </AppText>
        ) : null}
      </View>
      {actionIcon && onAction ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={actionLabel ?? 'Acción'}
          onPress={onAction}
          style={({ pressed }) => ({
            width: 40,
            height: 40,
            borderRadius: 20,
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: colors.surface,
            borderWidth: 1,
            borderColor: colors.border,
            opacity: pressed ? 0.7 : 1,
          })}
        >
          <Ionicons name={actionIcon as never} size={20} color={colors.text} />
        </Pressable>
      ) : null}
    </View>
  );
}
