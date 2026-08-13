import React from 'react';
import { View } from 'react-native';
import Svg, { Circle, G, Line, Path, Rect, Text as SvgText } from 'react-native-svg';

import { formatCompact } from '@/lib/money';
import { fontSize, radius, spacing, useTheme } from '@/theme';

import { AppText } from './ui';

export interface Slice {
  key: string;
  label: string;
  value: number;
  color: string;
}

/** Coordenada del borde del círculo para un ángulo dado (0 = arriba). */
function polar(cx: number, cy: number, r: number, angle: number) {
  const rad = ((angle - 90) * Math.PI) / 180;
  return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
}

function arcPath(cx: number, cy: number, outer: number, inner: number, start: number, end: number): string {
  const largeArc = end - start > 180 ? 1 : 0;
  const p1 = polar(cx, cy, outer, start);
  const p2 = polar(cx, cy, outer, end);
  const p3 = polar(cx, cy, inner, end);
  const p4 = polar(cx, cy, inner, start);
  return [
    `M ${p1.x} ${p1.y}`,
    `A ${outer} ${outer} 0 ${largeArc} 1 ${p2.x} ${p2.y}`,
    `L ${p3.x} ${p3.y}`,
    `A ${inner} ${inner} 0 ${largeArc} 0 ${p4.x} ${p4.y}`,
    'Z',
  ].join(' ');
}

export function DonutChart({
  slices,
  size = 180,
  thickness = 26,
  centerLabel,
  centerValue,
}: {
  slices: Slice[];
  size?: number;
  thickness?: number;
  centerLabel?: string;
  centerValue?: string;
}) {
  const { colors } = useTheme();
  const total = slices.reduce((sum, s) => sum + s.value, 0);
  const cx = size / 2;
  const cy = size / 2;
  const outer = size / 2;
  const inner = outer - thickness;

  let cursor = 0;
  const arcs = slices
    .filter((slice) => slice.value > 0)
    .map((slice) => {
      const sweep = (slice.value / total) * 360;
      // Un hueco de 1.5° separa visualmente los sectores contiguos.
      const start = cursor;
      const end = cursor + sweep;
      cursor = end;
      return { slice, start, end: Math.max(start + 0.5, end - 1.5) };
    });

  return (
    <View style={{ width: size, height: size, alignItems: 'center', justifyContent: 'center' }}>
      <Svg width={size} height={size}>
        {total === 0 ? (
          <Circle cx={cx} cy={cy} r={(outer + inner) / 2} stroke={colors.surfaceAlt} strokeWidth={thickness} fill="none" />
        ) : (
          <G>
            {arcs.map(({ slice, start, end }) => (
              <Path key={slice.key} d={arcPath(cx, cy, outer, inner, start, end)} fill={slice.color} />
            ))}
          </G>
        )}
      </Svg>
      <View style={{ position: 'absolute', alignItems: 'center' }}>
        {centerValue ? (
          <AppText variant="subtitle" weight="800">
            {centerValue}
          </AppText>
        ) : null}
        {centerLabel ? (
          <AppText variant="caption" muted>
            {centerLabel}
          </AppText>
        ) : null}
      </View>
    </View>
  );
}

export interface BarGroup {
  label: string;
  values: { value: number; color: string }[];
  highlighted?: boolean;
}

