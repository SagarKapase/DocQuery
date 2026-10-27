import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// "/api" calls are forwarded to the .NET backend, so no CORS setup is needed.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5016',
    },
  },
})
