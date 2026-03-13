# Alex — History

## Core Context

- **Project:** LinqStudio — IDE-like interface for writing and executing EF Core LINQ queries. Replaces SSMS.
- **Owner:** snakex64
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn, EF Core, Aspire, XUnit, Playwright
- **Build:** `./build.ps1 Test` (Nuke) · `dotnet build` · `dotnet run --project src/LinqStudio.App.WebServer`
- **Key conventions:** Nullable enabled, implicit usings, file-scoped namespaces, expression-bodied members, TreatWarningsAsErrors=True on main projects
- **Test framework:** XUnit — use `Assert.*`, no FluentAssertions

## Learnings

### 2026-03-13: Post-Implementation Code Review & Follow-Up Actions

**Sprint Summary:**
- Completed comprehensive code review of MSSQL auto-discovery feature (21 files, 14 new)
- Identified 1 High, 5 Medium, 7 Low severity findings
- Documented test coverage gaps and recommendations
- Follow-up fixes implemented by Simon (validation) and EvilJosh (caching, cleanup)
- Status: ✅ 407 tests passing, decisions documented and merged

**High Severity Finding (ADDRESSED):**
- **Missing null check in UpdateConnection:** Could accept empty strings, bypassing validation downstream
- **Fix:** EditProjectDialog now validates before Save(), prevents empty string passage

**Medium Severity Findings (ADDRESSED or DOCUMENTED):**
1. Connection state management could cause reopen issues → Resolved by removing auto-discovery entirely
2. EditProjectDialog null propagation → Fixed with validation
3. DatabaseTreeView race conditions → Fixed with GetValueOrDefault() pattern

**Code Review Patterns for Future:**
1. **Validation at boundaries:** When user input flows UI→Service→Model, validate early (dialog) or late (model), but not nowhere
2. **Stateful field patterns:** When caching database-specific state, tie to connection lifetime or document assumptions
3. **Empty string vs null:** Treat empty string as invalid for required fields
4. **Event handler concurrency:** For async event handlers triggering long-running ops, consider cancellation tokens

**Quality Assessment:**
- Build quality: Excellent (0 warnings with TreatWarningsAsErrors=True)
- Test coverage: Comprehensive (401 tests passing, good documentation on skipped tests)
- Team performance: Outstanding (copilot.md adoption, excellent test documentation, architectural clarity)
- Production readiness: Confirmed after follow-up fixes



#### Context
Reviewed implementation of new DatabaseTreeView feature with MSSQL auto-discovery, Blazor component integration, and comprehensive test coverage across 21 modified files.

#### Patterns Observed

**Positive Patterns:**
1. **Comprehensive feature implementation** - New UI features come with component tests, E2E tests (even if skipped), helper methods, and documentation. This is exemplary.
2. **Caching with lifecycle management** - DatabaseTreeView uses Dictionary-based caching with proper cleanup on workspace changes. Smart pattern for reducing database round-trips.
3. **Smart auto-discovery** - MssqlGenerator automatically switches from master to first user database when no database specified. Handles Aspire deployment patterns well.
4. **Excellent test documentation** - Skipped tests include detailed implementation notes explaining what/how to test. This is valuable for future work.
5. **Copilot.md pattern** - Each component/feature area has copilot.md with feature descriptions, test IDs, and implementation notes. Great for AI-assisted development.

**Concerning Patterns:**
1. **Empty string vs null confusion** - EditProjectDialog uses `_connectionString ?? string.Empty` which can pass empty string to UpdateConnection. Project.cs doesn't validate this, leading to potential runtime exceptions when creating QueryGenerator.
2. **Stateful caching across connection lifecycle** - MssqlGenerator's `_resolvedDatabase` field persists across connection open/close cycles. If connection closes and reopens, the cached database name may be stale, but code tries to switch to it.
3. **Property setters with side effects using field keyword** - C# 13 field keyword used in Project.cs with side effects (clearing QueryGenerator). While syntactically correct, mixing auto-property and manual logic can be missed during maintenance.
4. **Race conditions in event handlers** - DatabaseTreeView.OnWorkspaceChanged uses InvokeAsync without cancellation. Rapid workspace changes could queue multiple LoadTablesAsync calls. Low impact but worth noting.

#### Quality Observations
- Build: ✅ 0 warnings with TreatWarningsAsErrors=True (excellent)
- Tests: ✅ All 401 tests passing (297 DB + 45 Core + 44 Blazor + 15 E2E)
- Architecture: Follows established patterns (layered, DI, workspace pattern)
- Conventions: Nullable enabled, file-scoped namespaces, expression-bodied members

#### Recommendations for Future Reviews
1. **Validation at boundaries** - When user input flows from UI → Service → Model, validate early (in dialog) or late (in model), but not nowhere.
2. **Stateful field patterns** - When caching database-specific state (like resolved database name), consider tying it to connection lifetime or document the assumption.
3. **Empty string vs null** - Treat empty string as invalid for required fields like connection strings. Don't coalesce to empty string.
4. **Event handler concurrency** - For async event handlers that trigger long-running operations, consider cancellation tokens to prevent queuing.

#### Test Coverage Gaps Identified
- Project.UpdateConnection validation (empty/null strings)
- MssqlGenerator connection reopen scenarios
- DatabaseTreeView concurrent event handling
- EditProjectDialog validation error handling

#### Actions Taken
- Created comprehensive review document: `.squad/decisions/inbox/alex-review-current-changes.md`
- Identified 1 High, 5 Medium, 7 Low severity findings
- Documented test coverage gaps with suggested test names
- Provided specific code recommendations with examples
