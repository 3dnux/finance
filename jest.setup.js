// AsyncStorage necesita su mock oficial: en tests no hay módulo nativo detrás.
jest.mock('@react-native-async-storage/async-storage', () =>
  require('@react-native-async-storage/async-storage/jest/async-storage-mock'),
);
