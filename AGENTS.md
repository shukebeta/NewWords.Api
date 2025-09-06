# AGENTS.md – Coding Agent Guidelines

## Build, Lint, and Test
- Build: `dotnet build NewWords.Api.sln`
- Run: `dotnet run --project src/NewWords.Api`
- Run (Local): `dotnet run --project src/NewWords.Api --launch-profile Local`
- Test all: `dotnet test src/NewWords.Api.Tests`
- Test single: `dotnet test src/NewWords.Api.Tests --filter FullyQualifiedName~<TestClass>.<TestMethod>`
- Lint: No explicit linter; follow C#/.NET and repo rules

## Code Style
- **Imports:** Use explicit `using` statements; group system/third-party usings. `ImplicitUsings` is enabled.
- **Formatting:** 4 spaces/indent, braces on new lines, <120 chars/line.
- **Types:** Prefer explicit types; use `var` only when type is obvious. Nullable reference types enabled.
- **Naming:** PascalCase for types/methods/properties; camelCase for locals/params; UPPER_SNAKE_CASE for constants.
- **Error Handling:** Throw exceptions for errors; catch/log at service/controller boundaries. Use result objects for API responses.
- **Repository Pattern:** Always use repository pattern for data access in services (see `.roo/rules/repository_pattern.md`).
- **Comments:** Avoid redundant comments; comment only for intent or non-obvious logic (see `.roo/rules/comments.md`).
- **Testing:** Use xUnit, FluentAssertions, NSubstitute. Mock dependencies, cover edge/error cases.
- **Environment:** Use `appsettings.json` and env-specific files for config. Never hardcode secrets.
- **Principles:** Code should be self-documenting, DRY, and KISS. Follow project conventions strictly.
