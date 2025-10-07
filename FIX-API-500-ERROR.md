# Fix for API 500 Error - Database Schema Out of Sync

## Problem
The API is returning HTTP 500 errors because we added the `QualityProfileId` field to the Audiobook model, 
but the database doesn't have this column yet.

## Solution
Apply the database migration that was just created.

## Steps to Fix

### 1. Stop the API
Kill the running API process (PID can be found with `tasklist | grep Listenarr`)

### 2. Apply the Migration
```bash
cd listenarr.api
dotnet ef database update
```

This will:
- Add the `QualityProfileId` column to the `Audiobooks` table
- Create the new `QualityProfiles` table
- Fix the 500 error

### 3. Fix Monitored Audiobooks (Optional)
If you want existing audiobooks to show in the Wanted view:
```bash
sqlite3 listenarr.db "UPDATE Audiobooks SET Monitored = 1 WHERE FilePath IS NULL OR FilePath = ''"
```

### 4. Restart the API
```bash
dotnet run --urls http://localhost:5146
```

## What Was Changed

### Backend
1. ✅ Added `QualityProfile` model with comprehensive quality criteria
2. ✅ Added `QualityProfileId` to Audiobook model  
3. ✅ Created database migration `20251007215339_AddQualityProfiles`
4. ✅ Fixed LibraryController to set `Monitored = true` by default
5. ✅ Updated DbContext with QualityProfile entity configuration

### Frontend
1. ✅ Enhanced WantedView filtering logic
2. ✅ Added debugging console logs
3. ✅ Fixed TypeScript null safety issues

## Expected Result
After applying the migration and restarting:
- ✅ API will start without errors
- ✅ GET /api/library will return 200 OK
- ✅ Wanted view will load successfully
- ✅ New audiobooks will be monitored by default
- ✅ Quality Profiles system ready for implementation
