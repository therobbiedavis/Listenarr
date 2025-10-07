<template>
  <div class="folder-browser">
    <div class="browser-input-group">
      <input
        v-model="localPath"
        type="text"
        :placeholder="placeholder"
        class="browser-input"
        @blur="validatePath"
      />
      <button @click="toggleBrowser" class="browse-button" type="button">
        <i class="ph ph-folder-open"></i>
        Browse
      </button>
    </div>
    
    <div v-if="validationMessage" class="validation-message" :class="{ error: !isValid, success: isValid }">
      <i :class="isValid ? 'ph ph-check-circle' : 'ph ph-warning-circle'"></i>
      {{ validationMessage }}
    </div>

    <div v-if="showBrowser" class="browser-modal" @click="closeBrowser">
      <div class="browser-content" @click.stop>
        <div class="browser-header">
          <h3>
            <i class="ph ph-folder-open"></i>
            Select Folder
          </h3>
          <button @click="closeBrowser" class="close-button">
            <i class="ph ph-x"></i>
          </button>
        </div>

        <div class="browser-body">
          <div class="current-path">
            <button 
              v-if="parentPath !== null"
              @click="navigateToParent"
              class="back-button"
              title="Go up to parent folder"
            >
              <i class="ph ph-arrow-left"></i>
            </button>
            <i class="ph ph-folder"></i>
            <span>{{ currentPath || 'Computer' }}</span>
          </div>

          <div v-if="isLoading" class="loading-state">
            <i class="ph ph-spinner ph-spin"></i>
            <span>Loading directories...</span>
          </div>

          <div v-else-if="error" class="error-state">
            <i class="ph ph-warning-circle"></i>
            <span>{{ error }}</span>
          </div>

          <div v-else class="directory-list">
            <div
              v-if="parentPath !== null"
              @click="navigateToParent"
              class="directory-item parent-item"
            >
              <i class="ph ph-arrow-up"></i>
              <span>Go up to parent folder</span>
            </div>

            <div
              v-for="item in items"
              :key="item.path"
              @click="navigateToDirectory(item)"
              class="directory-item"
            >
              <i class="ph ph-folder"></i>
              <span>{{ item.name }}</span>
            </div>

            <div v-if="items.length === 0 && !parentPath" class="empty-state">
              <i class="ph ph-folder-open"></i>
              <span>No accessible directories found</span>
            </div>
          </div>
        </div>

        <div class="browser-footer">
          <button @click="closeBrowser" class="cancel-button">
            <i class="ph ph-x"></i>
            Cancel
          </button>
          <button @click="selectCurrentPath" class="select-button">
            <i class="ph ph-check"></i>
            Select Folder
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { apiService } from '@/services/api'

interface Props {
  modelValue: string
  placeholder?: string
}

interface FileSystemItem {
  name: string
  path: string
  isDirectory: boolean
  lastModified: string
}

