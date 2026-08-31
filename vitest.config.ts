import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const clientApp = resolve(__dirname, 'src/Qaly.Web/ClientApp')

// Frontend unit tests. Deliberately separate from `vite.config.ts`: the build config
// pins `process.env.NODE_ENV` to "production", which would strip Vue's dev-only
// warnings that these tests rely on.
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': clientApp,
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['tests/client/**/*.spec.ts'],
    setupFiles: ['tests/client/setup.ts'],
    restoreMocks: true,
    unstubGlobals: true,
    unstubEnvs: true,
    coverage: {
      provider: 'v8',
      reportsDirectory: 'TestResults/client-coverage',
      reporter: ['text-summary', 'lcov'],
      include: [
        'src/Qaly.Web/ClientApp/utils/**/*.ts',
        'src/Qaly.Web/ClientApp/composables/**/*.ts',
        'src/Qaly.Web/ClientApp/components/**/*-models.ts',
      ],
      exclude: ['src/Qaly.Web/ClientApp/utils/vendor/**'],
    },
  },
})
