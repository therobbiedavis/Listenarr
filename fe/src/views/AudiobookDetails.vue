<template>
  <div class="audiobook-details">
    <!-- ...existing code... -->
    <div class="detail-item">
      <strong>Quality Profile:</strong> {{ audiobook.qualityProfile?.name || 'Not set' }}
    </div>
    <!-- ...existing code... -->
  </div>
</template>

<script setup lang="ts">
// ...existing code...
// Assuming audiobook is fetched and reactive
// ...existing code...
</script>

<style scoped>
.detail-item {
  margin-bottom: 10px;
}
</style>