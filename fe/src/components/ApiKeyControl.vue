<template>
  <div class="api-key-field">
    <PasswordInput
      v-model="internalKey"
      placeholder="API Key"
      class="api-key-input"
      :disabled="disabled"
    />
    <div class="api-key-buttons">
      <button
        type="button"
        class="regenerate-button regen-btn"
        @click="onRegenerate"
        :disabled="loading"
        :title="internalKey ? 'Regenerate API key' : 'Generate API key'"
      >
        <template v-if="loading">
          <PhSpinner class="ph-spin" />
        </template>
        <template v-else-if="internalKey">
          <PhArrowCounterClockwise />
        </template>
        <template v-else>
          <PhPlus />
        </template>
      </button>

      <button
        type="button"
        class="icon-button copy-btn"
        :class="{ copied: copied }"
        @click="copy"
        :disabled="!internalKey"
        :title="copied ? 'Copied' : 'Copy API key'"
      >
        <template v-if="copied">
          <PhCheck />
        </template>
        <template v-else>
          <PhFiles />
        </template>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import PasswordInput from '@/components/PasswordInput.vue'
import { showConfirm } from '@/composables/useConfirm'
import { apiService } from '@/services/api'
import { useToast } from '@/services/toastService'
import { errorTracking } from '@/services/errorTracking'
import { logger } from '@/utils/logger'
import {
  PhFiles,
  PhCheck,
  PhSpinner,
  PhArrowCounterClockwise,
  PhPlus,
} from '@phosphor-icons/vue'

interface Props {
  apiKey?: string | null
  disabled?: boolean
}

const props = defineProps<Props>()
const emit = defineEmits(['update:apiKey'])

const internalKey = ref(props.apiKey ?? '')
const loading = ref(false)
const copied = ref(false)
const toast = useToast()

watch(
  () => props.apiKey,
  (v) => {
    internalKey.value = v ?? ''
  },
)

const copy = async () => {
  if (!internalKey.value) return
  try {
    await navigator.clipboard.writeText(internalKey.value)
    copied.value = true
    setTimeout(() => (copied.value = false), 2000)
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'ApiKeyControl',
      operation: 'copy',
    })
    logger.debug('Failed to copy API key', err)
    toast.error('Copy failed', 'Unable to copy the API key to clipboard')
  }
}

const onRegenerate = async () => {
  const hasExistingKey = !!internalKey.value
  const confirmMessage = hasExistingKey
    ? 'Regenerating the API key will immediately invalidate the existing key. Continue?'
    : 'Generate a new API key for this server instance?'

  const ok = await showConfirm(confirmMessage, 'API Key')
  if (!ok) return

  loading.value = true
  try {
    let resp: { apiKey: string; message?: string }

    if (!hasExistingKey) {
      try {
        resp = await apiService.generateInitialApiKey()
        internalKey.value = resp.apiKey
        emit('update:apiKey', resp.apiKey)
        toast.info('API key', resp.message || 'API key generated - copied to clipboard')
        try {
          await navigator.clipboard.writeText(resp.apiKey)
        } catch {}
        return
      } catch (initialErr) {
        logger.debug('Initial API key generation failed, trying authenticated regeneration', initialErr)
      }
    }

    resp = await apiService.regenerateApiKey()
    internalKey.value = resp.apiKey
    emit('update:apiKey', resp.apiKey)
    toast.info('API key', 'API key regenerated - copied to clipboard')
    try {
      await navigator.clipboard.writeText(resp.apiKey)
    } catch {}
  } catch (err) {
    errorTracking.captureException(err as Error, {
      component: 'ApiKeyControl',
      operation: 'regenerate',
    })
    const status = (err && typeof err === 'object' && 'status' in (err as any)) ? (err as any).status : 0
    if (status === 401 || status === 403) {
      toast.error(
        'Permission denied',
        'You must be logged in as an administrator to regenerate the API key. Please login and try again.',
      )
    } else {
      toast.error('API key failed', 'Failed to generate/regenerate API key')
    }
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.api-key-field {
  position: relative;
  width: 100%;
}

.api-key-field :deep(.password-input) {
  padding-right: 8rem; /* Space for visibility toggle (3.5rem) + 2 buttons + gaps */
}

.api-key-field :deep(.password-toggle) {
  right: 5.5rem; /* Position to the left of the api-key-buttons */
}

.api-key-buttons {
  position: absolute;
  right: 0.5rem; /* Rightmost position */
  top: 50%;
  transform: translateY(-50%);
  display: flex;
  gap: 0.25rem;
}

.icon-button,
.regenerate-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.5rem;
  border: none;
  background: none;
  color: #868e96;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
  font-size: 1.1rem;
}

.regenerate-button {
  color: #f44336; /* Red hue for regenerate */
}

.icon-button {
  color: #2196f3; /* Blue hue for copy */
}

.icon-button:hover,
.regenerate-button:hover {
  background: rgba(255, 255, 255, 0.05);
  color: #fff;
}

.regenerate-button:hover {
  color: #ff6b6b; /* Lighter red on hover */
}

.icon-button:hover {
  color: #64b5f6; /* Lighter blue on hover */
}

.icon-button:disabled,
.regenerate-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.icon-button.copied {
  color: #4caf50; /* Green for copied state */
}

.ph-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
