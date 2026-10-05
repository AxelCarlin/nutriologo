import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'
import { resolve } from 'path'

export default defineConfig({
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
    },
  },
  plugins: [
    vue(),
    VitePWA({
      // Sin prompt de "nueva versión": el SW se actualiza solo en segundo plano.
      // Nada de UI de update que mantener.
      registerType: 'autoUpdate',
      // manifest.json en vez de .webmanifest: el mime.types del nginx:alpine
      // no mapea .webmanifest (llegaba como application/octet-stream).
      // .json sí mapea a application/json sin tocar nginx.
      manifestFilename: 'manifest.json',
      includeAssets: ['favicon.svg', 'icons.svg', 'apple-touch-icon-180.png'],
      manifest: {
        name: 'Nutriólogo',
        short_name: 'Nutriólogo',
        description: 'Asistente de nutrición con IA. Registro de comidas, mediciones y balance.',
        theme_color: '#000000',
        background_color: '#000000',
        display: 'standalone',
        orientation: 'portrait',
        scope: '/',
        start_url: '/',
        lang: 'es-MX',
        dir: 'ltr',
        categories: ['health', 'fitness', 'lifestyle'],
        icons: [
          {
            src: 'pwa-192.png',
            sizes: '192x192',
            type: 'image/png',
          },
          {
            src: 'pwa-512.png',
            sizes: '512x512',
            type: 'image/png',
          },
          {
            src: 'pwa-maskable-512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
      workbox: {
        // SPA: sin esto, abrir /chat sin red da el error de nginx en vez del shell.
        navigateFallback: 'index.html',
        runtimeCaching: [
          {
            // Los datos de salud NO se cachean: sin red la app abre y muestra
            // estado, no datos inventados (fail-closed).
            urlPattern: ({ url }) => url.pathname.startsWith('/api/'),
            handler: 'NetworkOnly',
          },
          {
            urlPattern: ({ url }) => url.origin === 'https://fonts.googleapis.com',
            handler: 'StaleWhileRevalidate',
            options: { cacheName: 'google-fonts-css' },
          },
          {
            urlPattern: ({ url }) => url.origin === 'https://fonts.gstatic.com',
            handler: 'CacheFirst',
            options: {
              cacheName: 'google-fonts-files',
              expiration: { maxEntries: 30, maxAgeSeconds: 60 * 60 * 24 * 365 },
            },
          },
        ],
      },
    }),
  ],
})
