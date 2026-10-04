import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    vue(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg'],
      manifest: {
        name: 'Nutriólogo',
        short_name: 'Nutriólogo',
        description: 'Asistente de nutrición',
        theme_color: '#ffffff',
        icons: [],
      },
    }),
  ],
  server: {
    proxy: { '/api': { target: 'http://localhost:5080', changeOrigin: true, rewrite: (p) => p.replace(/^\/api/, '') } },
  },
})
