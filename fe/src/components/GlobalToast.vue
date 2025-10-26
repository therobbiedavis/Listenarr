<template>
  <div class="toast-container" aria-live="polite" aria-atomic="true">
    <transition-group name="toast" tag="div">
      <div v-for="t in toasts" :key="t.id" :class="['toast', t.level]">
        <div class="toast-title">{{ t.title }}</div>
        <div class="toast-message">{{ t.message }}</div>
        <button class="toast-close" @click="dismiss(t.id)">×</button>
      </div>
    </transition-group>
  </div>
</template>

<script setup lang="ts">
import { onUnmounted } from 'vue'
import { useToast } from '@/services/toastService'

const { toasts: toastsRef, dismiss } = useToast()
const toasts = toastsRef

onUnmounted(() => {
  // cleanup if necessary
})
</script>

<style scoped>
.toast-container { position: fixed; right: 20px; top: 80px; z-index: 2000; display: flex; flex-direction: column; gap: 8px; }
.toast { min-width: 260px; max-width: 380px; padding: 12px 14px; border-radius: 8px; color: #fff; box-shadow: 0 6px 20px rgba(0,0,0,0.45); position: relative; overflow: hidden; }
.toast .toast-title { font-weight: 700; margin-bottom: 4px; }
.toast .toast-message { font-size: 13px; color: #f3f3f3; }
.toast .toast-close { position: absolute; top: 6px; right: 8px; background: transparent; border: none; color: #fff; font-size: 16px; cursor: pointer; }
.toast.info { background: linear-gradient(90deg,#2196F3,#42A5F5); }
.toast.success { background: linear-gradient(90deg,#4CAF50,#66BB6A); }
.toast.warning { background: linear-gradient(90deg,#FF9800,#FFB74D); }
.toast.error { background: linear-gradient(90deg,#E53935,#EF5350); }
.toast-enter-active, .toast-leave-active { transition: all 300ms ease; }
.toast-enter-from { transform: translateX(20px); opacity: 0; }
.toast-leave-to { transform: translateX(20px); opacity: 0; }
</style>
