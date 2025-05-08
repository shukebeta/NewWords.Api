-- Database structure for NewWords.Api
-- Generated based on entities in src/NewWords.Api/Entities
-- MySQL compatible syntax

use NewWords;
-- Table: LlmConfigurations
CREATE TABLE IF NOT EXISTS LlmConfigurations (
    LlmConfigId INT PRIMARY KEY AUTO_INCREMENT,
    ModelName VARCHAR(100) NOT NULL UNIQUE,
    DisplayName VARCHAR(100),
    IsEnabled TINYINT(1) NOT NULL DEFAULT 1,
    ApiKey VARCHAR(1024),
    CreatedAt BIGINT NOT NULL
);

-- Table: Users
CREATE TABLE IF NOT EXISTS Users (
    UserId INT PRIMARY KEY AUTO_INCREMENT,
    Email VARCHAR(200) NOT NULL UNIQUE,
    PasswordHash VARCHAR(200) NOT NULL,
    NativeLanguage VARCHAR(20) NOT NULL,
    CurrentLearningLanguage VARCHAR(20) NOT NULL,
    CreatedAt BIGINT NOT NULL
);

-- Table: Words
CREATE TABLE IF NOT EXISTS Words (
    WordId INT PRIMARY KEY AUTO_INCREMENT,
    WordText VARCHAR(255) NOT NULL,
    WordLanguage VARCHAR(20) NOT NULL,
    UserNativeLanguage VARCHAR(20) NOT NULL,
    Pronunciation VARCHAR(512),
    Definitions TEXT,
    Examples TEXT,
    CreatedAt BIGINT NOT NULL,
    GeneratingLlm VARCHAR(100),
    CONSTRAINT UQ_Words_Text_Lang_NativeLang UNIQUE (WordText, WordLanguage, UserNativeLanguage)
);

-- Table: UserWords
CREATE TABLE IF NOT EXISTS UserWords (
    UserWordId INT PRIMARY KEY AUTO_INCREMENT,
    UserId INT NOT NULL,
    WordId INT NOT NULL,
    Status INT NOT NULL DEFAULT 0, -- Assuming WordStatus.New maps to 0
    CreatedAt BIGINT NOT NULL,
    CONSTRAINT UQ_UserWords_UserId_WordId UNIQUE (UserId, WordId),
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (WordId) REFERENCES Words(WordId)
);

-- Indexes (already defined in constraints for uniqueness)
-- Additional indexes can be added based on query patterns if needed
