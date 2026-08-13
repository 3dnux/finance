import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import { Alert, Switch, View } from 'react-native';

import { CatBadge } from '@/components/CatFace';
import { Screen, ScreenHeader } from '@/components/Screen';
import { Sheet } from '@/components/Sheet';
import { AppText, Card, Divider, IconCircle, ListRow, Segmented } from '@/components/ui';
import { CURRENCIES, currencyInfo, formatMoney } from '@/lib/money';
import { useFinance } from '@/store/FinanceProvider';
import { spacing, useTheme } from '@/theme';
import type { Settings } from '@/types';

export default function SettingsScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const { state, updateSettings, resetAll, loadSampleData } = useFinance();
  const { settings, accounts, categories, transactions, budgets } = state;
  const [currencyOpen, setCurrencyOpen] = useState(false);

  const currency = currencyInfo(settings.currency);

  const confirmReset = () => {
    Alert.alert(
      'Borrar todos los datos',
      'Se eliminarán cuentas, movimientos y presupuestos de este dispositivo. Esta acción no se puede deshacer.',
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Borrar todo', style: 'destructive', onPress: () => void resetAll() },
      ],
    );
  };

  const confirmSample = () => {
    Alert.alert(
      'Cargar datos de ejemplo',
      'Se sustituirán tus datos actuales por un ejemplo con tres meses de movimientos.',
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Cargar', onPress: loadSampleData },
      ],
    );
  };

  return (
    <Screen contentStyle={{ paddingTop: spacing.sm }}>
      <ScreenHeader title="Ajustes" subtitle="Personaliza la app a tu gusto" />

      {/* Apariencia */}
      <AppText variant="label" muted style={{ marginBottom: spacing.sm }}>
        Apariencia
      </AppText>
      <Card>
        <AppText variant="caption" muted style={{ marginBottom: spacing.sm }}>
          Tema
        </AppText>
        <Segmented<Settings['themeMode']>
          value={settings.themeMode}
          onChange={(themeMode) => updateSettings({ themeMode })}
          options={[
            { value: 'system', label: 'Sistema' },
            { value: 'light', label: 'Claro' },
            { value: 'dark', label: 'Oscuro' },
          ]}
        />

        <Divider />

        <ListRow
          title="Gatito de reacciones"
          subtitle={settings.mascot ? 'Reacciona a ingresos y gastos' : 'Desactivado'}
          left={<CatBadge mood={settings.mascot ? 'happy' : 'neutral'} size={38} />}
          right={
            <Switch
              accessibilityLabel="Gatito de reacciones"
              value={settings.mascot}
              onValueChange={(mascot) => updateSettings({ mascot })}
              trackColor={{ true: colors.primary, false: colors.border }}
            />
          }
        />
      </Card>

      {/* Datos */}
      <AppText variant="label" muted style={{ marginTop: spacing.xl, marginBottom: spacing.sm }}>
        Tus datos
      </AppText>
      <Card padded={false} style={{ paddingHorizontal: spacing.lg }}>
        <ListRow
          title="Moneda"
          subtitle={`${currency.name} (${currency.symbol})`}
          left={<IconCircle name="cash-outline" color={colors.income} size={38} />}
          right={<AppText muted>{currency.code}</AppText>}
          onPress={() => setCurrencyOpen(true)}
        />
        <Divider />
        <ListRow
          title="Cuentas"
          subtitle={`${accounts.length} cuenta${accounts.length === 1 ? '' : 's'}`}
          left={<IconCircle name="wallet-outline" color={colors.primary} size={38} />}
          chevron
          onPress={() => router.push('/accounts')}
        />
        <Divider />
        <ListRow
          title="Categorías"
          subtitle={`${categories.length} categorías`}
          left={<IconCircle name="pricetags-outline" color={colors.transfer} size={38} />}
          chevron
          onPress={() => router.push('/categories')}
        />
        <Divider />
        <ListRow
          title="Copia de seguridad"
          subtitle="Exportar o importar tus datos"
          left={<IconCircle name="cloud-download-outline" color={colors.warning} size={38} />}
          chevron
          onPress={() => router.push('/data/backup')}
        />
      </Card>

      {/* Resumen */}
      <Card style={{ marginTop: spacing.md }}>
        <AppText variant="label" muted>
          En este dispositivo
        </AppText>
        <View style={{ flexDirection: 'row', marginTop: spacing.md }}>
          {[
            { label: 'Movimientos', value: String(transactions.length) },
            { label: 'Presupuestos', value: String(budgets.length) },
            {
              label: 'Registrado',
              value: formatMoney(
                transactions.filter((t) => t.type === 'expense').reduce((s, t) => s + t.amount, 0),
                settings.currency,
                { hideDecimals: true },
              ),
            },
          ].map((item) => (
            <View key={item.label} style={{ flex: 1 }}>
              <AppText weight="800">{item.value}</AppText>
              <AppText variant="caption" muted>
                {item.label}
              </AppText>
            </View>
          ))}
        </View>
      </Card>

      {/* Zona peligrosa */}
      <AppText variant="label" muted style={{ marginTop: spacing.xl, marginBottom: spacing.sm }}>
        Avanzado
      </AppText>
      <Card padded={false} style={{ paddingHorizontal: spacing.lg }}>
        <ListRow
          title="Cargar datos de ejemplo"
          subtitle="Tres meses de movimientos para probar"
          left={<IconCircle name="flask-outline" color={colors.transfer} size={38} />}
          onPress={confirmSample}
        />
        <Divider />
        <ListRow
          title="Borrar todos los datos"
          subtitle="Empezar de cero"
          left={<IconCircle name="trash-outline" color={colors.expense} size={38} />}
          danger
          onPress={confirmReset}
        />
      </Card>

      <View style={{ alignItems: 'center', marginTop: spacing.xl, gap: 4 }}>
        <AppText variant="caption" muted>
          Finanzas · versión 1.0.0
        </AppText>
        <AppText variant="caption" muted>
          Todos los datos se guardan solo en tu dispositivo.
        </AppText>
      </View>

      <Sheet visible={currencyOpen} onClose={() => setCurrencyOpen(false)} title="Elegir moneda">
        {CURRENCIES.map((item) => (
          <ListRow
            key={item.code}
            title={item.name}
            subtitle={`${item.code} · ${item.symbol}`}
            left={<IconCircle name="cash-outline" color={colors.income} size={38} />}
            right={
              item.code === settings.currency ? <Ionicons name="checkmark" size={20} color={colors.primary} /> : undefined
            }
            onPress={() => {
              updateSettings({ currency: item.code });
              setCurrencyOpen(false);
            }}
          />
        ))}
      </Sheet>
    </Screen>
  );
}
