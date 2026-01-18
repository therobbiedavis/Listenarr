<template>
  <div class="password-field">
    <input
      :id="id"
      :type="visible ? 'text' : 'password'"
      :placeholder="placeholder"
      class="password-input"
      v-model="internalValue"
      v-bind="attrs"
    />
    <button
      type="button"
      class="password-toggle"
      @click.prevent="toggle"
      :aria-pressed="visible"
      :title="visible ? 'Hide' : 'Show'"
    >
      <template v-if="visible">
        <PhEyeSlash />
      </template>
      <template v-else>
        <PhEye />
      </template>
    </button>
  </div>
</template>

<script setup lang="ts">
// Prevent attributes (like `class="form-input"`) from being automatically applied
// to the root wrapper element. Instead we explicitly bind them to the inner `<input>` so
// the visual styles for inputs (from parent forms) apply to the input itself, not the
// outer wrapper which would otherwise get nested borders/padding.
import { ref, watch, computed, useAttrs } from 'vue'
import { PhEye, PhEyeSlash } from '@phosphor-icons/vue'

defineOptions({ inheritAttrs: false })

interface Props {
  modelValue?: string
  id?: string
  placeholder?: string
}

const props = defineProps<Props>()
const emit = defineEmits(['update:modelValue'])
const attrs = useAttrs()

const visible = ref(false)
const internalValue = ref(props.modelValue ?? '')

watch(
  () => props.modelValue,
  (v) => {
    internalValue.value = v ?? ''
  },
)

watch(internalValue, (v) => emit('update:modelValue', v))

function toggle() {
  visible.value = !visible.value
}
</script>

<style scoped>
.password-field {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

/* Base input sizing; keep space for toggle */
.password-input {
  width: 100%;
  padding-right: 3.5rem;
  padding-left: 0.75rem;
  padding-top: 0.75rem;
  padding-bottom: 0.75rem;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: #1a1a1a;
  color: #fff;
  font-size: 0.95rem;
  box-sizing: border-box;
}

/* Support being used with .admin-input or .form-input on the component (they are applied to the wrapper)
   and mimic the visual treatment used elsewhere */
.password-field.admin-input .password-input,
.password-field.form-input .password-input {
  padding: 0.75rem;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background-color: #1a1a1a;
  color: #fff;
}

/* Focus styles to match other inputs */
.password-field:focus-within .password-input,
.password-field.admin-input:focus-within .password-input,
.password-field.form-input:focus-within .password-input {
  outline: none;
  border-color: #4dabf7;
  background-color: rgba(0, 0, 0, 0.3);
  box-shadow: 0 0 0 3px rgba(77, 171, 247, 0.15);
}

.password-toggle {
  position: absolute;
  right: 0.5rem;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  color: #868e96;
  cursor: pointer;
  padding: 0.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
  font-size: 1.2rem;
}

.password-toggle:hover {
  background: rgba(255, 255, 255, 0.05);
  color: #fff;
}
</style>
