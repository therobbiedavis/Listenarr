<template>
  <div class="tab-content">
    <div class="quality-profiles-tab">
      <div class="section-header">
        <h3>Quality Profiles</h3>
      </div>

      <!-- Empty State -->
      <div v-if="qualityProfiles.length === 0" class="empty-state">
        <PhStar class="empty-icon" />
        <p>No quality profiles configured yet.</p>
        <p class="empty-help">
          Quality profiles define which release qualities you want to download and prefer.
        </p>
      </div>

      <!-- Quality Profiles Grid -->
      <div v-else class="profiles-grid">
        <div
          v-for="profile in qualityProfiles"
          :key="profile.id"
          class="profile-card"
          :class="{ 'is-default': profile.isDefault }"
        >
          <div class="profile-header">
            <div class="profile-title-section">
              <div class="profile-name-row">
                <h4>{{ profile.name }}</h4>
                <span v-if="profile.isDefault" class="status-badge default">
                  <PhCheckCircle />
                  Default
                </span>
              </div>
              <p v-if="profile.description" class="profile-description">
                {{ profile.description }}
              </p>
            </div>
            <div class="profile-actions">
              <button @click="editProfile(profile)" class="icon-button" title="Edit Profile">
                <PhPencil />
              </button>
              <button
                v-if="!profile.isDefault"
                @click="setDefaultProfile(profile)"
                class="icon-button"
                title="Set as Default"
              >
                <PhStar />
              </button>
              <button
                @click="confirmDeleteProfile(profile)"
                class="icon-button danger"
                :disabled="profile.isDefault"
                :title="profile.isDefault ? 'Cannot delete default profile' : 'Delete Profile'"
              >
                <PhTrash />
              </button>
            </div>
          </div>

          <div class="profile-content">
            <!-- Qualities Section -->
            <div
              v-if="profile.qualities && profile.qualities.filter((q) => q.allowed).length > 0"
              class="profile-section"
            >
              <h5><PhCheckSquare /> Allowed Qualities</h5>
              <div class="quality-badges">
                <span
                  v-for="quality in profile.qualities
                    .filter((q) => q.allowed)
                    .sort((a, b) => b.priority - a.priority)"
                  :key="quality.quality"
                  class="quality-badge"
                  :class="{ 'is-cutoff': quality.quality === profile.cutoffQuality }"
                >
                  {{ quality.quality }}
                  <template v-if="quality.quality === profile.cutoffQuality">
                    <PhScissors title="Cutoff Quality" />
                  </template>
                </span>
              </div>
            </div>

            <!-- Preferences Section -->
            <div
              v-if="profile.preferredFormats?.length || profile.preferredLanguages?.length"
              class="profile-section"
            >
              <h5><PhSliders /> Preferences</h5>
              <div class="preferences-grid">
                <div
                  v-if="profile.preferredFormats && profile.preferredFormats.length > 0"
                  class="preference-item"
                >
                  <span class="preference-label">Formats</span>
                  <span class="preference-value">{{ profile.preferredFormats.join(', ') }}</span>
                </div>
                <div
                  v-if="profile.preferredLanguages && profile.preferredLanguages.length > 0"
                  class="preference-item"
                >
                  <span class="preference-label">Languages</span>
                  <span class="preference-value">{{ profile.preferredLanguages.join(', ') }}</span>
                </div>
              </div>
            </div>

            <!-- Limits Section -->
            <div
              v-if="
                profile.minimumSize ||
                profile.maximumSize ||
                (profile.minimumSeeders && profile.minimumSeeders > 0) ||
                (profile.minimumScore && profile.minimumScore > 0) ||
                (profile.maximumAge && profile.maximumAge > 0)
              "
              class="profile-section"
            >
              <h5><PhListChecks /> Limits & Requirements</h5>
              <div class="limits-grid">
                <div v-if="profile.minimumSize || profile.maximumSize" class="limit-item">
                  <PhRuler />
                  <span class="limit-label">Size</span>
                  <span class="limit-value">
                    {{ profile.minimumSize || '0' }} - {{ profile.maximumSize || '∞' }} MB
                  </span>
                </div>
                <div v-if="profile.minimumSeeders && profile.minimumSeeders > 0" class="limit-item">
                  <PhUsers />
                  <span class="limit-label">Seeders</span>
                  <span class="limit-value">{{ profile.minimumSeeders }}+ required</span>
                </div>
                <div v-if="profile.minimumScore && profile.minimumScore > 0" class="limit-item">
                  <PhStar />
                  <span class="limit-label">Min Score</span>
                  <span class="limit-value">{{ profile.minimumScore }}+ required</span>
                </div>
                <div v-if="profile.maximumAge && profile.maximumAge > 0" class="limit-item">
                  <PhClock />
                  <span class="limit-label">Max Age</span>
                  <span class="limit-value">{{ profile.maximumAge }} days</span>
                </div>
              </div>
            </div>

            <!-- Word Filters Section -->
            <div
              v-if="
                profile.preferredWords?.length ||
                profile.mustContain?.length ||
                profile.mustNotContain?.length
              "
              class="profile-section"
            >
              <h5><PhTextAa /> Word Filters</h5>
              <div class="word-filters">
                <div
                  v-if="profile.preferredWords && profile.preferredWords.length > 0"
                  class="word-filter-group"
                >
                  <span class="filter-type">
                    <PhSparkle />
                    Preferred
                  </span>
                  <div class="word-tags">
                    <span
                      v-for="word in profile.preferredWords"
                      :key="word"
                      class="word-tag positive"
                    >
                      {{ word }}
                    </span>
                  </div>
                </div>
                <div
                  v-if="profile.mustContain && profile.mustContain.length > 0"
                  class="word-filter-group"
                >
                  <span class="filter-type">
                    <PhCheck />
                    Required
                  </span>
                  <div class="word-tags">
                    <span v-for="word in profile.mustContain" :key="word" class="word-tag required">
                      {{ word }}
                    </span>
                  </div>
                </div>
                <div
                  v-if="profile.mustNotContain && profile.mustNotContain.length > 0"
                  class="word-filter-group"
                >
                  <span class="filter-type">
                    <PhX />
                    Forbidden
                  </span>
                  <div class="word-tags">
                    <span
                      v-for="word in profile.mustNotContain"
                      :key="word"
                      class="word-tag forbidden"
                    >
                      {{ word }}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Quality Profile Form Modal -->
      <QualityProfileFormModal
        :visible="showQualityProfileForm"
        :profile="editingQualityProfile"
        @close="showQualityProfileForm = false; editingQualityProfile = null"
        @save="saveQualityProfile"
      />

      <!-- Delete Confirmation Modal -->
      <div v-if="profileToDelete" class="modal-overlay" @click="profileToDelete = null">
        <div class="modal-content" @click.stop>
          <div class="modal-header">
            <h3>
              <PhWarningCircle />
              Delete Quality Profile
            </h3>
            <button @click="profileToDelete = null" class="modal-close">
              <PhX />
            </button>
          </div>
          <div class="modal-body">
            <p>
              Are you sure you want to delete the quality profile
              <strong>{{ profileToDelete.name }}</strong
              >?
            </p>
            <p v-if="profileToDelete.isDefault" class="warning-text">
              <PhWarning />
              This is the default profile and cannot be deleted. Please set another profile as
              default first.
            </p>
            <p>This action cannot be undone.</p>
          </div>
          <div class="modal-actions">
            <button @click="profileToDelete = null" class="cancel-button">Cancel</button>
            <button
              @click="executeDeleteProfile"
              class="delete-button"
              :disabled="profileToDelete.isDefault"
            >
              <PhTrash />
              Delete
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from '@/services/toastService'
import { errorTracking } from '@/services/errorTracking'
import {
  getQualityProfiles,
  deleteQualityProfile,
  createQualityProfile,
  updateQualityProfile,
} from '@/services/api'
import type { QualityProfile } from '@/types'
import QualityProfileFormModal from '@/components/QualityProfileFormModal.vue'
import {
  PhStar,
  PhCheckCircle,
  PhPencil,
  PhTrash,
  PhCheckSquare,
  PhScissors,
  PhSliders,
  PhListChecks,
  PhRuler,
  PhUsers,
  PhClock,
  PhTextAa,
  PhSparkle,
  PhCheck,
  PhX,
  PhWarningCircle,
  PhWarning,
} from '@phosphor-icons/vue'

