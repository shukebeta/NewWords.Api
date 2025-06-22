-- Stories Feature Database Migration
-- Run this script to add the Stories and StoryWords tables

-- Create Stories table
CREATE TABLE IF NOT EXISTS Stories (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    UserId INT NOT NULL,
    Content TEXT NOT NULL,
    StoryWords VARCHAR(1024) NOT NULL,
    LearningLanguage VARCHAR(20) NOT NULL,
    FirstReadAt BIGINT NULL,
    FavoriteCount INT NOT NULL DEFAULT 0,
    ProviderModelName VARCHAR(100) NULL,
    CreatedAt BIGINT NOT NULL,

    -- Foreign key constraints
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,

    -- Indexes for better query performance
    INDEX idx_stories_userid (UserId),
    INDEX idx_stories_language (LearningLanguage),
    INDEX idx_stories_created (CreatedAt),
    INDEX idx_stories_favorites (FavoriteCount),
    INDEX idx_stories_model (ProviderModelName)
);

-- Create StoryWords junction table for future similarity features
CREATE TABLE IF NOT EXISTS StoryWords (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    StoryId BIGINT NOT NULL,
    WordCollectionId BIGINT NOT NULL,

    -- Foreign key constraints
    FOREIGN KEY (StoryId) REFERENCES Stories(Id) ON DELETE CASCADE,
    FOREIGN KEY (WordCollectionId) REFERENCES WordCollection(Id) ON DELETE CASCADE,

    -- Unique constraint to prevent duplicate word-story relationships
    UNIQUE KEY unique_story_word (StoryId, WordCollectionId),

    -- Indexes for better query performance
    INDEX idx_storywords_story (StoryId),
    INDEX idx_storywords_word (WordCollectionId)
);

-- Create UserFavoriteStories junction table to track user favorites
CREATE TABLE IF NOT EXISTS UserFavoriteStories (
    Id BIGINT PRIMARY KEY AUTO_INCREMENT,
    UserId INT NOT NULL,
    StoryId BIGINT NOT NULL,
    CreatedAt BIGINT NOT NULL,

    -- Foreign key constraints
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (StoryId) REFERENCES Stories(Id) ON DELETE CASCADE,

    -- Unique constraint to prevent duplicate favorites
    UNIQUE KEY unique_user_favorite (UserId, StoryId),

    -- Indexes for better query performance
    INDEX idx_userfavorites_user (UserId),
    INDEX idx_userfavorites_story (StoryId),
    INDEX idx_userfavorites_created (CreatedAt)
);

-- Verify tables were created
SELECT 'Stories table created successfully' as result;
SELECT 'StoryWords table created successfully' as result;
SELECT 'UserFavoriteStories table created successfully' as result;
