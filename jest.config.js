/** Configuración de tests: solo cubrimos la lógica pura de `src/lib` y `src/store`. */
module.exports = {
  preset: 'jest-expo',
  testMatch: ['**/__tests__/**/*.test.ts'],
  setupFiles: ['<rootDir>/jest.setup.js'],
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/src/$1',
  },
};
