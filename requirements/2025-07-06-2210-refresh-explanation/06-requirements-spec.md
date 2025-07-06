# Requirements Specification: Refresh Word Explanation

## Problem Statement
Users need the ability to refresh existing word explanations to take advantage of updated AI models or improved explanation generation. Currently, once a word explanation is generated, it remains static even when better AI models become available as the "first agent" in the system configuration.

## Solution Overview
Implement a new API endpoint `/vocabulary/refreshExplanation/{wordExplanationId}` that intelligently refreshes word explanations by:
1. Checking if the current first agent differs from the explanation's original provider model
2. If different, regenerating the explanation using the current first agent
3. If same, returning the explanation unchanged (no-op)
4. Handling failures gracefully by preserving original explanations

## Functional Requirements

### FR-1: Provider Model Comparison
- **Requirement**: System must compare current first agent with WordExplanation.ProviderModelName
- **Logic**: If current first agent == WordExplanation.ProviderModelName, return unchanged
- **Format**: Provider model names follow pattern `"{Provider}:{ModelName}"` (e.g., "openrouter:deepseek/deepseek-chat:free")

### FR-2: Explanation Regeneration
- **Requirement**: When provider models differ, regenerate explanation using current first agent
- **Process**: Call `LanguageService.GetMarkdownExplanationWithFallbackAsync()` with existing word parameters
- **Update Fields**: 
  - `MarkdownExplanation` - New AI-generated content
  - `ProviderModelName` - Current first agent identifier
  - `CreatedAt` - Current timestamp

### FR-3: Shared Explanation System
- **Requirement**: WordExplanations are shared across all users
- **Impact**: Refreshing an explanation affects all users who have that word
- **Security**: No user ownership validation required

### FR-4: Error Handling
- **Requirement**: If new agent fails to generate explanation, return original unchanged
- **Behavior**: Preserve data integrity by not corrupting existing explanations
- **Response**: Return original WordExplanation with success status

### FR-5: Atomic Operation
- **Requirement**: Single database update operation without explicit transaction management
- **Justification**: Updating single record is inherently atomic
- **Pattern**: Use standard repository `UpdateAsync()` method

## Technical Requirements

### API Endpoint
- **Method**: `PUT`
- **Route**: `/vocabulary/refreshExplanation/{wordExplanationId}`
- **Parameters**: `wordExplanationId` (long) - ID of WordExplanation to refresh
- **Authentication**: Required (`[Authorize]` attribute)
- **Response**: `ApiResult<WordExplanation>`

### Service Method
- **Interface**: `IVocabularyService`
- **Method**: `RefreshUserWordExplanationAsync(long wordExplanationId)`
- **Return**: `Task<WordExplanation>`

### Database Schema
- **Table**: `WordExplanations`
- **Key Fields**:
  - `Id` (long) - Primary key for identification
  - `MarkdownExplanation` (string) - Content to update
  - `ProviderModelName` (string) - Model tracking to update
  - `CreatedAt` (long) - Timestamp to update
- **Unchanged Fields**: `WordText`, `LearningLanguage`, `ExplanationLanguage`, `WordCollectionId`

## Implementation Details

### File Modifications Required

#### 1. VocabularyController.cs
```csharp
[HttpPut("RefreshExplanation/{wordExplanationId}")]
public async Task<IActionResult> RefreshExplanation(long wordExplanationId)
{
    try
    {
        var result = await vocabularyService.RefreshUserWordExplanationAsync(wordExplanationId);
        return Ok(new SuccessfulResult<WordExplanation>(result));
    }
    catch (Exception ex)
    {
        return BadRequest(ApiResult.Fail(ex.Message));
    }
}
```

#### 2. VocabularyService.cs
```csharp
public async Task<WordExplanation> RefreshUserWordExplanationAsync(long wordExplanationId)
{
    // 1. Get current explanation
    var explanation = await wordExplanationRepository.GetFirstOrDefaultAsync(we => we.Id == wordExplanationId);
    if (explanation == null) throw new ArgumentException("Word explanation not found");
    
    // 2. Get current first agent
    var firstAgent = configurationService.Agents.FirstOrDefault();
    if (firstAgent == null) throw new InvalidOperationException("No agents configured");
    
    var currentFirstAgentName = $"{firstAgent.Provider}:{firstAgent.ModelName}";
    
    // 3. Compare with existing provider model
    if (currentFirstAgentName == explanation.ProviderModelName)
    {
        return explanation; // No change needed
    }
    
    // 4. Get language names for regeneration
    var learningLanguageName = configurationService.GetLanguageName(explanation.LearningLanguage);
    var explanationLanguageName = configurationService.GetLanguageName(explanation.ExplanationLanguage);
    
    // 5. Generate new explanation
    var newExplanationResult = await languageService.GetMarkdownExplanationWithFallbackAsync(
        explanation.WordText, explanationLanguageName, learningLanguageName);
    
    // 6. Handle generation failure
    if (!newExplanationResult.IsSuccess)
    {
        return explanation; // Return original on failure
    }
    
    // 7. Update explanation
    explanation.MarkdownExplanation = newExplanationResult.Markdown;
    explanation.ProviderModelName = newExplanationResult.ModelName;
    explanation.CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds();
    
    // 8. Save changes
    await wordExplanationRepository.UpdateAsync(explanation);
    
    return explanation;
}
```

### Integration Points
- **ConfigurationService**: Access `Agents` property for current first agent
- **LanguageService**: Use `GetMarkdownExplanationWithFallbackAsync()` for regeneration
- **WordExplanationRepository**: Use `UpdateAsync()` for database persistence

## Acceptance Criteria

### AC-1: Same Provider Model
- **Given**: WordExplanation.ProviderModelName matches current first agent
- **When**: RefreshExplanation is called
- **Then**: Return original WordExplanation unchanged

### AC-2: Different Provider Model
- **Given**: WordExplanation.ProviderModelName differs from current first agent
- **When**: RefreshExplanation is called
- **Then**: Generate new explanation and update MarkdownExplanation, ProviderModelName, and CreatedAt

### AC-3: Generation Failure
- **Given**: New agent fails to generate explanation
- **When**: RefreshExplanation is called
- **Then**: Return original WordExplanation unchanged

### AC-4: Shared Impact
- **Given**: WordExplanation is refreshed
- **When**: Any user accesses that word
- **Then**: All users see the updated explanation

### AC-5: Authentication
- **Given**: User is not authenticated
- **When**: RefreshExplanation is called
- **Then**: Return 401 Unauthorized

### AC-6: Non-existent Word
- **Given**: wordExplanationId does not exist
- **When**: RefreshExplanation is called
- **Then**: Return error with appropriate message

## Assumptions
- Agent configuration remains stable during operation
- Language name resolution works for all existing explanations
- Database connectivity is available for update operations
- LanguageService fallback mechanism handles provider failures appropriately

## Success Metrics
- Explanations are successfully refreshed when provider models differ
- No data corruption occurs during refresh operations
- Original explanations are preserved when regeneration fails
- All users receive updated explanations immediately after refresh

🤖 Generated with [Claude Code](https://claude.ai/code)