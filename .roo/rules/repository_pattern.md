# Repository Pattern for Data Access

## Principle
**Always use the Repository pattern for data access in service layers, especially with `Api.Framework`.** This promotes separation of concerns and maintainability.

## Guidelines (`Api.Framework` & Consuming Projects like `NewWords.Api`)

1.  **Use Generics:** Base custom repositories (e.g., `IUserRepository`, `UserRepository`) on `Api.Framework`'s `IRepositoryBase<T>` and `RepositoryBase<T>`.
2.  **Avoid Direct `ISqlSugarClient` in Services for CRUD:**
    *   **Don't:** Inject `ISqlSugarClient` in services for standard CRUD or simple queries.
    *   **Do:** Inject specific repositories (e.g., `IUserRepository`) and use their methods.
3.  **Complex Queries:** For highly specific complex queries/projections not fitting repository methods, direct `ISqlSugarClient` use in services is permissible. First, try to encapsulate in a new repository method. (e.g., `VocabularyService.GetUserWordsAsync`'s join might stay in service).

## Rationale (Brief)
*   **SoC:** Services = business logic; Repositories = data persistence.
*   **Testability:** Mock repositories for service unit tests.
*   **Maintainability & Reusability:** Centralized, reusable data logic.
*   **Abstraction:** Decouples services from the ORM (SqlSugar).

Consistent use ensures a cleaner, more robust, and testable data access layer.