const props = withDefaults(defineProps<Props>(), {
  placeholder: 'Select a folder...'
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const localPath = ref(props.modelValue)
const showBrowser = ref(false)
const currentPath = ref('')
const parentPath = ref<string | null>(null)
const items = ref<FileSystemItem[]>([])
const isLoading = ref(false)
const error = ref('')
const validationMessage = ref('')
const isValid = ref(false)

// Watch for external changes to modelValue
watch(() => props.modelValue, (newValue) => {
  localPath.value = newValue
})

// Watch for local changes
watch(localPath, (newValue) => {
  emit('update:modelValue', newValue)
})

const toggleBrowser = async () => {
  showBrowser.value = !showBrowser.value
  if (showBrowser.value) {
    await browseDirectory(localPath.value || '')
  }
}

const closeBrowser = () => {
  showBrowser.value = false
  error.value = ''
}

const browseDirectory = async (path: string) => {
  isLoading.value = true
  error.value = ''
  
  try {
    const response = await apiService.browseDirectory(path || undefined)
    
    currentPath.value = response.currentPath
    parentPath.value = response.parentPath
    items.value = response.items
  } catch (err: any) {
    error.value = err.message || 'Failed to browse directory'
    console.error('Error browsing directory:', err)
  } finally {
    isLoading.value = false
  }
}

const navigateToParent = () => {
  if (parentPath.value !== null) {
    browseDirectory(parentPath.value)
  }
}

const navigateToDirectory = (item: FileSystemItem) => {
  browseDirectory(item.path)
}

const selectCurrentPath = () => {
  localPath.value = currentPath.value
  emit('update:modelValue', currentPath.value)
  validatePath()
  closeBrowser()
}

const validatePath = async () => {
  if (!localPath.value) {
    validationMessage.value = ''
    isValid.value = false
    return
  }

  try {
    const response = await apiService.validatePath(localPath.value)
    
    isValid.value = response.isValid
    validationMessage.value = response.message
  } catch (err) {
    isValid.value = false
    validationMessage.value = 'Failed to validate path'
    console.error('Error validating path:', err)
  }
}
</script>

<style scoped>
.folder-browser {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.browser-input-group {
  display: flex;
  gap: 0.5rem;
}

.browser-input {
  flex: 1;
  padding: 0.75rem;
  background-color: #2a2a2a;
  border: 1px solid #555;
  border-radius: 4px;
  font-size: 1rem;
  color: #fff;
  transition: all 0.2s;
}

.browser-input:focus {
  outline: none;
  border-color: #007acc;
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.1);
}

.browse-button {
  padding: 0.75rem 1.25rem;
  background-color: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  transition: all 0.2s;
  white-space: nowrap;
}

.browse-button:hover {
  background-color: #005fa3;
}

.validation-message {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  border-radius: 4px;
  font-size: 0.85rem;
}

.validation-message.error {
  background-color: rgba(231, 76, 60, 0.1);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.validation-message.success {
  background-color: rgba(46, 204, 113, 0.1);
  color: #2ecc71;
  border: 1px solid rgba(46, 204, 113, 0.3);
}

.browser-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
  padding: 1rem;
}

.browser-content {
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 8px;
  width: 100%;
  max-width: 700px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.browser-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  border-bottom: 1px solid #444;
}

.browser-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.3rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.browser-header h3 i {
  color: #007acc;
}

.close-button {
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.close-button:hover {
  background-color: #333;
  color: #fff;
}

.browser-body {
  flex: 1;
  overflow-y: auto;
  padding: 1.5rem;
  min-height: 300px;
  max-height: 500px;
}

.current-path {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  background-color: #333;
  border: 1px solid #444;
  border-radius: 4px;
  margin-bottom: 1rem;
  color: #007acc;
  font-family: 'Courier New', monospace;
  font-size: 0.9rem;
}

.current-path i {
  color: #007acc;
  font-size: 1.2rem;
}

.current-path span {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.back-button {
  padding: 0.5rem;
  background-color: rgba(0, 122, 204, 0.2);
  border: 1px solid rgba(0, 122, 204, 0.3);
  border-radius: 4px;
  color: #007acc;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  flex-shrink: 0;
}

.back-button:hover {
  background-color: #007acc;
  color: #fff;
  transform: translateX(-2px);
}

.back-button i {
  font-size: 1.2rem;
}

.loading-state,
.error-state,
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  padding: 3rem 1rem;
  color: #999;
  text-align: center;
}

.loading-state i,
.error-state i,
.empty-state i {
  font-size: 3rem;
  color: #555;
}

.error-state {
  color: #e74c3c;
}

.error-state i {
  color: #e74c3c;
}

.directory-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.directory-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem;
  background-color: #333;
  border: 1px solid #444;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  color: #fff;
}

.directory-item:hover {
  background-color: #3a3a3a;
  border-color: #007acc;
  transform: translateX(4px);
}

.directory-item i {
  font-size: 1.3rem;
  color: #007acc;
  flex-shrink: 0;
}

.directory-item.parent-item {
  background-color: rgba(0, 122, 204, 0.1);
  border-color: rgba(0, 122, 204, 0.3);
}

.directory-item.parent-item:hover {
  background-color: rgba(0, 122, 204, 0.2);
}

.directory-item.parent-item i {
  color: #007acc;
}

.browser-footer {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem;
  border-top: 1px solid #444;
}

.cancel-button,
.select-button {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
}

.cancel-button {
  background-color: #555;
  color: white;
}

.cancel-button:hover {
  background-color: #666;
}

.select-button {
  background-color: #007acc;
  color: white;
}

.select-button:hover {
  background-color: #005fa3;
}

@media (max-width: 768px) {
  .browser-modal {
    padding: 0;
  }

  .browser-content {
    max-width: 100%;
    max-height: 100vh;
    border-radius: 0;
  }

  .browser-input-group {
    flex-direction: column;
  }

  .browse-button {
    justify-content: center;
  }
}
</style>
