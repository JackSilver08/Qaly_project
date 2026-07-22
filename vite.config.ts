import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  base: '/dist/',
  publicDir: resolve(__dirname, 'src/Qaly.Web/ClientApp/public'),
  plugins: [vue({
    features: {
      componentIdGenerator: 'filepath',
    },
  })],
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src/Qaly.Web/ClientApp'),
    },
  },
  build: {
    outDir: resolve(__dirname, 'src/Qaly.Web/wwwroot/dist'),
    emptyOutDir: true,
    manifest: true,
    chunkSizeWarningLimit: 1000,
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'src/Qaly.Web/ClientApp/main.ts'),
      },
      output: {
        entryFileNames: 'assets/[name].js',
        chunkFileNames: 'assets/[name].js',
        assetFileNames: 'assets/[name].[ext]',
        manualChunks: {
          'vendor-vue': ['vue', 'vue-router'],
          'vendor-ui': ['vue-draggable-plus', 'lucide-vue-next'],
          'vendor-markdown': ['md-editor-v3'],
          'vendor-realtime': ['@microsoft/signalr'],
        },
      },
      onwarn(warning, defaultHandler) {
        const warningPath = warning.id?.replace(/\\/g, '/')
        if (
          warning.code === 'INVALID_ANNOTATION' &&
          warningPath?.includes('node_modules/@microsoft/signalr/')
        ) {
          return
        }

        defaultHandler(warning)
      },
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    hmr: {
      protocol: 'ws',
    },
  },
})
