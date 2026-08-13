import { amountToInput, formatCompact, formatInputDisplay, formatMoney, parseAmount } from '@/lib/money';

describe('formatMoney', () => {
  it('formatea con separador de miles y coma decimal', () => {
    expect(formatMoney(123456, 'EUR')).toBe('1.234,56 €');
    expect(formatMoney(0, 'EUR')).toBe('0,00 €');
    expect(formatMoney(5, 'EUR')).toBe('0,05 €');
  });

  it('coloca el símbolo delante en monedas que lo requieren', () => {
    expect(formatMoney(250000, 'USD')).toBe('$2.500,00');
    expect(formatMoney(250000, 'MXN')).toBe('$2.500,00');
  });

  it('mantiene el signo negativo antes del símbolo', () => {
    expect(formatMoney(-4500, 'EUR')).toBe('−45,00 €');
    expect(formatMoney(-4500, 'USD')).toBe('−$45,00');
  });

  it('admite ocultar decimales y forzar el signo positivo', () => {
    // Sin decimales se redondea al entero más cercano, no se trunca.
    expect(formatMoney(123456, 'EUR', { hideDecimals: true })).toBe('1.235 €');
    expect(formatMoney(123400, 'EUR', { hideDecimals: true })).toBe('1.234 €');
    expect(formatMoney(123456, 'EUR', { signed: true })).toBe('+1.234,56 €');
    expect(formatMoney(123456, 'EUR', { noSymbol: true })).toBe('1.234,56');
  });

  it('agrupa millones correctamente', () => {
    expect(formatMoney(123456789, 'EUR')).toBe('1.234.567,89 €');
  });
});

describe('parseAmount', () => {
  it('convierte texto del teclado a céntimos', () => {
    expect(parseAmount('12,34')).toBe(1234);
    expect(parseAmount('12')).toBe(1200);
    expect(parseAmount('12,5')).toBe(1250);
    expect(parseAmount('1.234,56')).toBe(123456);
  });

  it('devuelve 0 ante entradas vacías o inválidas', () => {
    expect(parseAmount('')).toBe(0);
    expect(parseAmount(',')).toBe(0);
    expect(parseAmount('abc')).toBe(0);
    expect(parseAmount('-5')).toBe(0);
  });

  it('no pierde precisión con decimales problemáticos en coma flotante', () => {
    expect(parseAmount('0,07')).toBe(7);
    expect(parseAmount('1,10')).toBe(110);
    expect(parseAmount('8,29')).toBe(829);
  });
});

describe('amountToInput', () => {
  it('es la operación inversa de parseAmount', () => {
    expect(amountToInput(1234)).toBe('12,34');
    expect(amountToInput(1200)).toBe('12');
    expect(amountToInput(1250)).toBe('12,5');
    expect(amountToInput(0)).toBe('');
    expect(parseAmount(amountToInput(98765))).toBe(98765);
  });
});

describe('formatInputDisplay', () => {
  it('muestra el importe en curso con separadores', () => {
    expect(formatInputDisplay('')).toBe('0');
    expect(formatInputDisplay('1234')).toBe('1.234');
    expect(formatInputDisplay('1234,5')).toBe('1.234,5');
    expect(formatInputDisplay('12,')).toBe('12,');
  });
});

describe('formatCompact', () => {
  it('abrevia importes grandes para los ejes', () => {
    expect(formatCompact(50000, 'EUR')).toBe('500 €');
    expect(formatCompact(150000, 'EUR')).toBe('1,5k €');
    expect(formatCompact(250000000, 'EUR')).toBe('2,5M €');
  });
});
