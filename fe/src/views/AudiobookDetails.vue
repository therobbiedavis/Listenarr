<template>
  <div class="audiobook-details">
    <!-- ...existing code... -->
    <div class="detail-item">
      <strong>Quality Profile ID:</strong> {{ audiobook.qualityProfileId ?? 'Not set' }}
    </div>
    <!-- ...existing code... -->
  </div>
</template>

<script setup lang="ts">
// ...existing code...
// Assuming audiobook is fetched and reactive
// ...existing code...
import { ref } from 'vue'
import type { Audiobook } from '@/types'

// Minimal placeholder so the template's `audiobook` binding type-checks. The real
// implementation populates this from the store or API; keep this as a defensive
// fallback for type-checking and local dev.
const audiobook = ref<Partial<Audiobook>>({}) as unknown as Audiobook
</script>

<style scoped>
.detail-item {
  margin-bottom: 10px;
}
</style>