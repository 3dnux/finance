import React, { useState } from 'react';
import { Alert, Share, View } from 'react-native';

import { TextField } from '@/components/Field';
import { Screen } from '@/components/Screen';
import { AppText, Button, Card, IconCircle } from '@/components/ui';
import { exportState, parseImport } from '@/store/persistence';
import { useFinance } from '@/store/FinanceProvider';
import { spacing, useTheme } from '@/theme';

export default function BackupScreen() {
  const { colors } = useTheme();
  const { state, replaceAll } = useFinance();
  const [importText, setImportText] = useState('');
  const [busy, setBusy] = useState(false);

  const share = async () => {
    setBusy(true);
    try {
      await Share.share({
        title: 'Copia de seguridad de Finanzas',
        message: exportState(state),
      });
    } catch {
      Alert.alert('No se pudo compartir', 'Inténtalo de nuevo desde otra app.');
    } finally {
      setBusy(false);
    }
  };

  const runImport = () => {
    let parsed;
    try {
      parsed = parseImport(importText);
    } catch (error) {
      Alert.alert('Copia no válida', error instanceof Error ? error.message : 'No se pudo leer el contenido.');
      return;
    }

    Alert.alert(
      'Restaurar copia',
      `Se sustituirán tus datos actuales por ${parsed.transactions.length} movimientos y ${parsed.accounts.length} cuentas.`,
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Restaurar',
          style: 'destructive',
          onPress: () => {
            replaceAll(parsed);
            setImportText('');
            Alert.alert('Listo', 'Tus datos se han restaurado.');
          },
        },
      ],
    );
  };

  return (
    <Screen contentStyle={{ paddingTop: spacing.lg, gap: spacing.lg }}>
      <Card>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
          <IconCircle name="shield-checkmark-outline" color={colors.income} />
          <View style={{ flex: 1 }}>
            <AppText weight="700">Tus datos son tuyos</AppText>
            <AppText variant="caption" muted>
              Todo se guarda en el propio dispositivo. No hay cuentas ni servidores.
            </AppText>
          </View>
        </View>
      </Card>

      <View style={{ gap: spacing.sm }}>
        <AppText variant="label" muted>
          Exportar
        </AppText>
        <AppText variant="caption" muted>
          Genera un JSON con {state.transactions.length} movimientos, {state.accounts.length} cuentas y{' '}
          {state.budgets.length} presupuestos. Guárdalo donde prefieras (notas, correo, archivos).
        </AppText>
        <Button
          label="Compartir copia de seguridad"
          icon="share-outline"
          fullWidth
          loading={busy}
          onPress={() => void share()}
        />
      </View>

      <View style={{ gap: spacing.sm }}>
        <AppText variant="label" muted>
          Importar
        </AppText>
        <TextField
          label="Pega aquí el contenido de una copia"
          value={importText}
          onChangeText={setImportText}
          placeholder='{"accounts": [...], "transactions": [...]}'
          multiline
        />
        <Button
          label="Restaurar datos"
          icon="cloud-upload-outline"
          variant="secondary"
          fullWidth
          disabled={!importText.trim()}
          onPress={runImport}
        />
        <AppText variant="caption" muted>
          Importar sustituye por completo los datos actuales. Exporta antes si quieres conservarlos.
        </AppText>
      </View>
    </Screen>
  );
}
