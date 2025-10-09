import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { ApiConfiguration, DownloadClientConfiguration, ApplicationSettings, QualityProfile } from '@/types'
import { apiService } from '@/services/api'

export const useConfigurationStore = defineStore('configuration', () => {
  const apiConfigurations = ref<ApiConfiguration[]>([])
  const downloadClientConfigurations = ref<DownloadClientConfiguration[]>([])
  const applicationSettings = ref<ApplicationSettings | null>(null)
  const qualityProfiles = ref<QualityProfile[]>([])
  const isLoading = ref(false)
  
  const loadApiConfigurations = async () => {
    isLoading.value = true
    try {
      const configs = await apiService.getApiConfigurations()
      apiConfigurations.value = configs
    } catch (error) {
      console.error('Failed to load API configurations:', error)
    } finally {
      isLoading.value = false
    }
  }
  
  const saveApiConfiguration = async (config: ApiConfiguration) => {
    try {
      await apiService.saveApiConfiguration(config)
      await loadApiConfigurations() // Refresh the list
    } catch (error) {
      console.error('Failed to save API configuration:', error)
      throw error
    }
  }
  
  const deleteApiConfiguration = async (id: string) => {
    try {
      await apiService.deleteApiConfiguration(id)
      await loadApiConfigurations() // Refresh the list
    } catch (error) {
      console.error('Failed to delete API configuration:', error)
      throw error
    }
  }
  
  const loadDownloadClientConfigurations = async () => {
    isLoading.value = true
    try {
      const configs = await apiService.getDownloadClientConfigurations()
      downloadClientConfigurations.value = configs
    } catch (error) {
      console.error('Failed to load download client configurations:', error)
    } finally {
      isLoading.value = false
    }
  }
  
  const saveDownloadClientConfiguration = async (config: DownloadClientConfiguration) => {
    try {
      await apiService.saveDownloadClientConfiguration(config)
      await loadDownloadClientConfigurations() // Refresh the list
    } catch (error) {
      console.error('Failed to save download client configuration:', error)
      throw error
    }
  }
  
  const deleteDownloadClientConfiguration = async (id: string) => {
    try {
      await apiService.deleteDownloadClientConfiguration(id)
      await loadDownloadClientConfigurations() // Refresh the list
    } catch (error) {
      console.error('Failed to delete download client configuration:', error)
      throw error
    }
  }
  
  const loadApplicationSettings = async () => {
    isLoading.value = true
    try {
      const settings = await apiService.getApplicationSettings()
      applicationSettings.value = settings
    } catch (error) {
      console.error('Failed to load application settings:', error)
    } finally {
      isLoading.value = false
    }
  }
  
  const saveApplicationSettings = async (settings: ApplicationSettings) => {
    try {
      const savedSettings = await apiService.saveApplicationSettings(settings)
      applicationSettings.value = savedSettings
    } catch (error) {
      console.error('Failed to save application settings:', error)
      throw error
    }
  }
  
  const loadQualityProfiles = async () => {
    isLoading.value = true
    try {
      const profiles = await apiService.getQualityProfiles()
      qualityProfiles.value = profiles
    } catch (error) {
      console.error('Failed to load quality profiles:', error)
    } finally {
      isLoading.value = false
    }
  }
  
  const saveQualityProfile = async (profile: QualityProfile) => {
    try {
      if (profile.id) {
        await apiService.updateQualityProfile(profile.id, profile)
      } else {
        await apiService.createQualityProfile(profile)
      }
      await loadQualityProfiles() // Refresh the list
    } catch (error) {
      console.error('Failed to save quality profile:', error)
      throw error
    }
  }
  
  const deleteQualityProfile = async (id: number) => {
    try {
      await apiService.deleteQualityProfile(id)
      await loadQualityProfiles() // Refresh the list
    } catch (error) {
      console.error('Failed to delete quality profile:', error)
      throw error
    }
  }
  
  return {
    apiConfigurations,
    downloadClientConfigurations,
    applicationSettings,
    qualityProfiles,
    isLoading,
    loadApiConfigurations,
    saveApiConfiguration,
    deleteApiConfiguration,
    loadDownloadClientConfigurations,
    saveDownloadClientConfiguration,
    deleteDownloadClientConfiguration,
    loadApplicationSettings,
    saveApplicationSettings,
    loadQualityProfiles,
    saveQualityProfile,
    deleteQualityProfile
  }
})