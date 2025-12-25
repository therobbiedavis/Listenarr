<!--
  Listenarr - Audiobook Management System
  Copyright (C) 2024-2025 Robbie Davis
  
  This program is free software: you can redistribute it and/or modify
  it under the terms of the GNU Affero General Public License as published
  by the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
  GNU Affero General Public License for more details.

  You should have received a copy of the GNU Affero General Public License
  along with this program. If not, see <https://www.gnu.org/licenses/>.
-->

<template>
  <Transition name="modal">
    <div v-if="visible" class="modal-overlay" @click.self="handleClose">
      <div class="notification-modal" :class="`notification-${type}`">
        <div class="notification-icon">
          <i v-if="type === 'success'" class="ph ph-check-circle"></i>
          <i v-else-if="type === 'error'" class="ph ph-x-circle"></i>
          <i v-else-if="type === 'warning'" class="ph ph-warning"></i>
          <i v-else class="ph ph-info"></i>
        </div>
        <div class="notification-content">
          <h3 v-if="title">{{ title }}</h3>
          <p>{{ message }}</p>
        </div>
        <button class="notification-close" @click="handleClose" aria-label="Close">
          <i class="ph ph-x"></i>
        </button>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { watch } from 'vue'

interface Props {
  visible: boolean
  message: string
  title?: string
  type?: 'success' | 'error' | 'warning' | 'info'
  autoClose?: number // milliseconds, 0 = no auto close
}

const props = withDefaults(defineProps<Props>(), {
  type: 'info',
  autoClose: 3000
})

const emit = defineEmits<{
  close: []
}>()

let autoCloseTimer: ReturnType<typeof setTimeout> | null = null

const handleClose = () => {
  if (autoCloseTimer) {
    clearTimeout(autoCloseTimer)
    autoCloseTimer = null
  }
  emit('close')
}

// Auto-close functionality
watch(() => props.visible, (isVisible) => {
  if (isVisible && props.autoClose > 0) {
    autoCloseTimer = setTimeout(() => {
      handleClose()
    }, props.autoClose)
  }
})
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: rgba(0, 0, 0, 0.5);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 9999;
  padding: 1rem;
}

.notification-modal {
  background: #2a2a2a;
  border-radius: 6px;
  padding: 1.5rem;
  max-width: 500px;
  width: 100%;
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
  position: relative;
  animation: slideIn 0.3s ease-out;
}

.notification-icon {
  flex-shrink: 0;
}

.notification-icon i {
  font-size: 2.5rem;
}

.notification-success .notification-icon i {
  color: #4caf50;
}

.notification-error .notification-icon i {
  color: #f44336;
}

.notification-warning .notification-icon i {
  color: #ff9800;
}

.notification-info .notification-icon i {
  color: #2196f3;
}

.notification-content {
  flex: 1;
  color: #ffffff;
}

.notification-content h3 {
  margin: 0 0 0.5rem 0;
  font-size: 1.25rem;
  font-weight: 600;
}

.notification-content p {
  margin: 0;
  line-height: 1.5;
  color: #cccccc;
}

.notification-close {
  position: absolute;
  top: 1rem;
  right: 1rem;
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  padding: 0.25rem;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: color 0.2s;
  font-size: 1.25rem;
}

.notification-close:hover {
  color: #fff;
}

/* Animations */
.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.3s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-active .notification-modal {
  animation: slideIn 0.3s ease-out;
}

.modal-leave-active .notification-modal {
  animation: slideOut 0.3s ease-in;
}

@keyframes slideIn {
  from {
    transform: translateY(-20px);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

@keyframes slideOut {
  from {
    transform: translateY(0);
    opacity: 1;
  }
  to {
    transform: translateY(-20px);
    opacity: 0;
  }
}

/* Responsive */
@media (max-width: 768px) {
  .notification-modal {
    max-width: calc(100vw - 2rem);
  }
  
  .notification-icon i {
    font-size: 2rem;
  }
  
  .notification-content h3 {
    font-size: 1.1rem;
  }
}
</style>
