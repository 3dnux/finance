# Finanzas — app de finanzas personales para Android y iOS

App móvil para controlar gastos, ingresos, cuentas y presupuestos. Una sola base de
código (React Native + Expo) que se ejecuta en **Android** y **iOS**. Todos los datos
se guardan en el propio dispositivo: no hay servidores, cuentas de usuario ni registro.

## Qué incluye

| Sección | Qué hace |
| --- | --- |
| **Resumen** | Patrimonio total, saldo de cada cuenta, ingresos/gastos del mes, reparto por categoría, presupuestos en riesgo y últimos movimientos. |
| **Movimientos** | Lista agrupada por día con búsqueda, filtros por tipo, cuenta y categoría, y navegación por meses. |
| **Presupuestos** | Límite mensual por categoría, progreso frente al *ritmo* esperado del mes y cuánto queda por día. |
| **Análisis** | Comparativa de ingresos y gastos de los últimos 6 meses, media diaria, variación frente al mes anterior, evolución del balance y desglose por categoría. |
| **Ajustes** | Moneda, tema claro/oscuro/sistema, gestión de cuentas y categorías, copia de seguridad y datos de ejemplo. |

### El gatito

La app tiene una mascota que opina de tus finanzas. Al registrar un movimiento aparece
su cara y reacciona:

| Movimiento | Reacción |
| --- | --- |
| Ingreso | Ojos cerrados de felicidad, sonrisa de gato y mofletes sonrojados, con corazones subiendo. |
| Ingreso grande | Ojos de corazón: se enamora. |
| Gasto | Cejas caídas, pupilas encogidas, boca torcida, tiembla de rabia y le sale la marca de enfado. |
| Gasto grande | Bufido: boca abierta con colmillos, cejas al máximo y nubes de vapor. |
| Transferencia | Ni fu ni fa: el dinero solo cambia de bolsillo. |

En el **Resumen** hay además un gatito permanente que refleja cómo va el mes: se enamora
si ahorras mucho, se enfada si te has pasado de algún presupuesto y se enfurece si gastas
más de lo que ingresas. Está dibujado con SVG (nada de imágenes) y animado con la API
`Animated`; respeta el ajuste de *reducir movimiento* del sistema y se puede apagar del
todo en **Ajustes → Gatito de reacciones**.

Otros detalles:

- **Gastos, ingresos y transferencias** entre cuentas (las transferencias mueven dinero
  sin contar como gasto ni ingreso).
- **Teclado numérico propio** con coma decimal, para registrar un movimiento en segundos.
- **Calendario propio** en español con la semana empezando en lunes.
- **9 monedas** (EUR, USD, MXN, COP, ARS, CLP, PEN, BRL, GBP) con formato español.
- **Tema claro y oscuro**, siguiendo el del sistema por defecto.
- **Copia de seguridad** exportable e importable en JSON.

## Puesta en marcha

```bash
npm install
npx expo start
```

Después:

- **Android/iOS:** escanea el QR con la app *Expo Go*.
- **Android emulador:** `npm run android` · **iOS simulador (macOS):** `npm run ios`.

## Compilar para las tiendas

El proyecto trae `eas.json` listo. Con una cuenta de Expo:

```bash
npm install -g eas-cli
eas login
eas build --platform android   # AAB para Google Play
eas build --platform ios       # IPA para App Store
eas build --platform android --profile preview   # APK para probar
```

Los identificadores de aplicación (`com.finanzas.app`) y el nombre visible se cambian
en `app.json`.

## Calidad

```bash
npm run typecheck   # TypeScript en modo estricto
npm test            # 82 tests de la lógica financiera y de la mascota
```

Los tests cubren la parte donde un error cuesta dinero: conversión y formato de importes,
cálculo de saldos, resúmenes mensuales, presupuestos, filtros, fechas y las reducciones
de estado (incluido qué pasa con los movimientos al borrar una cuenta o una categoría),
más los estados de ánimo del gatito.

## Cómo está organizado

```
app/                      Rutas (expo-router)
  (tabs)/                 Las cinco pestañas
  transaction/[id].tsx    Alta y edición de movimientos ('new' o un id)
  accounts/               Lista y formulario de cuentas
  categories/             Lista y formulario de categorías
  budgets/edit.tsx        Alta y edición de presupuestos
  data/backup.tsx         Exportar e importar
src/
  components/             UI reutilizable, gráficos SVG, teclado, calendario, gatito
  lib/                    Lógica pura: dinero, fechas, selectores, ánimo del gatito
  store/                  Estado global (reducer + persistencia en AsyncStorage)
  theme/                  Colores, espaciados y tipografía
  types/                  Modelo de datos
```

Decisiones que conviene conocer:

- **Los importes se guardan en céntimos** (enteros). Nada de decimales en coma flotante:
  `0,07 € + 0,07 €` nunca acaba en `0,14000000000000001 €`.
- **Las fechas son cadenas `YYYY-MM-DD`** en hora local, así que un gasto a las 23:50 no
  se cuenta en el día siguiente.
- **Los saldos se calculan**, no se almacenan: saldo inicial de la cuenta más el efecto de
  cada movimiento. Editar un movimiento antiguo cuadra las cuentas sin migraciones.
- **El estado es un único objeto** persistido en AsyncStorage con escritura diferida
  (250 ms), suficiente para un uso personal y sin dependencias nativas de base de datos.
- **Nada de librerías de gráficos**: el donut, las barras y la línea están dibujados con
  `react-native-svg` para mantener el paquete pequeño y el estilo consistente.

## Privacidad

La app no pide permisos, no accede a internet y no envía nada a ningún sitio. Si
desinstalas la app, los datos desaparecen con ella: por eso existe la copia de seguridad
en Ajustes.
