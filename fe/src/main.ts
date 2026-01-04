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
import { useToast } from './services/toastService'
import { errorTracking } from './services/errorTracking'

const app = createApp(App)

// Global error handler - prevents white screen of death
app.config.errorHandler = (err, instance, info) => {
  // Track error for debugging and monitoring
  errorTracking.captureException(err, {
    component: instance?.$options?.name || 'Unknown',
    operation: 'vueErrorHandler',
    metadata: { info }
  })
  
  // Show user-friendly error message
  try {
    const toast = useToast()
    toast.error('Unexpected Error', 'Something went wrong. Please refresh the page.')
  } catch (e) {
    // Fallback if toast service fails
    alert('An unexpected error occurred. Please refresh the page.')
  }
}

// Handle unhandled promise rejections
window.addEventListener('unhandledrejection', (event) => {
  errorTracking.captureException(event.reason, {
    component: 'Global',
    operation: 'unhandledRejection'
  })
  event.preventDefault()
  
  try {
    const toast = useToast()
    toast.error('Error', 'An unexpected error occurred.')
  } catch (err) {
    // Fallback if toast service fails
    errorTracking.captureException(err as Error, {
      component: 'Global',
      operation: 'toastServiceFallback'
    })
  }
})

app.use(createPinia())
app.use(router)

app.mount('#app')

// Web Vitals - Performance monitoring (production only)
// NOTE: Analytics integration point - when adding analytics service (Google Analytics, Plausible, etc.),
// send these metrics to your analytics platform for performance tracking.
if (import.meta.env.PROD) {
  import('web-vitals').then(({ onCLS, onINP, onFCP, onLCP, onTTFB }) => {
    // Core Web Vitals - Good thresholds: CLS < 0.1, INP < 200ms, LCP < 2.5s
    onCLS((metric) => {
      // Cumulative Layout Shift - measures visual stability
      if (import.meta.env.DEV) {
        console.log('[Web Vitals] CLS:', metric.value)
      }
      // Analytics integration: analyticsService.trackMetric('CLS', metric.value)
    })
    
    onINP((metric) => {
      // Interaction to Next Paint - measures responsiveness
      if (import.meta.env.DEV) {
        console.log('[Web Vitals] INP:', metric.value, 'ms')
      }
      // Analytics integration: analyticsService.trackMetric('INP', metric.value)
    })
    
    onLCP((metric) => {
      // Largest Contentful Paint - measures loading performance
      if (import.meta.env.DEV) {
        console.log('[Web Vitals] LCP:', metric.value, 'ms')
      }
      // Analytics integration: analyticsService.trackMetric('LCP', metric.value)
    })
    
    // Additional metrics
    onFCP((metric) => {
      // First Contentful Paint - measures perceived load speed
      if (import.meta.env.DEV) {
        console.log('[Web Vitals] FCP:', metric.value, 'ms')
      }
      // Analytics integration: analyticsService.trackMetric('FCP', metric.value)
    })
    
    onTTFB((metric) => {
      // Time to First Byte - measures server response time
      if (import.meta.env.DEV) {
        console.log('[Web Vitals] TTFB:', metric.value, 'ms')
      }
      // Analytics integration: analyticsService.trackMetric('TTFB', metric.value)
    })
  }).catch((err) => {
    errorTracking.captureException(err as Error, {
      component: 'WebVitals',
      operation: 'loadModule'
    })
  })
}
