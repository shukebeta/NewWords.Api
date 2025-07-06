# Context Findings

## Specific Files That Need Modification

### 1. Controller Layer
**File**: `/home/davidwei/RiderProjects/NewWords.Api/src/NewWords.Api/Controllers/VocabularyController.cs`
- **Action**: Add new `RefreshExplanation` endpoint
- **Pattern**: Follow existing endpoints (GET List, POST Add, DELETE)
- **Route**: `PUT /Vocabulary/RefreshExplanation/{wordExplanationId}`

### 2. Service Interface
**File**: `/home/davidwei/RiderProjects/NewWords.Api/src/NewWords.Api/Services/VocabularyService.cs`
- **Action**: Add `RefreshUserWordExplanationAsync` method to interface and implementation
- **Pattern**: Follow existing `AddUserWordAsync` method structure

### 3. Existing Implementation Patterns to Follow

#### Transaction Management
```csharp
try
{
    await db.AsTenant().BeginTranAsync();
    // ... operations ...
    await db.AsTenant().CommitTranAsync();
}
catch (Exception ex)
{
    await db.AsTenant().RollbackTranAsync();
    logger.LogError(ex, "Error message");
    throw;
}
```

#### Service Integration
```csharp
var explanationResult = await languageService.GetMarkdownExplanationWithFallbackAsync(
    wordText, explanationLanguageName, learningLanguageName);
```

#### Database Update Pattern
```csharp
await wordExplanationRepository.UpdateAsync(explanation);
```

## Technical Constraints and Considerations

### 1. Agent Configuration System
- **First Agent**: `configurationService.Agents.FirstOrDefault()`
- **Provider Model Format**: `"{Provider}:{ModelName}"` (e.g., "openrouter:deepseek/deepseek-chat:free")
- **Current Check**: Compare with `WordExplanation.ProviderModelName`

### 2. Database Constraints
- **Unique Constraint**: `UQ_WordExplanations_CollectionId_ExplLang` on (`WordCollectionId`, `ExplanationLanguage`)
- **Primary Key**: `WordExplanation.Id` (long)
- **User Ownership**: Validate through `UserWords` relationship

### 3. Error Handling Requirements
- **User Authorization**: Verify user owns the word explanation
- **Agent Failure**: Keep original explanation if new generation fails
- **Transaction Rollback**: Follow existing pattern for data integrity

## Integration Points Identified

### 1. LanguageService
- **Method**: `GetMarkdownExplanationWithFallbackAsync`
- **Returns**: `ExplanationResult` with `Markdown` and `ModelName`
- **Fallback**: Automatic agent fallback if first agent fails

### 2. ConfigurationService
- **Method**: `GetLanguageName()` for language code to name conversion
- **Property**: `Agents` for current agent configuration

### 3. Repository Layer
- **Pattern**: Generic repository with `UpdateAsync` method
- **ORM**: SqlSugar with async operations

## Similar Features Analyzed

### 1. AddUserWordAsync Method
- **Pattern**: Three-phase approach (Collection, Explanation, UserWord)
- **Replication**: Only need Explanation phase for refresh
- **Transaction**: Same transaction management pattern

### 2. Existing Vocabulary Endpoints
- **Authentication**: All use `[Authorize]` attribute
- **Response Format**: `ApiResult<WordExplanation>`
- **User Context**: Access through `ICurrentUser currentUser`

## Fields to Update During Refresh

### Required Updates
- `MarkdownExplanation` - New AI-generated content
- `ProviderModelName` - Track which model generated the refresh

### Fields to NOT Update
- `CreatedAt` - Based on Q3 answer, this should be updated to reflect refresh time
- `WordText`, `LearningLanguage`, `ExplanationLanguage` - Core word data unchanged
- `Id`, `WordCollectionId` - Entity relationships unchanged

## Implementation Sequence

1. **VocabularyService.RefreshUserWordExplanationAsync()**
   - Validate user ownership
   - Fetch current explanation
   - Generate new explanation
   - Update database record

2. **VocabularyController.RefreshExplanation()**
   - Accept `wordExplanationId` parameter
   - Call service method
   - Return `ApiResult<WordExplanation>`

3. **Error Handling**
   - User not authorized
   - Word explanation not found
   - Agent generation failure
   - Database update failure