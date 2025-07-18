-- CLEANUP SCRIPT: Remove FirstReadAt column from Stories table
-- Re-runnable: Checks if column exists before dropping
-- Run this AFTER verifying the UserStoryReads implementation works correctly

-- Verify data has been migrated successfully
SELECT 
    'Stories with FirstReadAt' as DataType,
    COUNT(*) as Count
FROM Stories 
WHERE FirstReadAt IS NOT NULL

UNION ALL

SELECT 
    'UserStoryReads records' as DataType,
    COUNT(*) as Count
FROM UserStoryReads;

-- Re-runnable: Check if column exists before dropping
-- This will only drop the column if it exists
SET @column_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() 
      AND TABLE_NAME = 'Stories' 
      AND COLUMN_NAME = 'FirstReadAt'
);

SET @sql = IF(@column_exists > 0, 
    'ALTER TABLE Stories DROP COLUMN FirstReadAt', 
    'SELECT "FirstReadAt column already removed from Stories table" as status'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;