-- Migration 08: Fix UserWords unique constraint
-- Change unique constraint from (UserId, WordCollectionId) to (UserId, WordExplanationId)
-- This allows users to have multiple explanations for the same word while preventing duplicate user-explanation pairs

-- Step 1: First, let's analyze the current data to understand the impact
SELECT 'Current UserWords count' as info, COUNT(*) as count FROM `UserWords`;

SELECT 'Unique (UserId, WordCollectionId) pairs' as info, COUNT(*) as count 
FROM (SELECT DISTINCT UserId, WordCollectionId FROM `UserWords`) t;

SELECT 'Unique (UserId, WordExplanationId) pairs' as info, COUNT(*) as count 
FROM (SELECT DISTINCT UserId, WordExplanationId FROM `UserWords`) t;

-- Step 2: Check for potential duplicate (UserId, WordExplanationId) pairs
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

-- Step 3: If there are duplicates, clean them up (keep the most recent one)
-- This will be done in a transaction for safety
START TRANSACTION;

-- For each duplicate group, keep only the record with the most recent UpdatedAt
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

-- Step 4: Drop the existing unique constraint
ALTER TABLE `UserWords` DROP INDEX `UQ_UserWords_UserId_WordCollectionId`;

-- Step 5: Create the new unique constraint
ALTER TABLE `UserWords` ADD UNIQUE INDEX `UQ_UserWords_UserId_WordExplanationId` (`UserId`, `WordExplanationId`);

-- Commit the transaction
COMMIT;

-- Step 6: Final verification
SELECT 'Migration completed. Verifying new constraint...' as info;
SHOW INDEX FROM `UserWords` WHERE Key_name = 'UQ_UserWords_UserId_WordExplanationId';

SELECT 'Final UserWords count' as info, COUNT(*) as count FROM `UserWords`;