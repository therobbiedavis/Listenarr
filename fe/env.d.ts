/// <reference types="vite/client" />

// Provide a module declaration for Vue SFCs so TypeScript can import `.vue` files.
// This avoids "Cannot find module './Foo.vue'" errors during `tsc` checks.
declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent
  export default component
}
