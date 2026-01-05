import { ref } from 'vue'

type Resolver = (v: boolean) => void

// Singleton app-level confirm service
const visible = ref(false)
const title = ref('')
const message = ref('')
const confirmText = ref('Confirm')
const cancelText = ref('Cancel')
const danger = ref(false)

let resolver: Resolver | null = null

export function showConfirm(
  msg: string,
  t?: string,
  options?: { confirmText?: string; cancelText?: string; danger?: boolean },
): Promise<boolean> {
  message.value = msg
  title.value = t || 'Confirm'
  confirmText.value = options?.confirmText ?? 'Confirm'
  cancelText.value = options?.cancelText ?? 'Cancel'
  danger.value = !!options?.danger
  visible.value = true
  return new Promise<boolean>((resolve) => {
    resolver = resolve
  })
}

export function confirm() {
  if (resolver) resolver(true)
  resolver = null
  visible.value = false
}

export function cancel() {
  if (resolver) resolver(false)
  resolver = null
  visible.value = false
}

export function useConfirmService() {
  return {
    visible,
    title,
    message,
    confirmText,
    cancelText,
    danger,
    showConfirm,
    confirm,
    cancel,
  }
}

// Default export convenience
export default {
  visible,
  title,
  message,
  confirmText,
  cancelText,
  danger,
  showConfirm,
  confirm,
  cancel,
  useConfirmService,
}
