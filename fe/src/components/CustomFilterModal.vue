<template>
  <div v-if="isOpen" class="modal-overlay" @click.self="onClose">
    <div class="modal-container">
      <div class="modal-header">
        <h2>Custom Filter</h2>
        <button class="btn-close" @click="onClose" aria-label="Close">✕</button>
      </div>

      <div class="modal-body">
        <form @submit.prevent="onSave" class="edit-form">
          <div class="form-row">
            <label class="form-label">Label</label>
            <input v-model="local.label" class="form-input" placeholder="Filter name" />
          </div>

          <div class="form-row">
            <label class="form-label">Filters</label>
            <div class="rules">
              <div v-for="(r, idx) in local.rules" :key="idx" class="rule-row">
                <!-- Group start toggle -->
                <button type="button" class="group-toggle" :class="{ active: r.groupStart }" @click.prevent="r.groupStart = !r.groupStart">(</button>

                <!-- Show conjunction selector before each rule except the first -->
                <template v-if="idx > 0">
                  <select v-model="r.conjunction" class="form-select" style="width:80px;">
                    <option value="and">AND</option>
                    <option value="or">OR</option>
                  </select>
                </template>
                <select v-model="r.field" class="form-select">
                  <option value="monitored">Monitored</option>
                  <option value="title">Title</option>
                  <option value="author">Author</option>
                  <option value="narrator">Narrator</option>
                  <option value="language">Language</option>
                  <option value="publisher">Publisher</option>
                  <option value="qualityProfileId">Quality Profile</option>
                  <option value="publishYear">Published Year</option>
                  <option value="path">Path</option>
                  <option value="files">Files</option>
                  <option value="filesize">Filesize</option>
                </select>

                <select v-model="r.operator" class="form-select small">
                  <!-- Operator choices depend on field type -->
                  <template v-if="r.field === 'monitored'">
                    <option value="is">is</option>
                    <option value="is_not">is not</option>
                  </template>
                  <template v-else-if="['publishYear','publishedYear','files','filesize'].includes(r.field)">
                    <option value="eq">=</option>
                    <option value="ne">!=</option>
                    <option value="lt">&lt;</option>
                    <option value="lte">&lt;=</option>
                    <option value="gt">&gt;</option>
                    <option value="gte">&gt;=</option>
                  </template>
                  <template v-else>
                    <option value="is">is</option>
                    <option value="is_not">is not</option>
                    <option value="contains">contains</option>
                    <option value="not_contains">not contains</option>
                  </template>
                </select>

                <template v-if="r.field === 'monitored'">
                  <select v-model="r.value" class="form-select">
                    <option value="true">true</option>
                    <option value="false">false</option>
                  </select>
                </template>

                <template v-else-if="r.field === 'qualityProfileId'">
                  <select v-model="r.value" class="form-select">
                    <option value="">(any)</option>
                    <option v-for="p in qualityProfiles" :key="p.id" :value="String(p.id)">{{ p.name }}</option>
                  </select>
                </template>

                <template v-else-if="r.field === 'language'">
                  <select v-model="r.value" class="form-select">
                    <option value="">(any)</option>
                    <option v-for="l in languages" :key="l" :value="l">{{ l }}</option>
                  </select>
                </template>

                <template v-else-if="r.field === 'publishYear' || r.field === 'publishedYear'">
                  <!-- Numeric input for year so users can use comparisons -->
                  <input v-model.number="r.value" type="number" class="form-input" placeholder="e.g. 2023" />
                </template>

                <template v-else-if="r.field === 'files'">
                  <!-- Numeric input for file count -->
                  <input v-model.number="r.value" type="number" class="form-input" placeholder="Number of files" />
                </template>

                <template v-else-if="r.field === 'filesize'">
                  <!-- Number + unit selector for filesize -->
                  <div style="display:flex;gap:8px;align-items:center;">
                    <input :value="getFileSizeDisplay(r).num" @input.prevent="onFileSizeInputEvent(r, $event)" type="number" class="form-input" placeholder="Size" />
                    <select :value="getFileSizeDisplay(r).unit" @change.prevent="onFileSizeUnitChangeEvent(r, $event)" class="form-select small">
                      <option v-for="u in SIZE_UNITS" :key="u" :value="u">{{ u }}</option>
                    </select>
                  </div>
                </template>

                <template v-else>
                  <input v-model="r.value" class="form-input" placeholder="Value" />
                </template>

                <div class="rule-actions">
              <button type="button" class="btn btn-secondary" @click.prevent="removeRule(idx)">−</button>
              <button type="button" class="group-toggle end" :class="{ active: r.groupEnd }" @click.prevent="r.groupEnd = !r.groupEnd">)</button>
                </div>
              </div>

              <div class="rules-actions">
                <button type="button" class="btn btn-primary" @click.prevent="addRule">＋ Add rule</button>
              </div>
            </div>
          </div>

          <div class="modal-actions">
            <button type="button" class="btn btn-secondary" @click="onClose">Cancel</button>
            <button type="submit" class="btn btn-primary">Save</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, toRaw } from 'vue'

// Types
type Rule = { field: string; operator: string; value: string; conjunction?: 'and' | 'or'; groupStart?: boolean; groupEnd?: boolean }
type CustomFilter = { id?: string; label: string; rules: Rule[] }

// Props
const props = defineProps<{
  isOpen: boolean
  filter: CustomFilter | null
  // accept profiles with optional id to match upstream type shape
  qualityProfiles?: Array<{ id?: number; name: string }>
  languages?: string[]
  years?: Array<string | number>
}>()
const emit = defineEmits(['save', 'close'])

