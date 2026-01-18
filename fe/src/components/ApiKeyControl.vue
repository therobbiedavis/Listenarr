<template>
  <div class="input-group api-key-control">
    <PasswordInput
      v-model="internalKey"
      placeholder="API Key"
      class="input-group-input"
      :disabled="disabled"
    />
    <div class="input-group-append">
      <button
        type="button"
        class="icon-button input-group-btn copy-btn"
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

      <button
        type="button"
        class="regenerate-button input-group-btn regen-btn"
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
        <span v-if="!loading">{{ internalKey ? 'Regenerate' : 'Generate' }}</span>
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
.input-group.api-key-control {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.input-group-append {
  display: flex;
  gap: 0.5rem;
}

.icon-button.input-group-btn,
.regenerate-button.input-group-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  border-radius: 6px;
  border: 1px solid rgba(255,255,255,0.08);
  background: #1a1a1a;
  color: #fff;
  cursor: pointer;
}

.icon-button.input-group-btn:disabled,
.regenerate-button.input-group-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.icon-button.copied {
  background: rgba(76, 175, 80, 0.08);
  border-color: rgba(76,175,80,0.18);
}

.ph-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
