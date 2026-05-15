USE NewWords;
-- Migration 08: Fix UserWords unique constraint (IDEMPOTENT)
-- Change unique constraint from (UserId, WordCollectionId) to (UserId, WordExplanationId)
-- This allows users to have multiple explanations for the same word while preventing duplicate user-explanation pairs
-- This script can be run multiple times safely.

-- Step 1: Check if migration is needed
SELECT
    CASE
        WHEN EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = DATABASE()
            AND table_name = 'UserWords'
            AND index_name = 'UQ_UserWords_UserId_WordExplanationId'
        ) THEN 'Migration already completed - new constraint exists'
        WHEN EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = DATABASE()
            AND table_name = 'UserWords'
            AND index_name = 'UQ_UserWords_UserId_WordCollectionId'
        ) THEN 'Migration needed - old constraint exists'
        ELSE 'Warning: Neither constraint exists - check table structure'
    END as migration_status;

-- Step 2: First, let's analyze the current data to understand the impact (only if table exists)
SELECT 'Current UserWords count' as info, COUNT(*) as count FROM `UserWords` WHERE 1=1;

SELECT 'Unique (UserId, WordCollectionId) pairs' as info, COUNT(*) as count
FROM (SELECT DISTINCT UserId, WordCollectionId FROM `UserWords`) t;

SELECT 'Unique (UserId, WordExplanationId) pairs' as info, COUNT(*) as count
FROM (SELECT DISTINCT UserId, WordExplanationId FROM `UserWords`) t;

-- Step 3: Check for potential duplicate (UserId, WordExplanationId) pairs
SELECT 'Checking for duplicates (UserId, WordExplanationId)...' as info;
SELECT
    UserId,
    WordExplanationId,
    COUNT(*) as duplicate_count,
    GROUP_CONCAT(Id ORDER BY UpdatedAt DESC) as duplicate_ids_newest_first,
    GROUP_CONCAT(CreatedAt ORDER BY UpdatedAt DESC) as created_times,
    GROUP_CONCAT(UpdatedAt ORDER BY UpdatedAt DESC) as updated_times
FROM `UserWords`
GROUP BY UserId, WordExplanationId
HAVING COUNT(*) > 1
ORDER BY duplicate_count DESC, UserId, WordExplanationId;

-- Step 4: Perform migration only if needed
START TRANSACTION;

-- Clean up duplicates (safe to run multiple times)
DELETE uw1 FROM `UserWords` uw1
INNER JOIN `UserWords` uw2 ON
    uw1.UserId = uw2.UserId
    AND uw1.WordExplanationId = uw2.WordExplanationId
    AND (uw1.UpdatedAt < uw2.UpdatedAt OR (uw1.UpdatedAt = uw2.UpdatedAt AND uw1.Id < uw2.Id));

-- Verify no duplicates remain
SELECT 'Verifying no duplicates remain...' as info;
SELECT COUNT(*) as remaining_duplicates
FROM (
    SELECT UserId, WordExplanationId, COUNT(*) as cnt
    FROM `UserWords`
    GROUP BY UserId, WordExplanationId
    HAVING COUNT(*) > 1
) t;

-- Drop the old constraint if it exists (idempotent)
SET @old_constraint_exists = (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
    AND table_name = 'UserWords'
    AND index_name = 'UQ_UserWords_UserId_WordCollectionId'
);

SET @sql = IF(@old_constraint_exists > 0,
    'ALTER TABLE `UserWords` DROP INDEX `UQ_UserWords_UserId_WordCollectionId`',
    'SELECT "Old constraint does not exist, skipping drop" as info'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Create the new constraint if it doesn't exist (idempotent)
SET @new_constraint_exists = (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
    AND table_name = 'UserWords'
    AND index_name = 'UQ_UserWords_UserId_WordExplanationId'
);

SET @sql = IF(@new_constraint_exists = 0,
    'ALTER TABLE `UserWords` ADD UNIQUE INDEX `UQ_UserWords_UserId_WordExplanationId` (`UserId`, `WordExplanationId`)',
    'SELECT "New constraint already exists, skipping creation" as info'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Commit the transaction
COMMIT;

-- Step 5: Final verification
SELECT 'Migration completed. Verifying constraints...' as info;

SELECT
    index_name,
    column_name,
    seq_in_index,
    non_unique
FROM information_schema.statistics
WHERE table_schema = DATABASE()
AND table_name = 'UserWords'
AND index_name IN ('UQ_UserWords_UserId_WordCollectionId', 'UQ_UserWords_UserId_WordExplanationId')
ORDER BY index_name, seq_in_index;

SELECT 'Final UserWords count' as info, COUNT(*) as count FROM `UserWords`;