const toast = useToast()
const qualityProfiles = ref<QualityProfile[]>([])
const showQualityProfileForm = ref(false)
const editingQualityProfile = ref<QualityProfile | null>(null)
const profileToDelete = ref<QualityProfile | null>(null)

const formatApiError = (error: unknown): string => {
  if (typeof error === 'object' && error !== null) {
    const err = error as {
      response?: { data?: { message?: string; error?: string }; status?: number }
      message?: string
    }
    if (err.response?.data?.message) return err.response.data.message
    if (err.response?.data?.error) return err.response.data.error
    if (err.message) return err.message
  }
  return 'An unexpected error occurred'
}

const loadQualityProfiles = async () => {
  try {
    qualityProfiles.value = await getQualityProfiles()
  } catch (error) {
    errorTracking.captureException(error as Error, {
      component: 'QualityProfilesTab',
      operation: 'loadQualityProfiles',
    })
    const errorMessage = formatApiError(error)
    toast.error('Load failed', errorMessage)
  }
}

const openQualityProfileForm = (profile?: QualityProfile) => {
  editingQualityProfile.value = profile || null
  showQualityProfileForm.value = true
}

const editProfile = (profile: QualityProfile) => {
  editingQualityProfile.value = profile
  showQualityProfileForm.value = true
}

