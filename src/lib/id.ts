/**
 * Identificador corto y único dentro del dispositivo.
 * No necesitamos UUID criptográfico: los datos son locales y sin colisión entre clientes.
 */
export function createId(prefix = ''): string {
  const random = Math.random().toString(36).slice(2, 10);
  const time = Date.now().toString(36);
  return `${prefix}${time}${random}`;
}