// Local editable copy
const local = ref<CustomFilter>({ label: '', rules: [] })

const SIZE_UNITS = ['B', 'KB', 'MB', 'GB'] as const

function unitMultiplier(u: string) {
  switch (u) {
    case 'KB': return 1024
    case 'MB': return 1024 * 1024
    case 'GB': return 1024 * 1024 * 1024
    default: return 1
  }
}

function displayForBytes(bytes: number) {
  if (!bytes || isNaN(bytes)) return { num: '', unit: 'MB' }
  if (bytes >= unitMultiplier('GB')) return { num: +(bytes / unitMultiplier('GB')).toFixed(2), unit: 'GB' }
  if (bytes >= unitMultiplier('MB')) return { num: +(bytes / unitMultiplier('MB')).toFixed(2), unit: 'MB' }
  if (bytes >= unitMultiplier('KB')) return { num: +(bytes / unitMultiplier('KB')).toFixed(2), unit: 'KB' }
  return { num: bytes, unit: 'B' }
}

function getFileSizeDisplay(r: Rule) {
  const bytes = Number(r.value)
  if (isNaN(bytes) || r.value === '') return { num: '', unit: 'MB' }
  return displayForBytes(bytes)
}

function onFileSizeInput(r: Rule, raw: string) {
  const num = Number(raw)
  if (isNaN(num)) {
    r.value = ''
    return
  }
  const { unit } = getFileSizeDisplay(r)
  // If r.value empty, default unit to MB
  const useUnit = unit || 'MB'
  r.value = String(Math.round(num * unitMultiplier(useUnit)))
}

function onFileSizeUnitChange(r: Rule, newUnit: string) {
  const display = getFileSizeDisplay(r)
  const num = Number(display.num) || 0
  r.value = String(Math.round(num * unitMultiplier(newUnit)))
}

function onFileSizeInputEvent(r: Rule, ev: Event) {
  const raw = (ev.target as HTMLInputElement)?.value ?? ''
  onFileSizeInput(r, raw)
}

function onFileSizeUnitChangeEvent(r: Rule, ev: Event) {
  const newUnit = (ev.target as HTMLSelectElement)?.value ?? 'MB'
  onFileSizeUnitChange(r, newUnit)
}

watch(
  () => props.filter,
  (f) => {
    if (f) {
      // Deep copy so edits don't immediately mutate parent
      local.value = JSON.parse(JSON.stringify(f))
      // Ensure conjunction present on older filters
      local.value.rules = (local.value.rules || []).map((rr: Record<string, unknown>) => {
        return {
          field: String(rr.field ?? 'title'),
          operator: String(rr.operator ?? 'contains'),
          value: String(rr.value ?? ''),
          conjunction: rr.conjunction === 'or' ? 'or' : 'and',
          groupStart: !!rr.groupStart,
          groupEnd: !!rr.groupEnd
        } as Rule
      })
    } else {
      local.value = { label: '', rules: [] }
    }
  },
  { immediate: true }
)

function addRule() {
  local.value.rules.push({ field: 'title', operator: 'contains', value: '', conjunction: 'and', groupStart: false, groupEnd: false })
}

function removeRule(index: number) {
  local.value.rules.splice(index, 1)
}

function onSave() {
  // Basic validation: label required
  if (!local.value.label || !local.value.label.trim()) {
    // keep it simple - just don't save empty labels
    return
  }

  // Ensure values are strings
  const out = JSON.parse(JSON.stringify(toRaw(local.value))) as CustomFilter
  // Normalize some values
  out.rules = out.rules.map((r: Rule) => ({ ...r, value: r.value ?? '' }))

  // Ensure id exists for new filters
  if (!out.id) {
    out.id = String(Date.now())
  }

  emit('save', out)
}

function onClose() {
  emit('close')
}
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}
.modal-container {
  background: #1f1f1f;
  border-radius: 8px;
  width: 720px;
  max-width: calc(100% - 32px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.6);
  color: #fff;
  overflow: hidden;
}
.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid rgba(255,255,255,0.04);
}
.modal-body { padding: 16px }
.btn-close { background: transparent; border: none; color: #fff; font-size: 18px }
.form-row { margin-bottom: 12px }
.form-label { display: block; margin-bottom: 6px; color: #ddd }
.form-input, .form-select { width: 100%; padding: 8px 10px; border-radius: 4px; background: #121212; border: 1px solid rgba(255,255,255,0.06); color: #fff }
.form-select.small { width: 140px }
.rules { display: flex; flex-direction: column; gap: 8px }
.rule-row { display: flex; gap: 8px; align-items: center }
.rule-actions { margin-left: auto }
.rules-actions { margin-top: 8px }
.modal-actions { display:flex; justify-content:flex-end; gap:8px; margin-top:8px }
.btn { padding: 8px 12px; border-radius: 4px; cursor: pointer }
.btn-primary { background: #2196F3; color: #fff; border: none }
.btn-secondary { background: #333; color: #fff; border: none }

.group-toggle {
  background: transparent;
  border: 1px solid rgba(255,255,255,0.06);
  color: #ddd;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer;
}
.group-toggle.active {
  background: rgba(33,150,243,0.12);
  border-color: rgba(33,150,243,0.24);
  color: #4dabf7;
}
.group-toggle.end { margin-left: 8px }
</style>

