import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { FinanceProvider, useFinance } from '@/store/FinanceProvider';
import { ThemeProvider, useTheme } from '@/theme';

function Navigation() {
  const { colors, isDark } = useTheme();
  const { ready } = useFinance();

  if (!ready) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background }}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  return (
    <>
      <StatusBar style={isDark ? 'light' : 'dark'} />
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.background },
          headerTitleStyle: { color: colors.text, fontWeight: '700' },
          headerTintColor: colors.primary,
          headerShadowVisible: false,
          contentStyle: { backgroundColor: colors.background },
        }}
      >
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen
          name="transaction/[id]"
          options={{ presentation: 'modal', title: 'Movimiento', headerShown: false }}
        />
        <Stack.Screen name="accounts/index" options={{ title: 'Cuentas' }} />
        <Stack.Screen name="accounts/[id]" options={{ presentation: 'modal', title: 'Cuenta' }} />
        <Stack.Screen name="categories/index" options={{ title: 'Categorías' }} />
        <Stack.Screen name="categories/[id]" options={{ presentation: 'modal', title: 'Categoría' }} />
        <Stack.Screen name="budgets/edit" options={{ presentation: 'modal', title: 'Presupuesto' }} />
        <Stack.Screen name="data/backup" options={{ title: 'Copia de seguridad' }} />
      </Stack>
    </>
  );
}

function Themed() {
  const { state } = useFinance();
  return (
    <ThemeProvider mode={state.settings.themeMode}>
      <Navigation />
    </ThemeProvider>
  );
}

export default function RootLayout() {
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <FinanceProvider>
          <Themed />
        </FinanceProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
