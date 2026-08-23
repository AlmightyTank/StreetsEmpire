import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  css: {
    preprocessorOptions: {
      scss: {
        /*
          Bootstrap 5.3 is still written with @import, which Dart Sass has deprecated in favour of
          @use. Silencing the warnings rather than working around them: the rewrite is Bootstrap's to
          do, and until it does, every build would otherwise print several hundred lines of notice
          about files this project does not own.
        */
        silenceDeprecations: ['import', 'global-builtin', 'color-functions'],
        quietDeps: true,
      },
    },
  },
  server: {
    /*
      The harness assigns a free port when more than one session runs this server at once, and hands
      it over as PORT. Vite does not read that variable on its own, so it is read here; 5173 stays
      the default for a plain `npm run dev`.

      Nothing is pinned to whichever port this ends up being: the browser talks to the API through
      the proxy below, which makes those calls same-origin, so there is no CORS allowlist or callback
      URL that has to agree with it.
    */
    port: Number(process.env.PORT) || 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
})
