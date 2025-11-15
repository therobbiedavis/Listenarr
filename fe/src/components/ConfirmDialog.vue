<template>
  <div v-if="modelValue" class="confirm-overlay" @click="onCancel">
    <div class="confirm-dialog" @click.stop>
      <div class="confirm-header">
        <h3>{{ title || 'Confirm' }}</h3>
      </div>
      <div class="confirm-body">
        <p v-html="message"></p>
      </div>
      <div class="confirm-actions">
        <button class="btn cancel" @click="onCancel">{{ cancelText }}</button>
        <button class="btn confirm" :class="{ danger }" @click="onConfirm">{{ confirmText }}</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
// use the compiler macros directly (no need to import defineProps/defineEmits)
defineProps({
  modelValue: { type: Boolean, required: true },
  title: { type: String, default: '' },
  message: { type: String, default: '' },
  confirmText: { type: String, default: 'Confirm' },
  cancelText: { type: String, default: 'Cancel' },
  danger: { type: Boolean, default: false }
})

const emit = defineEmits(['update:modelValue', 'confirm'])

function onConfirm() {
  emit('confirm')
  emit('update:modelValue', false)
}

function onCancel() {
  emit('update:modelValue', false)
}
</script>

<style scoped>
.confirm-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1200;
}
.confirm-dialog {
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 8px;
  width: 90%;
  max-width: 520px;
  box-shadow: 0 10px 40px rgba(0,0,0,0.6);
  padding: 16px;
}
.confirm-header h3 {
  margin: 0 0 8px 0;
  color: #fff;
}
.confirm-body p {
  color: #ddd;
  margin: 0 0 16px 0;
}
.confirm-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
.btn {
  padding: 8px 14px;
  border-radius: 6px;
  border: none;
  cursor: pointer;
}
.btn.cancel { background: #3a3a3a; color: #fff }
.btn.confirm { background: #2196F3; color: #fff }
.btn.confirm.danger { background: #e74c3c }
</style>