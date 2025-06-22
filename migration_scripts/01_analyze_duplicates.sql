USE NewWords;
-- Analysis Script: WordCollection Duplicates
-- This script analyzes duplicate WordText records in WordCollection table

-- 1. Count total records vs unique WordText values
SELECT
    'Total Records' as Metric,
    COUNT(*) as Count
FROM WordCollection
WHERE DeletedAt IS NULL

UNION ALL

SELECT
    'Unique WordText Values' as Metric,
    COUNT(DISTINCT WordText) as Count
FROM WordCollection
WHERE DeletedAt IS NULL;

-- 2. Find WordText values that have multiple Language records
SELECT
    WordText,
    COUNT(*) as DuplicateCount,
    GROUP_CONCAT(DISTINCT Language ORDER BY Language) as Languages,
    SUM(QueryCount) as TotalQueryCount,
    MIN(CreatedAt) as EarliestCreated,
    MAX(UpdatedAt) as LatestUpdated
FROM WordCollection
WHERE DeletedAt IS NULL
GROUP BY WordText
HAVING COUNT(*) > 1
ORDER BY DuplicateCount DESC, TotalQueryCount DESC;

-- 3. Summary of duplication impact
SELECT
    'Words with Duplicates' as Metric,
    COUNT(*) as Count
FROM (
    SELECT WordText
    FROM WordCollection
    WHERE DeletedAt IS NULL
    GROUP BY WordText
    HAVING COUNT(*) > 1
) duplicates

UNION ALL

SELECT
    'Total Duplicate Records to Remove' as Metric,
    COUNT(*) - COUNT(DISTINCT WordText) as Count
FROM WordCollection
WHERE DeletedAt IS NULL;

-- 4. Check WordExplanations dependencies
SELECT
    'WordExplanations Affected' as Metric,
    COUNT(DISTINCT we.Id) as Count
FROM WordExplanations we
INNER JOIN WordCollection wc ON we.WordCollectionId = wc.Id
WHERE wc.DeletedAt IS NULL
AND wc.WordText IN (
    SELECT WordText
    FROM WordCollection
    WHERE DeletedAt IS NULL
    GROUP BY WordText
    HAVING COUNT(*) > 1
);

-- 5. Most duplicated words (top 10)
SELECT
    WordText,
    COUNT(*) as DuplicateCount,
    GROUP_CONCAT(DISTINCT Language ORDER BY Language) as Languages,
    SUM(QueryCount) as TotalQueryCount
FROM WordCollection
WHERE DeletedAt IS NULL
GROUP BY WordText
HAVING COUNT(*) > 1
ORDER BY DuplicateCount DESC, TotalQueryCount DESC
LIMIT 10;
