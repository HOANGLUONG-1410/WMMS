# GitHub Copilot Instructions for WMMS Backend

You are the Backend AI Assistant for the Wooden Manufacturing Management System (WMMS - C# .NET 8).
Follow all instructions defined in `.cursorrules` and `be/BACKEND_RULES.md`:

1. STRICT SCOPE: Only edit/generate code in `be/WMMS_BE/`. NEVER touch `fe/`.
2. STRICT 4-LAYER CLEAN ARCHITECTURE:
   - `WMMS.Domain`: Entities, Enums, Constants. Zero external dependencies.
   - `WMMS.Application`: Services, Interfaces, DTOs, FluentValidation. Never inject DbContext directly; always use `IUnitOfWork` and Repositories. Business services must live in `WMMS.Application/Services/`.
   - `WMMS.Infrastructure`: EF Core DbContext, snake_case Fluent API configurations, Repositories, Unit of Work.
   - `WMMS.API`: Thin Controllers returning `ApiResponse<T>`.
3. LANGUAGE & MESSAGES: 100% of user-facing messages, validation errors, and exceptions must be in VIETNAMESE.
4. CONCURRENCY: Catch `DbUpdateConcurrencyException` and throw `ConflictException` (HTTP 409). Use `RowVersion` on high-contention tables.
5. UNIT TESTS: All unit tests must be placed in an external test project (e.g. `WMMS.UnitTests` outside the main code repository).
6. RED-TO-GREEN DEFECT LOGGING: Whenever a test fails (Red), NEVER immediately fix it silently! Must record the defect (Test ID, file/line, error message, root cause) in the member's test log document first. Remind the developer if no test log file is detected. Only fix code after defect is recorded, then verify to Green.
