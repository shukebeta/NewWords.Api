USE NewWords;
-- Add WordCollectionId to UserWords and update unique constraint
-- Enables multiple explanations per word with user-specific default selection
-- MySQL 8 compatible, fully idempotent (safe to run multiple times)

-- ============================================================================
-- Step 1: Add WordCollectionId column if it doesn't exist
-- ============================================================================
SET @column_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND COLUMN_NAME = 'WordCollectionId'
);

SET @add_column_sql = IF(
    @column_exists = 0,
    'ALTER TABLE UserWords ADD COLUMN WordCollectionId BIGINT NULL AFTER UserId',
    'SELECT 1'  -- No-op statement
);

PREPARE stmt FROM @add_column_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Log status for debugging
SELECT IF(@column_exists = 0,
    'Added WordCollectionId column',
    'WordCollectionId column already exists') as Step1_Status;

-- ============================================================================
-- Step 2: Populate WordCollectionId from WordExplanation table
-- ============================================================================
UPDATE UserWords uw
INNER JOIN WordExplanations we ON uw.WordExplanationId = we.Id
SET uw.WordCollectionId = we.WordCollectionId
WHERE uw.WordCollectionId IS NULL;

-- ============================================================================
-- Step 3: Make WordCollectionId NOT NULL (only if currently nullable)
-- ============================================================================
SET @column_nullable = (
    SELECT IS_NULLABLE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND COLUMN_NAME = 'WordCollectionId'
);

SET @alter_nullable_sql = IF(
    @column_nullable = 'YES',
    'ALTER TABLE UserWords MODIFY COLUMN WordCollectionId BIGINT NOT NULL',
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @alter_nullable_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT IF(@column_nullable = 'YES',
    'Made WordCollectionId NOT NULL',
    'WordCollectionId is already NOT NULL') as Step3_Status;

-- ============================================================================
-- Step 4: Drop unique constraint on WordExplanations (allow multiple explanations per word)
-- ============================================================================
SET @word_exp_unique_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'WordExplanations'
      AND INDEX_NAME = 'UQ_Words_Text_Lang_NativeLang'
);

SET @drop_word_exp_unique_sql = IF(
    @word_exp_unique_exists > 0,
    'ALTER TABLE WordExplanations DROP INDEX UQ_Words_Text_Lang_NativeLang',
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @drop_word_exp_unique_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT IF(@word_exp_unique_exists > 0,
    'Dropped unique index from WordExplanations: UQ_Words_Text_Lang_NativeLang',
    'Unique index on WordExplanations does not exist') as Step4_Status;

-- ============================================================================
-- Step 5: Create new unique index on WordExplanations (including ProviderModelName)
-- ============================================================================
-- This allows multiple explanations per word (one per model)
SET @new_word_exp_unique_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'WordExplanations'
      AND INDEX_NAME = 'UQ_WordExplanations_Collection_Languages_Provider'
);

