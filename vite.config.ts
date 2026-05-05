import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  plugins: [vue()],
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  resolve: {
    preserveSymlinks: true,
    alias: {
      '@': path.resolve(__dirname, './src/Qaly.Web/ClientApp'),
    },
  },
  build: {
    outDir: './src/Qaly.Web/wwwroot/dist',
    emptyOutDir: true,
    manifest: true,
    rollupOptions: {
      input: {
        main: './src/Qaly.Web/ClientApp/main.ts',
      },
      output: {
        entryFileNames: `assets/[name].js`,
        chunkFileNames: `assets/[name].js`,
        assetFileNames: `assets/[name].[ext]`
      },
      onwarn(warning, warn) {
  const message = warning.message || ''

  if (
    message.includes('/*#__PURE__*/') &&
    message.includes('@microsoft/signalr')
  ) {
    return
  }

  warn(warning)
}
    }
  },
  server: {
    port: 5173,
    strictPort: true,
    hmr: {
      protocol: 'ws',
    },
  },
})
