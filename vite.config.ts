import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  base: '/dist/',
  publicDir: 'src/Qaly.Web/ClientApp/public',
  plugins: [vue()],
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
  }, 
  
  resolve: {
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
        assetFileNames: `assets/[name].[ext]`,
        manualChunks: {
          'vendor-vue': ['vue', 'vue-router'],
          'vendor-ui': ['vue-draggable-plus', 'lucide-vue-next'],
          'vendor-markdown': ['markdown-it', 'dompurify'],
          'vendor-realtime': ['@microsoft/signalr'],
        },
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
