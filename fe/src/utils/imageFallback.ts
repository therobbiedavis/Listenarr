import { getPlaceholderUrl } from '@/utils/placeholder'

export function handleImageError(ev: Event) {
  try {
    const img = ev?.target as HTMLImageElement | null
    if (!img) return

    // Prevent loops
    try {
      const imgRec = img as unknown as Record<string, unknown>
      if (imgRec.__imageFallbackDone) return
      imgRec.__imageFallbackDone = true
    } catch {}

    // Set placeholder and clear lazy attributes
    try {
      img.src = getPlaceholderUrl()
    } catch {}
    try {
      img.removeAttribute('data-src')
    } catch {}
    try {
      img.removeAttribute('data-original-src')
    } catch {}
    try {
      ;(img as unknown as { onerror?: ((ev: Event) => void) | null }).onerror = null
    } catch {}
  } catch {}
}
