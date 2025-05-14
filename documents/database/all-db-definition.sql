-- MySQL dump 10.13  Distrib 8.0.25, for Linux (x86_64)
--
-- Host: mysql-2688587-weizhong2004-67dd.h.aivencloud.com    Database: NewWords
-- ------------------------------------------------------
-- Server version	8.0.35

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Current Database: `NewWords`
--

CREATE DATABASE /*!32312 IF NOT EXISTS*/ `NewWords` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;

USE `NewWords`;

--
-- Table structure for table `QueryHistory`
--

DROP TABLE IF EXISTS `QueryHistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `QueryHistory` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `WordCollectionId` bigint NOT NULL,
  `UserId` bigint NOT NULL,
  `CreatedAt` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `UserSettings`
--

DROP TABLE IF EXISTS `UserSettings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `UserSettings` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `UserId` bigint NOT NULL,
  `SettingName` varchar(255) NOT NULL,
  `SettingValue` text NOT NULL,
  `CreatedAt` bigint NOT NULL,
  `DeletedAt` bigint DEFAULT NULL,
  `UpdatedAt` bigint DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `UserWords`
--

DROP TABLE IF EXISTS `UserWords`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `UserWords` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `WordExplanationId` bigint NOT NULL,
  `Status` int NOT NULL DEFAULT '0',
  `CreatedAt` bigint NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UQ_UserWords_UserId_WordId` (`UserId`,`WordExplanationId`),
  KEY `UserWords_ibfk_2` (`WordExplanationId`),
  CONSTRAINT `UserWords_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Users`
--

DROP TABLE IF EXISTS `Users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Users` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Email` varchar(200) NOT NULL,
  `Gravatar` varchar(255) DEFAULT NULL,
  `Salt` varchar(64) NOT NULL,
  `PasswordHash` varchar(200) NOT NULL,
  `NativeLanguage` varchar(20) NOT NULL,
  `CurrentLearningLanguage` varchar(20) NOT NULL,
  `CreatedAt` bigint NOT NULL,
  `UpdatedAt` bigint DEFAULT NULL,
  `DeletedAt` bigint DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Email` (`Email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `WordCollection`
--

DROP TABLE IF EXISTS `WordCollection`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `WordCollection` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `WordText` varchar(255) NOT NULL,
  `Language` varchar(20) NOT NULL,
  `QueryCount` bigint NOT NULL,
  `CreatedAt` bigint NOT NULL,
  `UpdatedAt` bigint DEFAULT NULL,
  `DeletedAt` bigint DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `WordText` (`WordText`,`Language`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `WordExplanations`
--

DROP TABLE IF EXISTS `WordExplanations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `WordExplanations` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `WordCollectionId` bigint NOT NULL,
  `WordText` varchar(255) NOT NULL,
  `WordLanguage` varchar(20) NOT NULL,
  `ExplanationLanguage` varchar(20) NOT NULL,
  `MarkdownExplanation` text,
  `Pronunciation` varchar(512) DEFAULT NULL,
  `Definitions` text,
  `Examples` text,
  `CreatedAt` bigint NOT NULL,
  `ProviderModelName` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UQ_Words_Text_Lang_NativeLang` (`WordText`,`WordLanguage`,`ExplanationLanguage`),
  KEY `WordCollection_ibfk_1` (`WordCollectionId`),
  CONSTRAINT `WordCollection_ibfk_1` FOREIGN KEY (`WordCollectionId`) REFERENCES `WordCollection` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

