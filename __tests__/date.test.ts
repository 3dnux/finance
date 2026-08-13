import {
  addMonths,
  daysInMonth,
  elapsedDays,
  formatRelativeDay,
  fromISODate,
  isSameMonth,
  lastMonths,
  monthKey,
  monthLabel,
  toISODate,
} from '@/lib/date';

describe('conversión de fechas', () => {
  it('toISODate usa la fecha local, no UTC', () => {
    // 23:30 hora local del día 5 debe seguir siendo el día 5.
    expect(toISODate(new Date(2026, 7, 5, 23, 30))).toBe('2026-08-05');
    expect(toISODate(new Date(2026, 0, 1, 0, 15))).toBe('2026-01-01');
  });

  it('fromISODate y toISODate son inversas', () => {
    expect(toISODate(fromISODate('2026-02-28'))).toBe('2026-02-28');
    expect(toISODate(fromISODate('2026-12-31'))).toBe('2026-12-31');
  });
});

describe('meses', () => {
  it('monthKey extrae YYYY-MM', () => {
    expect(monthKey('2026-08-12')).toBe('2026-08');
    expect(monthKey(new Date(2026, 7, 12))).toBe('2026-08');
  });

  it('addMonths cruza correctamente el cambio de año', () => {
    expect(addMonths('2026-12', 1)).toBe('2027-01');
    expect(addMonths('2026-01', -1)).toBe('2025-12');
    expect(addMonths('2026-08', 5)).toBe('2027-01');
  });

  it('lastMonths termina en la clave indicada', () => {
    expect(lastMonths('2026-03', 4)).toEqual(['2025-12', '2026-01', '2026-02', '2026-03']);
  });

  it('monthLabel traduce al español', () => {
    expect(monthLabel('2026-08')).toBe('Agosto');
    expect(monthLabel('2026-08', { withYear: true })).toBe('Agosto 2026');
    expect(monthLabel('2026-08', { short: true })).toBe('Ago');
  });

  it('daysInMonth contempla los años bisiestos', () => {
    expect(daysInMonth('2026-02')).toBe(28);
    expect(daysInMonth('2028-02')).toBe(29);
    expect(daysInMonth('2026-04')).toBe(30);
    expect(daysInMonth('2026-12')).toBe(31);
  });

  it('isSameMonth compara solo año y mes', () => {
    expect(isSameMonth('2026-08-31', '2026-08')).toBe(true);
    expect(isSameMonth('2026-09-01', '2026-08')).toBe(false);
  });
});

describe('elapsedDays', () => {
  it('un mes pasado cuenta como completo y uno futuro como vacío', () => {
    expect(elapsedDays('2026-07', '2026-08-12')).toBe(31);
    expect(elapsedDays('2026-09', '2026-08-12')).toBe(0);
  });

  it('el mes en curso se corta en el día actual', () => {
    expect(elapsedDays('2026-08', '2026-08-12')).toBe(12);
  });
});

describe('formatRelativeDay', () => {
  it('usa etiquetas relativas para hoy y ayer', () => {
    expect(formatRelativeDay('2026-08-12', '2026-08-12')).toBe('Hoy');
    expect(formatRelativeDay('2026-08-11', '2026-08-12')).toBe('Ayer');
  });

  it('cruza el cambio de mes al calcular «ayer»', () => {
    expect(formatRelativeDay('2026-07-31', '2026-08-01')).toBe('Ayer');
  });

  it('muestra el día de la semana en fechas anteriores', () => {
    expect(formatRelativeDay('2026-08-05', '2026-08-12')).toBe('mié, 5 ago');
  });

  it('añade el año cuando la fecha es de otro año', () => {
    expect(formatRelativeDay('2025-08-05', '2026-08-12')).toBe('mar, 5 ago 2025');
  });
});
