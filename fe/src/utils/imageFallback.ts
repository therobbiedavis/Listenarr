import { apiService } from '@/services/api'

export function handleImageError(ev: Event) {
  try {
    const img = ev?.target as HTMLImageElement | null
    if (!img) return

    // Prevent loops
    try { if ((img as any).__imageFallbackDone) return; (img as any).__imageFallbackDone = true } catch {}

    // Try to extract identifier from data-original-src or src that points to /api/images/{id}
    const original = img.dataset?.originalSrc || img.getAttribute('src') || ''
    try {
      const m = (original || '').match(/\/api\/images\/([^?\\/]+)(?:\?|$)/i)
      if (m && m[1]) {
        try { apiService.markImageFailed(decodeURIComponent(m[1])) } catch {}
      }
    } catch {}

    // Set placeholder and clear lazy attributes
    try { img.src = apiService.getPlaceholderUrl() } catch {}
    try { img.removeAttribute('data-src') } catch {}
    try { img.removeAttribute('data-original-src') } catch {}
    try { (img as any).onerror = null } catch {}
  } catch {}
}
