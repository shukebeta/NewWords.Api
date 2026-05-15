USE NewWords;
-- Add UpdatedAt column to UserWords table (MySQL 8 compatible, idempotent)
-- This enables re-adding words to bump them to the front of the user's timeline

-- Step 1: Add UpdatedAt column only if it doesn't exist (idempotent)
SET @column_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND COLUMN_NAME = 'UpdatedAt'
);

SET @add_column_sql = IF(
    @column_exists = 0,
    'ALTER TABLE UserWords ADD COLUMN UpdatedAt BIGINT NULL',
    'DO 0'
);

PREPARE stmt FROM @add_column_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Step 2: Initialize UpdatedAt with CreatedAt for records where it's NULL (idempotent)
UPDATE UserWords
SET UpdatedAt = CreatedAt
WHERE UpdatedAt IS NULL;

-- Step 3: Make UpdatedAt NOT NULL only if currently nullable (idempotent check)
SET @column_nullable = (
    SELECT IS_NULLABLE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND COLUMN_NAME = 'UpdatedAt'
);

-- Only modify column if it's currently nullable
SET @alter_sql = IF(
    @column_nullable = 'YES',
    'ALTER TABLE UserWords MODIFY COLUMN UpdatedAt BIGINT NOT NULL DEFAULT 0',
    'DO 0'
);

PREPARE stmt FROM @alter_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Step 4: Verification queries
SELECT
    'UserWords total records' as Metric,
    COUNT(*) as Count
FROM UserWords

UNION ALL

SELECT
    'Records with UpdatedAt set' as Metric,
    COUNT(*) as Count
FROM UserWords
WHERE UpdatedAt > 0

UNION ALL

SELECT
    'Records where UpdatedAt = CreatedAt' as Metric,
    COUNT(*) as Count
FROM UserWords
WHERE UpdatedAt = CreatedAt;

-- Show sample data to verify
SELECT
    Id,
    UserId,
    WordExplanationId,
    CreatedAt,
    UpdatedAt,
    (UpdatedAt - CreatedAt) as TimeDifference
FROM UserWords
ORDER BY UpdatedAt DESC
LIMIT 10;

-- Show table structure
DESCRIBE UserWords;
