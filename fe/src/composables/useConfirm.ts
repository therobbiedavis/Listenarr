// Backwards-compatible wrapper that uses the centralized confirm service
import { useConfirmService, showConfirm as serviceShowConfirm } from './confirmService'

export function useConfirm() {
  // Return the singleton service (components can still call useConfirm() to get reactive refs)
  return useConfirmService()
}

export { serviceShowConfirm as showConfirm }
