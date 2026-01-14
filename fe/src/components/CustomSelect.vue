<template>
  <div class="custom-select" :class="{ open: isOpen, disabled }">
    <div
      class="select-trigger"
      @click.stop="toggleDropdown"
      :tabindex="disabled ? -1 : 0"
      @keydown="handleKeydown"
    >
      <div class="select-content">
        <component :is="selectedOption?.icon" v-if="selectedOption?.icon" class="option-icon" />
        <PhArrowsDownUp class="dropdown-arrow" :class="{ rotated: isOpen }" />
        <span class="option-text">{{ selectedOption?.label || placeholder }}</span>
      </div>

    </div>

    <div v-if="isOpen" class="select-dropdown" ref="dropdown">
      <div
        v-for="option in options"
        :key="option.value"
        class="select-option"
        :class="{ selected: option.value === modelValue }"
        @click.stop="selectOption(option)"
      >
        <component :is="option.icon" v-if="option.icon" class="option-icon" />
        <span class="option-text">{{ option.label }}</span>
      </div>
    </div>

    <!-- Invisible native select for form compatibility -->
    <select :value="modelValue" @input="onNativeInput" class="hidden-select" ref="hiddenSelect">
      <option v-for="option in options" :key="option.value" :value="option.value">
        {{ option.label }}
      </option>
    </select>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { PhArrowsDownUp } from '@phosphor-icons/vue'
import type { Component } from 'vue'

interface SelectOption {
  value: string
  label: string
  icon?: Component
}

interface Props {
  modelValue: string
  options: SelectOption[]
  placeholder?: string
  disabled?: boolean
}

interface Emits {
  (e: 'update:modelValue', value: string): void
}

const props = withDefaults(defineProps<Props>(), {
  placeholder: 'Select an option',
  disabled: false,
})

const emit = defineEmits<Emits>()

const isOpen = ref(false)
const dropdown = ref<HTMLElement>()
const hiddenSelect = ref<HTMLSelectElement>()

const selectedOption = computed(() => {
  return props.options.find((option) => option.value === props.modelValue)
})

const toggleDropdown = () => {
  if (props.disabled) return
  isOpen.value = !isOpen.value
}

const selectOption = (option: SelectOption) => {
  emit('update:modelValue', option.value)
  isOpen.value = false
}

const handleKeydown = (event: KeyboardEvent) => {
  if (props.disabled) return

  switch (event.key) {
    case 'Enter':
    case ' ':
      event.preventDefault()
      toggleDropdown()
      break
    case 'Escape':
      event.preventDefault()
      isOpen.value = false
      break
    case 'ArrowDown':
      event.preventDefault()
      if (!isOpen.value) {
        isOpen.value = true
      } else {
        // Could implement keyboard navigation here
      }
      break
  }
}

const handleClickOutside = (event: MouseEvent) => {
  if (dropdown.value && !dropdown.value.contains(event.target as Node)) {
    isOpen.value = false
  }
}

// Native select input handler with proper typing for TS checks
const onNativeInput = (event: Event) => {
  const target = event.target as HTMLSelectElement | null
  const value = target?.value
  if (typeof value === 'string') {
    emit('update:modelValue', value)
  }
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})

// Expose methods for form compatibility
defineExpose({
  focus: () => hiddenSelect.value?.focus(),
  blur: () => hiddenSelect.value?.blur(),
})
</script>

<style scoped>
.custom-select {
  position: relative;
  width: 100%;
}

.select-trigger {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 12px; /* match toolbar button horizontal padding */
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  color: #fff;
  cursor: pointer;
  transition: all 0.2s ease;
  min-height: 36px; /* same height as toolbar buttons */
  font-size: 13px; /* match toolbar font size */
  display: inline-flex;
}

.select-trigger:focus {
  outline: none;
  border-color: #4dabf7;
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.1);
}

.select-trigger:hover:not(.disabled) {
  border-color: rgba(255, 255, 255, 0.15);
}

.custom-select.disabled .select-trigger {
  opacity: 0.5;
  cursor: not-allowed;
}

.select-content {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  flex: 1;
  min-width: 0;
}

.option-icon {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
  color: #4dabf7;
}

.option-text {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.dropdown-arrow {
  width: 16px;
  height: 16px;
  color: #adb5bd;
  transition: transform 0.2s ease;
  flex-shrink: 0;
}

.dropdown-arrow.rotated {
  transform: rotate(180deg);
}

.select-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  z-index: 1000;
  max-height: 200px;
  overflow-y: auto;
  margin-top: 4px;
  min-width: 145px;
  width: fit-content;
}

.select-option {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 6px 8px;
  cursor: pointer;
  transition: background-color 0.2s ease;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  font-size: 12px;
}

.select-option:last-child {
  border-bottom: none;
}

.select-option:hover {
  background-color: rgba(255, 255, 255, 0.05);
}

.select-option.selected {
  background-color: rgba(77, 171, 247, 0.15);
  color: #4dabf7;
}

.select-option.selected .option-icon {
  color: #74c0fc;
}

.hidden-select {
  position: absolute;
  opacity: 0;
  pointer-events: none;
  width: 1px;
  height: 1px;
}

/* Mobile responsive adjustments */
@media (max-width: 768px) {
}

/* Mobile-friendly: hide text and icons in trigger on screens 1024px and below */
@media (max-width: 1024px) {
  .select-trigger .option-text {
    display: none;
  }

  .select-trigger {
    padding: 8px 8px;
  }

  .select-option {
    padding: 8px 8px;
  }

  .select-trigger .option-icon {
    display: none;
  }

  .select-trigger {
    width: fit-content;
  }
}
</style>
