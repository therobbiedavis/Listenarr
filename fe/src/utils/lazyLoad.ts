import { nextTick } from 'vue'
import { logger } from '@/utils/logger'

let lazyObserver: IntersectionObserver | null = null

export function resetLazyObserver() {
  try { lazyObserver?.disconnect(); lazyObserver = null } catch {}
}

export function ensureVisibleImagesLoad(selector = 'img.lazy-img', extraMarginPx = 0) {
  try {
    const imgs = Array.from(document.querySelectorAll(selector)) as HTMLImageElement[]
    const margin = Number(extraMarginPx) || 0
    const loaded: HTMLImageElement[] = []
    for (const img of imgs) {
      try {
        const ds = img.dataset.src
        if (!ds) continue
        const rect = img.getBoundingClientRect()
        const inViewport = rect.top < (window.innerHeight + margin) && rect.bottom > -margin && rect.left < (window.innerWidth + margin) && rect.right > -margin
        if (inViewport) {
          try { logger.debug('[lazyLoad] force immediate load (visible after group change)', { src: img.getAttribute('src'), dataSrc: ds, alt: img.alt }) } catch {}
          img.src = ds
          img.removeAttribute('data-src')
          loaded.push(img)
          try { lazyObserver?.unobserve(img) } catch {}
        }
      } catch (e) { /* ignore individual failures */ }
    }

    // Dev-only telemetry: increment a global counter for forced loads to help detect frequent use
    try {
      if (typeof window !== 'undefined' && loaded.length > 0) {
        ;(window as any).__LAZYLOAD_FORCED_COUNT = ((window as any).__LAZYLOAD_FORCED_COUNT || 0) + loaded.length
        logger.debug('[lazyLoad] forced loads count', (window as any).__LAZYLOAD_FORCED_COUNT)
      }
    } catch (e) { /* ignore telemetry failures */ }

    return loaded
  } catch (e) {
    try { logger.debug('[lazyLoad] ensureVisibleImagesLoad error', e) } catch {}
    return []
  }
}

export function observeLazyImages(selector = 'img.lazy-img', rootMargin = '200px', threshold = 0.01) {
  try {
    const images = Array.from(document.querySelectorAll(selector)) as HTMLImageElement[]
    if (!images || images.length === 0) return

// Debug: log found images and their data-src values
    try {
      const srcs = images.slice(0, 20).map(i => ({ src: i.getAttribute('src'), dataSrc: i.dataset.src }))
      logger.debug('[lazyLoad] observeLazyImages found', images.length, 'images', srcs)
    } catch {}

    if ('IntersectionObserver' in window) {
      if (!lazyObserver) {
        lazyObserver = new IntersectionObserver((entries) => {
          entries.forEach(entry => {
            if (entry.isIntersecting) {
              const img = entry.target as HTMLImageElement
              const ds = img.dataset.src
              try { logger.debug('[lazyLoad] intersecting, loading', { src: img.getAttribute('src'), dataSrc: ds, alt: img.alt }) } catch {}
              if (ds) {
                img.src = ds
                img.removeAttribute('data-src')
              }
              lazyObserver?.unobserve(img)
            }
          })
        }, { rootMargin, threshold })
      }

      const marginPx = parseInt(String(rootMargin || '0'), 10) || 0
      for (const img of images) {
        if (img.dataset.src) {
          try {
            // Load immediately if within an expanded viewport (preload visible images)
            const rect = img.getBoundingClientRect()
            const inViewport = rect.top < (window.innerHeight + marginPx) && rect.bottom > -marginPx
            if (inViewport) {
              try { logger.debug('[lazyLoad] immediate load (in-viewport)', { src: img.getAttribute('src'), dataSrc: img.dataset.src, alt: img.alt }) } catch {}
              img.src = img.dataset.src
              img.removeAttribute('data-src')
              continue
            }
            lazyObserver.observe(img)
          } catch (e) { try { logger.debug('[lazyLoad] observe failed', e) } catch {} }
        } else {
          try { logger.debug('[lazyLoad] image has no data-src, skipping', { src: img.getAttribute('src'), alt: img.alt }) } catch {}
        }
      }


    } else {
      // Fallback: load immediately
      for (const img of images) {
        if (img.dataset.src) {
          try { logger.debug('[lazyLoad] fallback loading image', { src: img.getAttribute('src'), dataSrc: img.dataset.src, alt: img.alt }) } catch {}
          img.src = img.dataset.src
          img.removeAttribute('data-src')
        }
      }
    }
  } catch (e) {
    try { logger.debug('observeLazyImages error', e) } catch {}
  }
}
