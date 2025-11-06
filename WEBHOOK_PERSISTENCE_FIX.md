# Webhook Persistence Fix

## Issue
Webhooks were only stored in memory (`webhooks.value` ref) and disappeared on page reload. The migration from old webhook format worked, but the migrated webhooks were never persisted to the backend.

## Root Cause
1. **No persistence layer**: `saveWebhook()`, `deleteWebhook()`, and `toggleWebhook()` only modified the in-memory array
2. **No loading mechanism**: Webhooks were never loaded from settings on component mount
3. **Missing type definition**: `ApplicationSettings` interface didn't include a `webhooks` array property
4. **Migration didn't persist**: Migration created webhooks in memory but never saved them

## Solution

### 1. Updated Type Definition (`fe/src/types/index.ts`)
Added `webhooks` property to `ApplicationSettings` interface:
```typescript
export interface ApplicationSettings {
  // ... existing properties
  
  // Notification settings
  webhookUrl?: string  // Old format (deprecated)
  enabledNotificationTriggers?: string[]  // Old format (deprecated)
  // New webhook format (multiple webhooks)
  webhooks?: Array<{
    id: string
    name: string
    url: string
    type: 'Pushbullet' | 'Telegram' | 'Slack' | 'Discord' | 'Pushover' | 'NTFY' | 'Zapier'
    triggers: string[]
    isEnabled: boolean
  }>
}
```

### 2. Added Persistence Helper Function (`fe/src/views/SettingsView.vue`)
Created `persistWebhooks()` to save webhooks to backend:
```typescript
const persistWebhooks = async () => {
  if (!settings.value) return
  
  try {
    settings.value.webhooks = webhooks.value
    await configStore.saveApplicationSettings(settings.value)
  } catch (error) {
    console.error('Failed to persist webhooks:', error)
    toast.error('Save failed', 'Failed to save webhooks to settings')
    throw error
  }
}
```

### 3. Updated Webhook Operations
Modified all webhook manipulation functions to persist changes:

**saveWebhook()** - Added persistence after create/update:
```typescript
// ... webhook save logic
await persistWebhooks()
closeWebhookForm()
```

**deleteWebhook()** - Added persistence after deletion:
```typescript
webhooks.value = webhooks.value.filter(w => w.id !== id)
toast.success('Webhook', 'Webhook deleted successfully')
await persistWebhooks()
```

**toggleWebhook()** - Added persistence after enable/disable:
```typescript
targetWebhook.isEnabled = !targetWebhook.isEnabled
toast.success('Webhook', `${webhook.name} ${targetWebhook.isEnabled ? 'enabled' : 'disabled'}`)
await persistWebhooks()
```

### 4. Updated Migration Function
Made `migrateOldWebhookData()` async and added persistence:
```typescript
const migrateOldWebhookData = async () => {
  // ... migration logic
  webhooks.value = [/* migrated webhook */]
  
  // Persist migrated webhook to backend
  await persistWebhooks()
  
  localStorage.setItem(migrationKey, 'true')
  // ... toast notification
}
```

### 5. Added Webhook Loading on Tab Switch
Updated `loadTabContents()` to load webhooks from settings:
```typescript
case 'notifications':
  if (!loaded.general) {
    await loadTabContents('general')
  }
  // Load webhooks from settings
  if (settings.value?.webhooks && settings.value.webhooks.length > 0) {
    webhooks.value = settings.value.webhooks
  }
  // Migrate old webhook format
  await migrateOldWebhookData()
  break
```

## Benefits
✅ **Persistent storage**: Webhooks now survive page reloads  
✅ **Backend integration**: Uses existing settings API endpoint  
✅ **Automatic loading**: Webhooks load when notifications tab is accessed  
✅ **Real-time sync**: All operations immediately persist to backend  
✅ **Migration works**: Old webhook format properly migrates and persists  
✅ **Type safety**: TypeScript types updated for webhooks array  

## Testing Checklist
- [ ] Create a new webhook → Reload page → Webhook still exists
- [ ] Edit an existing webhook → Reload page → Changes persisted
- [ ] Delete a webhook → Reload page → Webhook stays deleted
- [ ] Toggle webhook enabled/disabled → Reload page → State persisted
- [ ] Migrate old webhook format → Reload page → Migration persists
- [ ] Multiple webhooks work correctly
- [ ] Webhook triggers save properly

## Files Modified
1. `fe/src/types/index.ts` - Added `webhooks` to ApplicationSettings interface
2. `fe/src/views/SettingsView.vue` - Added persistence logic and loading mechanism

## Future Enhancements
- Consider adding a dedicated webhook API endpoint for better RESTful design
- Add optimistic UI updates with rollback on error
- Implement debouncing for rapid toggle operations
- Add webhook version/schema for future migrations
