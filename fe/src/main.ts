/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

import './assets/main.css'
// Restore legacy Phosphor CSS classes (e.g. <i class="ph ph-grid-four">)
// This provides the `.ph` + `.ph-<name>` mappings that many templates use.
// We keep component-based `@phosphor-icons/vue` for new code, but
// re-importing the web CSS ensures existing markup still displays icons.
// Legacy web font import removed now that components are used everywhere.

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'

const app = createApp(App)

// Global error handler - prevents white screen of death
app.config.errorHandler = (err, instance, info) => {
  console.error('[Vue Error]', err, info)
  
  // Show user-friendly error message
  import('./services/toastService').then(({ useToast }) => {
    const toast = useToast()
    toast.error('Unexpected Error', 'Something went wrong. Please refresh the page.')
  }).catch(() => {
    // Fallback if toast service fails
    alert('An unexpected error occurred. Please refresh the page.')
  })
  
  // TODO: Send to error tracking service (e.g., Sentry)
  // if (import.meta.env.PROD) {
  //   trackError(err, { component: instance?.$options.name, info })
  // }
}

// Handle unhandled promise rejections
window.addEventListener('unhandledrejection', (event) => {
  console.error('[Unhandled Promise]', event.reason)
  event.preventDefault()
  
  import('./services/toastService').then(({ useToast }) => {
    const toast = useToast()
    toast.error('Error', 'An unexpected error occurred.')
  }).catch(() => {
    // Fallback if toast service fails
    console.error('Toast service unavailable for unhandled rejection')
  })
})

app.use(createPinia())
app.use(router)

app.mount('#app')

// Web Vitals - Performance monitoring (production only)
if (import.meta.env.PROD) {
  import('web-vitals').then(({ onCLS, onINP, onFCP, onLCP, onTTFB }) => {
    // Core Web Vitals
    onCLS((metric) => {
      console.log('[Web Vitals] CLS (Cumulative Layout Shift):', metric.value)
      // TODO: Send to analytics service
    })
    
    onINP((metric) => {
      console.log('[Web Vitals] INP (Interaction to Next Paint):', metric.value, 'ms')
      // TODO: Send to analytics service
    })
    
    onLCP((metric) => {
      console.log('[Web Vitals] LCP (Largest Contentful Paint):', metric.value, 'ms')
      // TODO: Send to analytics service
    })
    
    // Additional metrics
    onFCP((metric) => {
      console.log('[Web Vitals] FCP (First Contentful Paint):', metric.value, 'ms')
      // TODO: Send to analytics service
    })
    
    onTTFB((metric) => {
      console.log('[Web Vitals] TTFB (Time to First Byte):', metric.value, 'ms')
      // TODO: Send to analytics service
    })
  }).catch((err) => {
    console.warn('[Web Vitals] Failed to load:', err)
  })
}
