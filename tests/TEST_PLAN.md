Test Plan for NewWords.Api

Goals:
- Verify transaction helper behavior and correct rollback/commit semantics.
- Test VocabularyService behavior for AI fallback and canonicalization.
- Ensure idempotent insert behavior for WordCollection under concurrent insert scenarios.
- Add unit tests for ExtractCanonicalWordFromMarkdown and normalization.

Suggested tests:

1) Unit: ExtractCanonicalWordFromMarkdown
- Cases: first-line bold `**apple**`, leading header `# apple`, inline bold with text, empty markdown.

2) Unit: NormalizeWord
- Cases: trimming, casing, null/empty input.

3) Unit: InvokeAiServiceAsync (LanguageService stubbed)
- AI returns valid markdown: ensure returned ExplanationResult.IsSuccess true and Markdown preserved.
- AI fails or empty: ensure IsSuccess false and fallback behavior exercised in VocabularyService.

4) Integration/Unit: EnsureCanonicalWordAsync
- Given existing canonical and typo entries, test correct soft-delete/rename logic.
- Case-insensitive checks after normalization.

5) Concurrency test: _AddWordCollection idempotency
- Simulate concurrent inserts (mock repository to throw duplicate-key for one thread) and assert only one record ID is returned and no exception escapes.

6) Transaction helper tests
- Use a fake `ISqlSugarClient` to assert that Begin/Commit are called on success and Rollback called on exception; verify logger receives rollback error if rollback itself fails.

Implementation notes:
- Use xUnit, FluentAssertions and NSubstitute for mocking.
- Place tests under `tests/NewWords.Api.Tests` and reference project under test.
- For concurrency test, implement repository-level mock that simulates duplicate key exception on first InsertReturnIdentityAsync call.

Run locally:

```bash
dotnet test src/NewWords.Api.Tests
```

Optional:
- Add CI job that runs `dotnet build` and `dotnet test` on PRs.
