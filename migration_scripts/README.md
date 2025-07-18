# Database Migration Scripts

This directory contains all database migration scripts for the NewWords.Api project.

## Migration History

### 01-03: WordCollection Language Field Removal Migration
This migration removes the `Language` field from the `WordCollection` table to simplify the architecture and eliminate language detection ambiguity.

### 04-05: Per-User Story Read Tracking Migration  
This migration implements per-user read tracking for stories, fixing the issue where `Stories.FirstReadAt` was story-level instead of per-user.

## Migration Steps

### Phase 1: Analysis
1. **Run analysis script:**
   ```sql
   source 01_analyze_duplicates.sql
   ```
   - Review the duplicate analysis results
   - Note which words have multiple language entries
   - Understand the scope of data consolidation needed

### Phase 2: Data Consolidation
2. **Run merge script:**
   ```sql
   source 02_merge_duplicates.sql
   ```
   - **CRITICAL:** This script modifies data. Ensure you have a backup.
   - Updates `WordExplanation` foreign key references
   - Merges duplicate `WordCollection` records by summing `QueryCount`
   - Soft-deletes duplicate records (keeps earliest `CreatedAt`)
   - Creates audit log in temporary `merge_log` table

3. **Verify merge results:**
   - Check that `Orphaned WordExplanations` count is 0
   - Check that `Remaining Duplicates` count is 0
   - Review `merge_log` table for audit trail

### Phase 3: Code Deployment
4. **Deploy updated codebase:**
   - The code changes have already been made
   - Deploy the updated application with removed `Language` field references
   - Test that word addition and explanation generation still work

### Phase 4: Schema Update
5. **Run final migration:**
   ```sql
   source 03_drop_language_column.sql
   ```
   - **IMPORTANT:** Permanently deletes soft-deleted records first (required for unique index)
   - Drops old unique index `WordText` (WordText + Language)
   - Creates new unique index `UQ_WordCollection_WordText` (WordText only)
   - Drops the `Language` column
   - Verifies no duplicates remain

**Note:** The cleanup of soft-deleted records is now integrated into step 3 since it's required before creating the new unique index.

## Rollback Plan

If you need to rollback:

1. **Before running merge script:** Simply restore from backup
2. **After running merge script:** 
   - Use the `merge_log` table to identify what was changed
   - Restore from backup (recommended)
   - Or manually recreate duplicate records if backup is not available

## Key Changes Made

### Database Schema:
- **Before:** `WordCollection` had `Language` field with compound unique key
- **After:** `WordCollection` has only `WordText` with simple unique key

### Code Logic:
- **Before:** Complex language-based matching in `_HandleWordCollection`
- **After:** Simple text-based lookup, language-agnostic storage

### WordExplanation:
- **No changes** - Still maintains `LearningLanguage` from user profile
- Foreign key integrity preserved through merge script

## Benefits

1. **Simplified Logic:** No more language detection for storage
2. **Better Accuracy:** User's `LearningLanguage` always correct from profile  
3. **Unified Tracking:** `QueryCount` represents total popularity across languages
4. **Reduced Complexity:** One record per unique word text

## Testing Checklist

After WordCollection migration:
- [ ] Can add new words via API
- [ ] Word explanations generate correctly
- [ ] `QueryCount` increments properly
- [ ] No duplicate `WordText` entries exist
- [ ] `WordExplanations` foreign keys are valid
- [ ] Bulk explanation generation works (`FillWordExplanationsTable`)
- [ ] Duplicate records permanently removed

---

## Per-User Story Read Tracking Migration (04-05)

### Overview
Fixes the design issue where `Stories.FirstReadAt` was story-level instead of per-user, preventing proper read tracking when users discover stories created by others.

### Migration Steps

#### Phase 1: Create Infrastructure & Migrate Data
1. **Run migration script:**
   ```sql
   source 04_user_story_reads_migration.sql
   ```
   - Creates `UserStoryReads` table with proper indexes (re-runnable)
   - Migrates existing `Stories.FirstReadAt` data to preserve read status
   - Skips duplicate entries if script is run multiple times
   - Verifies migration completed successfully

#### Phase 2: Code Deployment  
2. **Deploy updated codebase:**
   - Updated service layer with optimized SqlSugar joins
   - Removed ownership restriction from `MarkStoryAsReadAsync`
   - `Story.FirstReadAt` becomes computed property (user-context-aware)

#### Phase 3: Schema Cleanup (Optional)
3. **Run cleanup script after verification:**
   ```sql
   source 05_cleanup_stories_firstreadat.sql
   ```
   - **Optional:** Removes old `FirstReadAt` column from `Stories` table (re-runnable)
   - Safely checks if column exists before dropping
   - Run only after confirming everything works correctly

### Key Changes

#### Database Schema:
- **Before:** `Stories.FirstReadAt` (story-level, single timestamp)
- **After:** `UserStoryReads` table (per-user read tracking)

#### API Behavior:
- **Before:** `MarkStoryAsReadAsync` only worked for story owners
- **After:** Any user can mark any story as read
- **Frontend:** Zero changes needed - API contracts unchanged

#### Performance:
- **Before:** N+1 queries in `ConvertToStoryDtosAsync`
- **After:** Single query with LEFT JOINs for favorites and read status

### Benefits

1. **Proper Read Tracking:** Users can read stories from Story Square
2. **Zero Frontend Changes:** `StoryDto.FirstReadAt` still works as expected  
3. **Better Performance:** Single optimized query instead of multiple lookups
4. **Data Preservation:** All existing read data migrated safely

### Testing Checklist

After migration:
- [ ] Story Square shows stories from other users
- [ ] Users can mark any story as read (not just their own)
- [ ] Read status persists correctly per user
- [ ] Stories show proper read status in all endpoints
- [ ] Favorite functionality still works
- [ ] Migration preserved all existing read data
- [ ] Performance improved (check query execution plans)