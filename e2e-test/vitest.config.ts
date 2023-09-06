// eslint-disable-next-line import/no-extraneous-dependencies
import { defineConfig } from 'vite';

export default defineConfig({
  test: {
    testTimeout: 100000,
    hookTimeout: 100000,
  },
});
