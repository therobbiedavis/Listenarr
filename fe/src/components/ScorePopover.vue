<template>
  <div class="score-popover" @mouseenter="show" @mouseleave="hide">
    <slot />
    <div v-if="visible" class="popover-content" role="tooltip">
      <div class="popover-inner">{{ content }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
const props = defineProps<{ content: string }>()
const visible = ref(false)
const show = () => { visible.value = true }
const hide = () => { visible.value = false }
const { content } = props
</script>

<style scoped>
.score-popover {
  position: relative;
  display: inline-block;
}
.popover-content {
  position: absolute;
  top: calc(100% + 8px);
  left: 50%;
  transform: translateX(-50%);
  background: rgba(20,20,20,0.98);
  color: #fff;
  padding: 0.5rem 0.6rem;
  border-radius: 6px;
  box-shadow: 0 6px 18px rgba(0,0,0,0.5);
  white-space: pre-line;
  z-index: 1000;
  min-width: 160px;
  max-width: 320px;
  font-size: 0.85rem;
}
.popover-inner {
  color: #e6eef8;
}
</style>