const confirmDeleteProfile = (profile: QualityProfile) => {
  profileToDelete.value = profile
}

const executeDeleteProfile = async () => {
  if (!profileToDelete.value) return

  try {
    await deleteQualityProfile(profileToDelete.value.id!)
    qualityProfiles.value = qualityProfiles.value.filter((p) => p.id !== profileToDelete.value!.id)
    toast.success('Quality profile', 'Quality profile deleted successfully')
  } catch (error: unknown) {
    errorTracking.captureException(error as Error, {
      component: 'QualityProfilesTab',
      operation: 'deleteQualityProfile',
    })
    const errorMessage = formatApiError(error)
    toast.error('Delete failed', errorMessage)
  } finally {
    profileToDelete.value = null
  }
}

const saveQualityProfile = async (profile: QualityProfile) => {
  try {
    if (profile.id) {
      // Update existing profile
      const updated = await updateQualityProfile(profile.id, profile)
      const index = qualityProfiles.value.findIndex((p) => p.id === profile.id)
      if (index !== -1) {
        qualityProfiles.value[index] = updated
      }
      toast.success('Quality profile', 'Quality profile updated successfully')
    } else {
      // Create new profile
      const created = await createQualityProfile(profile)
      qualityProfiles.value.push(created)
      toast.success('Quality profile', 'Quality profile created successfully')
    }
    showQualityProfileForm.value = false
    editingQualityProfile.value = null
  } catch (error: unknown) {
    errorTracking.captureException(error as Error, {
      component: 'QualityProfilesTab',
      operation: 'saveQualityProfile',
    })
    const errorMessage = formatApiError(error)
    toast.error('Save failed', errorMessage)
  }
}

const setDefaultProfile = async (profile: QualityProfile) => {
  try {
    // Update all profiles - set this one as default, others as non-default
    const updatedProfile = { ...profile, isDefault: true }
    await updateQualityProfile(profile.id!, updatedProfile)

    // Update local state
    qualityProfiles.value = qualityProfiles.value.map((p) => ({
      ...p,
      isDefault: p.id === profile.id,
    }))

    toast.success('Quality profile', `${profile.name} set as default quality profile`)
  } catch (error: unknown) {
    errorTracking.captureException(error as Error, {
      component: 'QualityProfilesTab',
      operation: 'setDefaultProfile',
    })
    const errorMessage = formatApiError(error)
    toast.error('Set default failed', errorMessage)
  }
}

onMounted(async () => {
  await loadQualityProfiles()
})

// Expose method for parent component to trigger add profile
defineExpose({
  openAddProfile: () => openQualityProfileForm(),
})
</script>

<style scoped>
.tab-content {
  animation: fadeIn 0.2s ease;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
}

.section-header h3 {
  font-size: 1.5rem;
  font-weight: 600;
  margin: 0;
}

.empty-state {
  text-align: center;
  padding: 3rem 2rem;
  color: var(--text-secondary);
}

.empty-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
  opacity: 0.5;
}

.empty-help {
  font-size: 0.9rem;
  margin-top: 0.5rem;
}

.profiles-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(500px, 1fr));
  gap: 1.5rem;
}

.profile-card {
  background-color: #2a2a2a;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 6px;
  overflow: hidden;
  transition: all 0.2s ease;
}

.profile-card:hover {
  border-color: rgba(77, 171, 247, 0.3);
  box-shadow: 0 4px 12px rgba(77, 171, 247, 0.15);
  transform: translateY(-1px);
}

.profile-card.is-default {
  border-color: rgba(77, 171, 247, 0.3);
  background: rgba(77, 171, 247, 0.05);
}

.profile-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1.5rem;
  background-color: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  gap: 0.5rem;
}

.profile-title-section {
  flex: 1;
}

.profile-name-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.7rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.status-badge.default {
  background-color: rgba(76, 175, 80, 0.15);
  color: #51cf66;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.profile-description {
  margin: 0;
  color: #868e96;
  font-size: 0.9rem;
  line-height: 1.5;
}

.profile-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: 1rem;
}