/** Barras agrupadas (ingresos vs gastos) con eje de referencia. */
export function GroupedBarChart({
  groups,
  height = 170,
  currency = 'EUR',
}: {
  groups: BarGroup[];
  height?: number;
  currency?: string;
}) {
  const { colors } = useTheme();
  const max = Math.max(1, ...groups.flatMap((g) => g.values.map((v) => v.value)));
  const chartHeight = height - 26;

  return (
    <View>
      <View style={{ flexDirection: 'row', alignItems: 'flex-end', height: chartHeight, gap: spacing.sm }}>
        {groups.map((group, groupIndex) => (
          <View key={`${group.label}-${groupIndex}`} style={{ flex: 1, alignItems: 'center', gap: 3 }}>
            <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: 3, height: chartHeight - 18 }}>
              {group.values.map((bar, index) => {
                const barHeight = Math.max(bar.value > 0 ? 4 : 2, (bar.value / max) * (chartHeight - 22));
                return (
                  <View
                    key={index}
                    style={{
                      width: 12,
                      height: barHeight,
                      borderRadius: radius.sm,
                      backgroundColor: bar.value > 0 ? bar.color : colors.surfaceAlt,
                      opacity: group.highlighted === false ? 0.55 : 1,
                    }}
                  />
                );
              })}
            </View>
          </View>
        ))}
      </View>
      <View style={{ flexDirection: 'row', gap: spacing.sm, marginTop: 6 }}>
        {groups.map((group, index) => (
          <View key={`${group.label}-label-${index}`} style={{ flex: 1, alignItems: 'center' }}>
            <AppText variant="caption" muted={!group.highlighted} weight={group.highlighted ? '700' : '500'}>
              {group.label}
            </AppText>
          </View>
        ))}
      </View>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginTop: spacing.xs }}>
        <AppText variant="caption" muted>
          Máximo del periodo
        </AppText>
        <AppText variant="caption" muted>
          {formatCompact(max, currency)}
        </AppText>
      </View>
    </View>
  );
}

/** Gráfico de líneas simple para la evolución del saldo. */
export function LineChart({
  points,
  labels,
  width,
  height = 150,
  color,
  currency = 'EUR',
}: {
  points: number[];
  labels: string[];
  width: number;
  height?: number;
  color?: string;
  currency?: string;
}) {
  const { colors } = useTheme();
  const stroke = color ?? colors.primary;
  // Los laterales dejan sitio a las etiquetas de los extremos, que van centradas
  // bajo su punto y se recortarían con un margen menor.
  const padding = { top: 16, right: 22, bottom: 22, left: 22 };
  const innerW = Math.max(1, width - padding.left - padding.right);
  const innerH = Math.max(1, height - padding.top - padding.bottom);

  const max = Math.max(...points, 0);
  const min = Math.min(...points, 0);
  const span = max - min || 1;

  const coords = points.map((value, index) => ({
    x: padding.left + (points.length === 1 ? innerW / 2 : (index / (points.length - 1)) * innerW),
    y: padding.top + innerH - ((value - min) / span) * innerH,
  }));

  const line = coords.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x} ${c.y}`).join(' ');
  const area = `${line} L ${coords[coords.length - 1]?.x ?? 0} ${padding.top + innerH} L ${coords[0]?.x ?? 0} ${
    padding.top + innerH
  } Z`;
  const zeroY = padding.top + innerH - ((0 - min) / span) * innerH;

  return (
    <Svg width={width} height={height}>
      <Rect x={0} y={0} width={width} height={height} fill="transparent" />
      {min < 0 ? (
        <Line
          x1={padding.left}
          y1={zeroY}
          x2={width - padding.right}
          y2={zeroY}
          stroke={colors.border}
          strokeDasharray="4 4"
          strokeWidth={1}
        />
      ) : null}
      <Path d={area} fill={stroke} opacity={0.12} />
      <Path d={line} stroke={stroke} strokeWidth={2.5} fill="none" strokeLinejoin="round" strokeLinecap="round" />
      {coords.map((c, i) => (
        <Circle key={i} cx={c.x} cy={c.y} r={i === coords.length - 1 ? 4.5 : 2.5} fill={stroke} />
      ))}
      {labels.map((label, i) => (
        <SvgText
          key={`${label}-${i}`}
          x={coords[i]?.x ?? 0}
          y={height - 6}
          fill={colors.textMuted}
          fontSize={fontSize.xs}
          textAnchor="middle"
        >
          {label}
        </SvgText>
      ))}
      <SvgText x={padding.left} y={12} fill={colors.textMuted} fontSize={fontSize.xs}>
        {formatCompact(max, currency)}
      </SvgText>
    </Svg>
  );
}
