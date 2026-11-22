import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // The repo lives on a Windows drive mounted into WSL, where file change events do not arrive.
    watch: { usePolling: true, interval: 500 },
    proxy: {
      '/api': 'http://localhost:5090',
    },
  },
})