.profile-content {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.profile-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.profile-section h5 {
  margin: 0;
  color: #4dabf7;
  font-size: 0.9rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.profile-title-section {
  flex: 1;
  min-width: 0;
}

.profile-name-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.profile-name-row h4 {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 600;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.status-badge.default {
  background: rgba(33, 150, 243, 0.15);
  color: var(--primary-color);
}

.profile-description {
  margin: 0.5rem 0 0 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.profile-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.icon-button {
  background: transparent;
  border: 1px solid var(--border-color);
  padding: 0.5rem;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  font-size: 1.1rem;
}

.icon-button:hover:not(:disabled) {
  background: var(--bg-hover);
  border-color: var(--primary-color);
  color: var(--primary-color);
}

.icon-button.danger:hover:not(:disabled) {
  border-color: #f44336;
  color: #f44336;
}

.icon-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.profile-content {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.profile-section {
  padding: 1rem;
  background: var(--bg-tertiary);
  border-radius: 6px;
}

.profile-section h5 {
  margin: 0 0 0.75rem 0;
  font-size: 0.9rem;
  font-weight: 600;
  text-transform: uppercase;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.quality-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.quality-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.4rem 0.75rem;
  background: rgba(33, 150, 243, 0.15);
  color: var(--primary-color);
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  border: 1px solid rgba(33, 150, 243, 0.3);
}

.quality-badge.is-cutoff {
  background: rgba(76, 175, 80, 0.15);
  color: #4caf50;
  border-color: rgba(76, 175, 80, 0.3);
  font-weight: 600;
}

/* Icon sizing & color consistency inside profile cards */
.profile-header svg,
.profile-header .ph-icon,
.profile-name-row svg,
.status-badge svg {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
  vertical-align: middle;
}

.profile-header svg {
  color: #fff;
}

.profile-section h5 svg {
  width: 18px;
  height: 18px;
  color: #4dabf7;
}

.quality-badge svg {
  width: 14px;
  height: 14px;
}

.status-badge svg {
  width: 14px;
  height: 14px;
}

.preferences-grid,
.limits-grid {
  display: grid;
  gap: 0.75rem;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
}

.preference-item,
.limit-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.limit-item {
  flex-direction: row;
  align-items: center;
  gap: 0.5rem;
}

.preference-label,
.limit-label {
  font-size: 0.8rem;
  color: var(--text-secondary);
  font-weight: 500;
}

.preference-value,
.limit-value {
  font-size: 0.9rem;
  color: var(--text-primary);
}

.word-filters {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.word-filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.filter-type {
  font-size: 0.85rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--text-secondary);
}

.word-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.word-tag {
  padding: 0.35rem 0.75rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
}

.word-tag.positive {
  background: rgba(33, 150, 243, 0.15);
  color: var(--primary-color);
  border: 1px solid rgba(33, 150, 243, 0.3);
}

.word-tag.required {
  background: rgba(76, 175, 80, 0.15);
  color: #4caf50;
  border: 1px solid rgba(76, 175, 80, 0.3);
}

.word-tag.forbidden {
  background: rgba(244, 67, 54, 0.15);
  color: #f44336;
  border: 1px solid rgba(244, 67, 54, 0.3);
}

/* Modal Styles */
/* Modal Styles (canonical) */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.85);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}

.modal-content {
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 6px;
  max-width: 700px;
  width: 100%;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem;
  border-bottom: 1px solid #444;
}

.modal-header h3 {
  margin: 0;
  color: #fff;
  font-size: 1.25rem;
}

.modal-close {
  background: none;
  border: none;
  color: #b3b3b3;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 6px;
  transition: all 0.2s;
}

.modal-close:hover {
  background: #333;
  color: #fff;
}

.modal-body {
  padding: 2rem;
  overflow-y: auto;
  flex: 1;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  padding: 1.5rem;
  border-top: 1px solid #444;
}

/* Ensure modal context delete buttons are full-size */
.modal-overlay .modal-content .modal-actions .delete-button,
.modal-content .modal-actions .delete-button,
.modal-overlay .modal-content .modal-actions .modal-delete-button,
.modal-content .modal-actions .modal-delete-button {
  padding: 0.75rem 1.25rem;
  background-color: rgba(231, 76, 60, 0.15);
  color: #ff6b6b;
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.18s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.6rem;
  font-weight: 700;
  font-size: 1rem;
  min-width: 120px;
  height: auto;
  box-shadow: 0 6px 16px rgba(231, 76, 60, 0.12);
}

.cancel-button {
  padding: 0.75rem 1.5rem;
  background: var(--bg-secondary);
  color: var(--text-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
  font-size: 0.95rem;
}

.cancel-button:hover {
  background: var(--bg-hover);
}
</style>
