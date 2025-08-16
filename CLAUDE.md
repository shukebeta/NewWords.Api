# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NewWords.Api is a multilingual vocabulary learning application that helps users build and manage their foreign language vocabulary. The application supports multiple languages and uses AI-powered explanations to help users understand new words in context.

## Architecture

This is a .NET 8 Web API solution with a clean architecture pattern:

### Project Structure
- **NewWords.Api** - Main Web API project containing controllers, entities, services, and DTOs
- **Api.Framework** - Shared framework library providing base classes, helpers, and common functionality
- **LLM** - Language Learning Model service for AI-powered word explanations and language detection

### Key Components
- **Entity Framework**: Uses SqlSugar ORM for database operations with MySQL
- **Authentication**: JWT Bearer token authentication
- **LLM Integration**: Multi-provider AI service supporting OpenRouter and other providers with fallback mechanisms
- **Language Support**: 25+ supported languages for vocabulary learning
- **Repository Pattern**: Generic repository base classes for data access
- **Logging**: NLog with Seq integration for centralized structured logging

### Core Services
- **VocabularyService**: Manages user words, explanations, and vocabulary operations
- **LanguageService**: Handles AI-powered word explanations and language detection using configurable agents
- **AuthService**: JWT authentication and user management
- **ConfigurationService**: Manages LLM agent configurations and provider settings

## Development Commands

### Build and Run
```bash
# Build the entire solution
dotnet build NewWords.Api.sln

# Run the API in development mode
dotnet run --project src/NewWords.Api

# Run with specific profile
dotnet run --project src/NewWords.Api --launch-profile https
```

### Database
- Uses SqlSugar ORM with MySQL
- Database migrations are handled through Alembic (Python-based migration tool)
- Connection configuration in `appsettings.json` under `DatabaseConnectionOptions`

### Development URLs
- HTTP: http://localhost:5116
- HTTPS: https://localhost:7162
- Swagger UI available at `/swagger` endpoint

### Launch Profiles
- **http**: Standard development profile (localhost only)
- **https**: HTTPS development profile
- **Local**: Local development profile (binds to all interfaces: `http://*:5116`)

## Configuration

### Key Configuration Sections
- **DatabaseConnectionOptions**: MySQL connection settings
- **Jwt**: JWT authentication configuration  
- **Agents**: LLM provider configurations (OpenRouter, etc.)
- **SupportedLanguages**: Array of supported language codes and names
- **AllowedCorsOrigins**: CORS configuration for frontend integration

### Environment-Specific Settings
- Development settings in `appsettings.Development.json`
- Local development settings in `appsettings.Local.json` (git-ignored)
- Production secrets should be replaced in `appsettings.json` (placeholders: `PRODUCTION_MYSQL_PASSWORD`, `PRODUCTION_SYMMETRIC_SECURITY_KEY`, `XAI_API_KEY`)

## LLM Integration

The application uses a flexible agent-based system for AI services:
- Multiple providers supported with fallback mechanisms
- Configurable model selection per provider
- Language-specific prompts for word explanations
- Supports both free and paid model tiers

## Database Schema

Key entities:
- **Users**: User accounts and profiles
- **UserWords**: User's vocabulary progress tracking
- **WordExplanations**: AI-generated word explanations
- **WordCollections**: Predefined vocabulary sets (CET-4/6, GRE, TOEFL, etc.)
- **QueryHistory**: Track user interaction history
- **UserSettings**: Per-user configuration and preferences

## API Structure

Controllers follow RESTful patterns:
- **AuthController**: Login, registration, token management
- **VocabularyController**: Word management and explanations
- **UserController**: User profile management
- **SettingsController**: User preferences
- **LLMController**: AI service endpoints

All controllers inherit from `BaseController` which provides common functionality and user context access.

## Logging Configuration

The application uses NLog with Seq integration for centralized logging:

### NLog Setup
- **File Logging**: Local log files in `logs/` directory
- **Console Logging**: Development console output
- **Seq Logging**: Centralized structured logging to Seq server

### Structured Logging Properties
- **Environment**: Automatically detects from `ASPNETCORE_ENVIRONMENT`
- **Host**: Machine hostname for environment identification
- **Application**: Set to "NewWords.Api"
- **Version**: Application version from configuration

### Log Filtering
ASP.NET Core framework verbose logs are filtered to reduce noise:
- Execution logs (`Microsoft.AspNetCore.Mvc.Infrastructure.*`)
- Routing logs (`Microsoft.AspNetCore.Routing.*`)
- Authorization logs (`Microsoft.AspNetCore.Authorization.*`)
- Hosting logs (`Microsoft.AspNetCore.Hosting.*`)

Only Warning level and above are logged for these categories, while application logs remain at Information level.

### Running with Logging
```bash
# Run with Local profile (recommended for development)
dotnet run --launch-profile Local

# Run with Development profile
dotnet run --launch-profile http
```

## Build & Test Commands
- Build solution: `dotnet build`
- Run application: `dotnet run --project src/NewWords.Api`
- Run with Local environment: `dotnet run --project src/NewWords.Api --launch-profile Local`