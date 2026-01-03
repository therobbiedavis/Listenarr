import { nextTick } from 'vue'
import { logger } from '@/utils/logger'

let lazyObserver: IntersectionObserver | null = null

export function observeLazyImages(selector = 'img.lazy-img', rootMargin = '200px', threshold = 0.01) {
  try {
    const images = Array.from(document.querySelectorAll(selector)) as HTMLImageElement[]
    if (!images || images.length === 0) return

    if ('IntersectionObserver' in window) {
      if (!lazyObserver) {
        lazyObserver = new IntersectionObserver((entries) => {
          entries.forEach(entry => {
            if (entry.isIntersecting) {
              const img = entry.target as HTMLImageElement
              const ds = img.dataset.src
              if (ds) {
                img.src = ds
                img.removeAttribute('data-src')
              }
              lazyObserver?.unobserve(img)
            }
          })
        }, { rootMargin, threshold })
      }

      for (const img of images) {
        if (img.dataset.src) {
          lazyObserver.observe(img)
        }
      }
    } else {
      // Fallback: load immediately
      for (const img of images) {
        if (img.dataset.src) {
          img.src = img.dataset.src
          img.removeAttribute('data-src')
        }
      }
    }
  } catch (e) {
    try { logger.debug('observeLazyImages error', e) } catch {}
  }
}
