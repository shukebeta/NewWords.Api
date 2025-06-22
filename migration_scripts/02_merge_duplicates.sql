USE NewWords;
-- Migration Script: Merge WordCollection Duplicates
-- This script consolidates duplicate WordText records in WordCollection
-- WARNING: Run 01_analyze_duplicates.sql first to understand the scope

-- Create a temporary table to track merge operations
CREATE TEMPORARY TABLE IF NOT EXISTS merge_log (
    id INT AUTO_INCREMENT PRIMARY KEY,
    original_word_text VARCHAR(255),
    kept_id BIGINT,
    removed_ids TEXT,
    total_query_count INT,
    merge_timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Step 1: Update WordExplanations foreign key references
-- For each duplicate group, update all references to point to the record we'll keep
UPDATE `WordExplanations` we
INNER JOIN `WordCollection` wc ON we.`WordCollectionId` = wc.`Id`
INNER JOIN (
    -- Subquery to find the record to keep for each WordText (earliest CreatedAt)
    SELECT 
        `WordText`,
        MIN(`Id`) as keep_id
    FROM `WordCollection`
    WHERE `DeletedAt` IS NULL
    GROUP BY `WordText`
    HAVING COUNT(*) > 1
) keeper ON wc.`WordText` = keeper.`WordText` AND wc.`Id` != keeper.keep_id
SET we.`WordCollectionId` = keeper.keep_id
WHERE wc.`DeletedAt` IS NULL;

-- Step 2: Log merge operations for audit trail
INSERT INTO merge_log (original_word_text, kept_id, removed_ids, total_query_count)
SELECT 
    `WordText`,
    MIN(`Id`) as kept_id,
    GROUP_CONCAT(`Id` ORDER BY `Id`) as all_ids,
    SUM(`QueryCount`) as total_query_count
FROM `WordCollection`
WHERE `DeletedAt` IS NULL
GROUP BY `WordText`
HAVING COUNT(*) > 1;

-- Step 3: Update the QueryCount of records we're keeping
UPDATE `WordCollection` wc
INNER JOIN (
    SELECT 
        `WordText`,
        MIN(`Id`) as keep_id,
        SUM(`QueryCount`) as total_query_count,
        MIN(`CreatedAt`) as earliest_created,
        MAX(`UpdatedAt`) as latest_updated
    FROM `WordCollection`
    WHERE `DeletedAt` IS NULL
    GROUP BY `WordText`
    HAVING COUNT(*) > 1
) summary ON wc.`Id` = summary.keep_id
SET 
    wc.`QueryCount` = summary.total_query_count,
    wc.`CreatedAt` = summary.earliest_created,
    wc.`UpdatedAt` = summary.latest_updated
WHERE wc.`DeletedAt` IS NULL;

-- Step 4: Soft delete duplicate records (keeping the earliest created one)
UPDATE `WordCollection` wc
INNER JOIN (
    SELECT 
        `WordText`,
        MIN(`Id`) as keep_id
    FROM `WordCollection`
    WHERE `DeletedAt` IS NULL
    GROUP BY `WordText`
    HAVING COUNT(*) > 1
) keeper ON wc.`WordText` = keeper.`WordText` AND wc.`Id` != keeper.keep_id
SET wc.`DeletedAt` = UNIX_TIMESTAMP()
WHERE wc.`DeletedAt` IS NULL;

-- Step 5: Verification queries
-- Check that no WordExplanations references deleted WordCollection records
SELECT 
    'Orphaned WordExplanations' as Check_Name,
    COUNT(*) as Count
FROM `WordExplanations` we
INNER JOIN `WordCollection` wc ON we.`WordCollectionId` = wc.`Id`
WHERE wc.`DeletedAt` IS NOT NULL;

-- Verify no duplicates remain
SELECT 
    'Remaining Duplicates' as Check_Name,
    COUNT(*) as Count
FROM (
    SELECT `WordText`
    FROM `WordCollection` 
    WHERE `DeletedAt` IS NULL
    GROUP BY `WordText`
    HAVING COUNT(*) > 1
) remaining_dupes;

-- Show merge summary
SELECT 
    'Total Merges Performed' as Summary,
    COUNT(*) as Count
FROM merge_log;

SELECT 
    'Records Soft Deleted' as Summary,
    COUNT(*) as Count
FROM `WordCollection`
WHERE `DeletedAt` IS NOT NULL;

-- Show merge log (for audit)
SELECT * FROM merge_log ORDER BY merge_timestamp;