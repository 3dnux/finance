export interface Palette {
  /** Fondo general de la pantalla. */
  background: string;
  /** Superficie de tarjetas y listas. */
  surface: string;
  /** Superficie elevada (modales, campos, chips). */
  surfaceAlt: string;
  text: string;
  textMuted: string;
  border: string;
  primary: string;
  primarySoft: string;
  onPrimary: string;
  income: string;
  incomeSoft: string;
  expense: string;
  expenseSoft: string;
  warning: string;
  warningSoft: string;
  transfer: string;
  overlay: string;
}

export const lightPalette: Palette = {
  background: '#F4F5F9',
  surface: '#FFFFFF',
  surfaceAlt: '#EEF0F6',
  text: '#0F172A',
  textMuted: '#6B7280',
  border: '#E3E6EE',
  primary: '#4F46E5',
  primarySoft: '#E7E6FD',
  onPrimary: '#FFFFFF',
  income: '#0F9D58',
  incomeSoft: '#DDF3E6',
  expense: '#E23D3D',
  expenseSoft: '#FCE3E3',
  warning: '#C77700',
  warningSoft: '#FBEBD2',
  transfer: '#0E7490',
  overlay: 'rgba(15, 23, 42, 0.45)',
};

export const darkPalette: Palette = {
  background: '#0B0F1A',
  surface: '#151B2B',
  surfaceAlt: '#1E263A',
  text: '#F8FAFC',
  textMuted: '#94A3B8',
  border: '#27314A',
  primary: '#818CF8',
  primarySoft: '#262A55',
  onPrimary: '#0B0F1A',
  income: '#34D399',
  incomeSoft: '#123528',
  expense: '#F87171',
  expenseSoft: '#3A1B1F',
  warning: '#FBBF24',
  warningSoft: '#3A2C10',
  transfer: '#22D3EE',
  overlay: 'rgba(2, 6, 23, 0.65)',
};

export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 24,
  xxl: 32,
} as const;

export const radius = {
  sm: 8,
  md: 12,
  lg: 18,
  xl: 26,
  pill: 999,
} as const;

export const fontSize = {
  xs: 11,
  sm: 13,
  md: 15,
  lg: 17,
  xl: 22,
  xxl: 30,
  display: 38,
} as const;

/** Colores disponibles al crear cuentas y categorías propias. */
export const swatches = [
  '#4F46E5',
  '#7C3AED',
  '#DB2777',
  '#E23D3D',
  '#EA580C',
  '#D97706',
  '#65A30D',
  '#0F9D58',
  '#0D9488',
  '#0284C7',
  '#2563EB',
  '#64748B',
];