SET @create_word_exp_unique_sql = IF(
    @new_word_exp_unique_exists = 0,
    'ALTER TABLE WordExplanations ADD UNIQUE INDEX UQ_WordExplanations_Collection_Languages_Provider (WordCollectionId, LearningLanguage, ExplanationLanguage, ProviderModelName)',
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @create_word_exp_unique_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT IF(@new_word_exp_unique_exists = 0,
    'Created new unique index on WordExplanations with ProviderModelName',
    'New unique index on WordExplanations already exists') as Step5_Status;

-- ============================================================================
-- Step 6: Dynamically find and drop ALL foreign keys on UserWords
-- ============================================================================
-- Find all FK constraints on UserWords table
SET @fk_names = (
    SELECT GROUP_CONCAT(DISTINCT CONSTRAINT_NAME SEPARATOR ',')
    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND REFERENCED_TABLE_NAME IS NOT NULL
);

-- Drop each foreign key constraint (handle multiple FKs)
-- First FK (usually WordExplanationId)
SET @fk1_name = (
    SELECT CONSTRAINT_NAME
    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND COLUMN_NAME = 'WordExplanationId'
      AND REFERENCED_TABLE_NAME = 'WordExplanations'
    LIMIT 1
);

SET @drop_fk1_sql = IF(
    @fk1_name IS NOT NULL,
    CONCAT('ALTER TABLE UserWords DROP FOREIGN KEY `', @fk1_name, '`'),
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @drop_fk1_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Check for any other FKs and drop them
SET @fk2_name = (
    SELECT CONSTRAINT_NAME
    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND REFERENCED_TABLE_NAME IS NOT NULL
      AND CONSTRAINT_NAME != IFNULL(@fk1_name, '')
    LIMIT 1
);

SET @drop_fk2_sql = IF(
    @fk2_name IS NOT NULL,
    CONCAT('ALTER TABLE UserWords DROP FOREIGN KEY `', @fk2_name, '`'),
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @drop_fk2_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT CONCAT('Dropped foreign keys: ', IFNULL(@fk_names, 'None found')) as Step6_Status;

-- ============================================================================
-- Step 7: Drop old unique constraint (now safe after FK removal)
-- ============================================================================
SET @old_index_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND INDEX_NAME = 'UQ_UserWords_UserId_WordId'
);

SET @drop_old_index_sql = IF(
    @old_index_exists > 0,
    'ALTER TABLE UserWords DROP INDEX UQ_UserWords_UserId_WordId',
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @drop_old_index_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT IF(@old_index_exists > 0,
    'Dropped old index: UQ_UserWords_UserId_WordId',
    'Old index does not exist') as Step7_Status;

-- ============================================================================
-- Step 8: Create new unique constraint if it doesn't exist
-- ============================================================================
SET @new_index_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND INDEX_NAME = 'UQ_UserWords_UserId_WordCollectionId'
);

-- Use default ASC for both columns (DESC provides no benefit for uniqueness)
SET @create_new_index_sql = IF(
    @new_index_exists = 0,
    'ALTER TABLE UserWords ADD UNIQUE INDEX UQ_UserWords_UserId_WordCollectionId (UserId, WordCollectionId)',
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @create_new_index_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT IF(@new_index_exists = 0,
    'Created new index: UQ_UserWords_UserId_WordCollectionId',
    'New index already exists') as Step8_Status;

-- ============================================================================
-- Step 9: Add index on WordCollectionId for better join performance
-- ============================================================================
SET @wordcollection_index_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND INDEX_NAME = 'IX_UserWords_WordCollectionId'
);

SET @create_wordcollection_index_sql = IF(
    @wordcollection_index_exists = 0,
    'ALTER TABLE UserWords ADD INDEX IX_UserWords_WordCollectionId (WordCollectionId)',
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @create_wordcollection_index_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT IF(@wordcollection_index_exists = 0,
    'Created index: IX_UserWords_WordCollectionId',
    'Index already exists') as Step9_Status;

-- ============================================================================
-- Step 10: Recreate foreign key constraint (if needed)
-- ============================================================================
-- Check if FK already exists (using actual name, not hardcoded)
SET @fk_exists = (
    SELECT CONSTRAINT_NAME
    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'UserWords'
      AND COLUMN_NAME = 'WordExplanationId'
      AND REFERENCED_TABLE_NAME = 'WordExplanations'
    LIMIT 1
);

-- Only recreate if it doesn't exist
-- Note: We use a generated name but it won't conflict if one exists
SET @recreate_fk_sql = IF(
    @fk_exists IS NULL,
    'ALTER TABLE UserWords ADD CONSTRAINT FK_UserWords_WordExplanations_WordExplanationId FOREIGN KEY (WordExplanationId) REFERENCES WordExplanations(Id)',
    'SELECT 1'  -- No-op
);

PREPARE stmt FROM @recreate_fk_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT IF(@fk_exists IS NULL,
    'Recreated foreign key: FK_UserWords_WordExplanations_WordExplanationId',
    CONCAT('Foreign key already exists: ', @fk_exists)) as Step10_Status;

-- ============================================================================
-- Verification Queries
-- ============================================================================

-- Show migration summary
SELECT
    'Migration Summary' as Section,
    'Multiple Explanations Feature' as Feature,
    NOW() as ExecutedAt;

-- Verify column exists and is NOT NULL
SELECT
    COLUMN_NAME,
    COLUMN_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT,
    COLUMN_KEY
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'UserWords'
  AND COLUMN_NAME = 'WordCollectionId';

-- Verify indexes
SELECT
    INDEX_NAME,
    COLUMN_NAME,
    SEQ_IN_INDEX,
    NON_UNIQUE,
    INDEX_TYPE
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'UserWords'
ORDER BY INDEX_NAME, SEQ_IN_INDEX;

-- Verify foreign keys
SELECT
    CONSTRAINT_NAME,
    TABLE_NAME,
    COLUMN_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'UserWords'
  AND REFERENCED_TABLE_NAME IS NOT NULL;

-- Data integrity checks
SELECT
    'Total UserWords records' as Metric,
    COUNT(*) as Count
FROM UserWords

UNION ALL

SELECT
    'Records with WordCollectionId set' as Metric,
    COUNT(*) as Count
FROM UserWords
WHERE WordCollectionId IS NOT NULL AND WordCollectionId > 0

UNION ALL

SELECT
    'Records with matching WordCollectionId' as Metric,
    COUNT(*) as Count
FROM UserWords uw
INNER JOIN WordExplanations we ON uw.WordExplanationId = we.Id
WHERE uw.WordCollectionId = we.WordCollectionId

UNION ALL

SELECT
    'Unique (UserId, WordCollectionId) pairs' as Metric,
    COUNT(DISTINCT CONCAT(UserId, '-', WordCollectionId)) as Count
FROM UserWords;

-- Show sample data
SELECT
    uw.Id,
    uw.UserId,
    uw.WordCollectionId,
    uw.WordExplanationId,
    we.WordText,
    we.WordCollectionId as WE_WordCollectionId,
    uw.CreatedAt,
    uw.UpdatedAt
FROM UserWords uw
INNER JOIN WordExplanations we ON uw.WordExplanationId = we.Id
ORDER BY uw.UpdatedAt DESC
LIMIT 10;

-- Check for any data inconsistencies
SELECT
    'Data Inconsistency Check' as CheckType,
    COUNT(*) as IssueCount
FROM UserWords uw
LEFT JOIN WordExplanations we ON uw.WordExplanationId = we.Id
WHERE uw.WordCollectionId != we.WordCollectionId
   OR we.Id IS NULL;

-- Show final table structure
DESCRIBE UserWords;

-- ============================================================================
-- Migration Complete
-- ============================================================================
SELECT
    '✓ Migration 07_multiple_explanations.sql completed successfully' as Status,
    NOW() as CompletedAt;
