import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        // Rewrite cookie domains coming from the backend so the browser will
        // accept Set-Cookie when the backend sets cookies for its own host.
        // Use the object form which is more explicit and reliable across
        // environments. Also rewrite path to '/' to ensure cookie applies.
        cookieDomainRewrite: { '*': '' },
        cookiePathRewrite: '/'
        ,
        // Ensure the original Cookie header from the browser is forwarded to
        // the backend. Some proxy environments do not forward cookies by
        // default; adding this configure hook forces the header through.
        configure: (proxy) => {
          if (proxy && typeof proxy.on === 'function') {
            proxy.on('proxyReq', (proxyReq, req) => {
              try {
                const origCookie = req && req.headers && (req.headers['cookie'] || req.headers.cookie)
                if (origCookie) {
                  proxyReq.setHeader('cookie', origCookie)
                }
              } catch {}
            })
          }
        }
      }
    }
  }
})
