import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { Modal, Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { radius, spacing, useTheme } from '@/theme';

import { AppText } from './ui';

/** Panel inferior modal, usado para todos los selectores de la app. */
export function Sheet({
  visible,
  onClose,
  title,
  children,
  maxHeightRatio = 0.8,
}: {
  visible: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  maxHeightRatio?: number;
}) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose} statusBarTranslucent>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Cerrar"
        onPress={onClose}
        style={{ flex: 1, backgroundColor: colors.overlay }}
      />
      <View
        style={{
          backgroundColor: colors.surface,
          borderTopLeftRadius: radius.xl,
          borderTopRightRadius: radius.xl,
          paddingBottom: insets.bottom + spacing.lg,
          maxHeight: `${maxHeightRatio * 100}%`,
        }}
      >
        <View style={{ alignItems: 'center', paddingTop: spacing.sm }}>
          <View style={{ width: 40, height: 4, borderRadius: 2, backgroundColor: colors.border }} />
        </View>
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: spacing.lg,
            paddingBottom: spacing.md,
          }}
        >
          <AppText variant="subtitle">{title}</AppText>
          <Pressable accessibilityRole="button" accessibilityLabel="Cerrar" onPress={onClose} hitSlop={10}>
            <Ionicons name="close" size={22} color={colors.textMuted} />
          </Pressable>
        </View>
        <ScrollView
          contentContainerStyle={{ paddingHorizontal: spacing.lg, paddingBottom: spacing.lg }}
          keyboardShouldPersistTaps="handled"
        >
          {children}
        </ScrollView>
      </View>
    </Modal>
  );
}
