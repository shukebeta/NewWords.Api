# Discovery Questions

Based on analysis of the VocabularyController and agent configuration system, here are the key discovery questions to understand the problem space:

## Q1: Should the refresh operation be available through the main vocabulary UI where users see their word lists?
**Default if unknown:** Yes (integrates with existing user workflow in vocabulary management)

## Q2: Should the refresh operation require user confirmation before regenerating explanations?
**Default if unknown:** Yes (prevents accidental regeneration of explanations users may prefer)

## Q3: Should the refresh operation update the CreatedAt timestamp to reflect when the explanation was refreshed?
**Default if unknown:** Yes (maintains audit trail of when explanations were last updated)

## Q4: Should the refresh operation be available for all words or only words with outdated ProviderModelNames?
**Default if unknown:** All words (gives users flexibility to refresh any explanation)

## Q5: Should the refresh operation handle cases where the new agent fails to generate an explanation?
**Default if unknown:** Yes (keep original explanation if new generation fails, ensuring data integrity)

## Analysis Context

**Existing Patterns Identified:**
- VocabularyController uses standard REST patterns with `[Authorize]` attribute
- All endpoints return `ApiResult<T>` response format
- Service layer (`IVocabularyService`) handles business logic
- Agent configuration uses ordered list with fallback mechanism
- ProviderModelName format: `"{Provider}:{ModelName}"` (e.g., "openrouter:deepseek/deepseek-chat:free")
- First agent determined by: `configurationService.Agents.FirstOrDefault()`

**Related Features:**
- Existing `/Vocabulary/Add` endpoint already generates explanations using current agent
- Agent configuration system with fallback mechanism
- WordExplanation entity with ProviderModelName tracking