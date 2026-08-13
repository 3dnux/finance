import { Ionicons } from '@expo/vector-icons';
import { Tabs, usePathname, useRouter } from 'expo-router';
import React from 'react';
import { Platform, Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { iconName } from '@/components/ui';
import { spacing, useTheme } from '@/theme';

/** Botón flotante para crear un movimiento desde cualquier pestaña. */
function AddButton() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const pathname = usePathname();

  if (pathname.startsWith('/settings')) return null;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Añadir movimiento"
      onPress={() => router.push('/transaction/new')}
      style={({ pressed }) => ({
        position: 'absolute',
        right: spacing.lg,
        bottom: insets.bottom + 72,
        width: 58,
        height: 58,
        borderRadius: 29,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: colors.primary,
        transform: [{ scale: pressed ? 0.94 : 1 }],
        shadowColor: '#000',
        shadowOpacity: 0.25,
        shadowRadius: 12,
        shadowOffset: { width: 0, height: 6 },
        elevation: 6,
      })}
    >
      <Ionicons name="add" size={30} color={colors.onPrimary} />
    </Pressable>
  );
}

export default function TabsLayout() {
  const { colors } = useTheme();

  // `label` es la etiqueta corta de la barra (el ancho por pestaña no da para más);
  // el nombre completo de cada sección aparece en la cabecera de su pantalla.
  const tabs: { name: string; title: string; label: string; icon: string }[] = [
    { name: 'index', title: 'Resumen', label: 'Resumen', icon: 'pie-chart' },
    { name: 'transactions', title: 'Movimientos', label: 'Actividad', icon: 'swap-vertical' },
    { name: 'budgets', title: 'Presupuestos', label: 'Límites', icon: 'speedometer' },
    { name: 'stats', title: 'Análisis', label: 'Análisis', icon: 'bar-chart' },
    { name: 'settings', title: 'Ajustes', label: 'Ajustes', icon: 'settings' },
  ];

  return (
    <View style={{ flex: 1 }}>
      <Tabs
        screenOptions={{
          headerShown: false,
          tabBarActiveTintColor: colors.primary,
          tabBarInactiveTintColor: colors.textMuted,
          tabBarStyle: {
            backgroundColor: colors.surface,
            borderTopColor: colors.border,
            borderTopWidth: StyleSheet.hairlineWidth,
            height: Platform.OS === 'ios' ? 84 : 64,
            paddingTop: 6,
            paddingBottom: Platform.OS === 'ios' ? 28 : 8,
          },
          // Con 5 pestañas el ancho por elemento es justo: sin padding lateral y a 10 pt
          // las etiquetas caben enteras incluso en pantallas de 360 pt.
          // El margen negativo recupera los ~10 pt que el contenedor reserva a los lados,
          // suficiente para que «Presupuestos» quepa entero también en pantallas de 360 pt.
          tabBarLabelStyle: { fontSize: 10, fontWeight: '600', marginHorizontal: -6 },
          tabBarItemStyle: { paddingHorizontal: 0 },
          tabBarIconStyle: { marginBottom: -2 },
        }}
      >
        {tabs.map((tab) => (
          <Tabs.Screen
            key={tab.name}
            name={tab.name}
            options={{
              title: tab.title,
              tabBarLabel: tab.label,
              tabBarIcon: ({ color, focused, size }) => (
                <Ionicons name={iconName(focused ? tab.icon : `${tab.icon}-outline`)} size={size ?? 22} color={color} />
              ),
            }}
          />
        ))}
      </Tabs>
      <AddButton />
    </View>
  );
}
