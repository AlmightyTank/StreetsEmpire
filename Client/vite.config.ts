import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

/*
  The version, from the same file the server reads.

  It was typed into five places by hand - here in package.json, in the sidebar below, in the README
  title, in the health endpoint and in VERSION itself. They agreed, which is what hand-copied numbers
  do until the day somebody bumps four of them. Baked in at build time rather than fetched, because a
  number in the corner of the page is not worth a request.
*/
const version = readFileSync(fileURLToPath(new URL('../VERSION', import.meta.url)), 'utf8').trim()

export default defineConfig({
  define: { __APP_VERSION__: JSON.stringify(version) },
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
