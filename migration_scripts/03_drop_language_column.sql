USE NewWords;
-- Final Migration: Drop Language column from WordCollection
-- Run this AFTER successfully running the merge script and updating the codebase

-- Step 1: Permanently delete soft-deleted duplicate records FIRST
-- This is required before creating the new unique index
DELETE FROM WordCollection 
WHERE DeletedAt IS NOT NULL;

-- Step 2: Drop the old unique index that includes Language
-- Note: Using ALTER TABLE syntax for better MySQL compatibility
ALTER TABLE WordCollection DROP INDEX WordText;

-- Step 3: Create new unique index on WordText only
CREATE UNIQUE INDEX UQ_WordCollection_WordText ON WordCollection (WordText);

-- Step 4: Drop the Language column
ALTER TABLE WordCollection DROP COLUMN Language;

-- Step 5: Clean up temporary table from merge process
DROP TEMPORARY TABLE IF EXISTS merge_log;

-- Step 6: Verification queries
-- Verify unique constraint is working
SELECT 
    'Duplicate WordText Check' as Verification,
    COUNT(*) as DuplicateCount
FROM (
    SELECT WordText
    FROM WordCollection 
    WHERE DeletedAt IS NULL
    GROUP BY WordText
    HAVING COUNT(*) > 1
) duplicates;

-- Show final table structure
DESCRIBE `WordCollection`;

-- Show sample data
SELECT 
    `Id`,
    `WordText`,
    `QueryCount`,
    `CreatedAt`,
    `UpdatedAt`,
    `DeletedAt`
FROM `WordCollection` 
WHERE `DeletedAt` IS NULL
ORDER BY `QueryCount` DESC
LIMIT 10;