-- Create UserStoryReads table for tracking per-user story read status
-- Re-runnable: Uses CREATE TABLE IF NOT EXISTS
CREATE TABLE IF NOT EXISTS UserStoryReads (
    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    StoryId BIGINT NOT NULL,
    FirstReadAt BIGINT NOT NULL,
    
    -- Indexes for performance
    INDEX idx_user_story (UserId, StoryId),
    INDEX idx_story_id (StoryId),
    INDEX idx_user_id (UserId),
    
    -- Unique constraint to prevent duplicate entries
    UNIQUE KEY unique_user_story (UserId, StoryId),
    
    -- Foreign key constraints (adjust table names if different)
    FOREIGN KEY (StoryId) REFERENCES Stories(Id) ON DELETE CASCADE
    -- Note: Add User table FK if needed: FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Migrate existing FirstReadAt data from Stories to UserStoryReads
-- Re-runnable: Uses INSERT IGNORE to skip duplicates
-- This preserves existing read status for story owners
INSERT IGNORE INTO UserStoryReads (UserId, StoryId, FirstReadAt)
SELECT UserId, Id, FirstReadAt 
FROM Stories 
WHERE FirstReadAt IS NOT NULL 
  AND NOT EXISTS (
    SELECT 1 FROM UserStoryReads usr 
    WHERE usr.UserId = Stories.UserId 
      AND usr.StoryId = Stories.Id
  );

-- Verify migration
SELECT 
    'Stories with FirstReadAt' as DataType,
    COUNT(*) as Count
FROM Stories 
WHERE FirstReadAt IS NOT NULL

UNION ALL

SELECT 
    'UserStoryReads created' as DataType,
    COUNT(*) as Count
FROM UserStoryReads;