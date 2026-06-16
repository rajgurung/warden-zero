import { defineConfig } from 'vite';

// Relative base so the build works whether served at a domain root or
// embedded under a subpath (e.g. iframed into the portfolio /game route).
export default defineConfig({
  base: './',
  build: {
    target: 'es2020',
    sourcemap: false,
  },
});
