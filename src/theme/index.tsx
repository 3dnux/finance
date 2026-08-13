import React, { createContext, useContext, useMemo } from 'react';
import { useColorScheme } from 'react-native';

import { darkPalette, lightPalette, type Palette } from './tokens';

export type ThemeMode = 'system' | 'light' | 'dark';

interface ThemeValue {
  colors: Palette;
  scheme: 'light' | 'dark';
  isDark: boolean;
}

const ThemeContext = createContext<ThemeValue>({
  colors: lightPalette,
  scheme: 'light',
  isDark: false,
});

export function ThemeProvider({
  mode,
  children,
}: {
  mode: ThemeMode;
  children: React.ReactNode;
}) {
  const system = useColorScheme();
  const scheme: 'light' | 'dark' = mode === 'system' ? (system === 'dark' ? 'dark' : 'light') : mode;

  const value = useMemo<ThemeValue>(
    () => ({
      colors: scheme === 'dark' ? darkPalette : lightPalette,
      scheme,
      isDark: scheme === 'dark',
    }),
    [scheme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  return useContext(ThemeContext);
}

export * from './tokens';
