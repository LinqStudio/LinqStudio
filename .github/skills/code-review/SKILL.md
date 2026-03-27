---
name: code-review
description: How to conduct a structured code review in LinqStudio — what to check, severity levels, output format, and what to skip. Use this when reviewing uncommitted changes or pull request diffs in this repository.
---

## Context

Apply this skill whenever any agent is asked to review code changes in the LinqStudio repository. This covers post-change reviews of uncommitted diffs, PR reviews, and targeted file reviews. The skill encodes patterns learned across 3+ review cycles covering 89+ source files, 400+ tests, and multiple feature deliveries.

This is a **read and report** skill. The reviewer reads, finds, and writes findings. They do not fix, commit, or open PRs.

---

## When to Use This Skill

- An agent is asked to "review", "check", "look at", or "assess" code changes
- A PR is ready for review and needs a findings report before the team acts on it
- A full-codebase quality audit is requested
- A specific file or component has been flagged for closer inspection
- Following a significant refactor that touched multiple files

---

## Review Process

### Step 1 — Gather the diff

```powershell
git diff HEAD          # unstaged + staged vs last commit
git diff --cached      # staged only
git diff origin/main   # branch diff against main
```

Read the relevant source files in full when the diff is ambiguous. Diff context is often too narrow to judge correctness.

### Step 2 — Check decisions first

Read `.squad/decisions.md` before flagging anything. Known issues, accepted trade-offs, and documented workarounds live there. Do not re-flag items already captured as P3 monitoring items or accepted decisions.

All accepted trade-offs and monitoring items are documented in `.squad/decisions.md`. Do not re-flag anything already captured there.

### Step 3 — Review for findings

Check each of these categories. All are required on a full review; targeted reviews may focus on the areas touched.

---

## What to Check

### ✅ Correctness

| Check | Signal |
|-------|--------|
| Null-forgiving operator (`!`) without guard | `value!` used when null is actually possible |
| Empty or silent catch blocks | `catch { }` or `catch (Exception) { }` with no log/rethrow |
| Fire-and-forget without error handling | `_ = SomeAsync()` or `Task.Run(...)` with no `.ContinueWith` or `.ConfigureAwait(false)` |
| Missing `await` in async context | `Task`-returning methods called without `await` in test or production code |
| Race conditions in event handlers | `InvokeAsync` with long-running ops and no cancellation token |
| Off-by-one in offset math | Roslyn cursor position offsets adjusted for wrapper prefix length |
| Unguarded `GetDocument()` calls | Roslyn `Document?` from workspace — always check for null before `!` |

**LinqStudio-specific correctness traps:**
- `CompilerService` uses `SemaphoreSlim` — any Roslyn workspace mutation MUST await the lock
- `QueriesWorkspace` and `ProjectWorkspace` raise change events — code using both must not assume event order
- `MonacoProvidersService` global provider dict — components must unregister on `Dispose`, not just on navigation
- Aspire console dependencies: exit code MUST be explicit (`Environment.Exit(0/1)`) — no implicit exits

### 🏗️ Architecture & Layer Violations

Layer order (low → high): `Abstractions → Core → Databases → Blazor → App.WebServer → AppHost`

| Violation | Example |
|-----------|---------|
| Core referencing Blazor | `using LinqStudio.Blazor.*` in Core project |
| Database referencing Core internals | Database generators importing Core services (not abstractions) |
| UI state in wrong layer | Workspace state (`ProjectWorkspace`, `QueriesWorkspace`) defined outside Blazor |
| New database type not in Databases project | EF Core or ADO.NET query logic placed in Core |

### 🧪 Test Coverage

Flag missing tests for any of these:

- New public methods with no corresponding test
- Changed behavior in existing methods with no test update
- Exception paths that have no test exercising the error case
- New database query generators without Testcontainers integration test
- Fire-and-forget event handlers with no test verifying failure tolerance
- Missing awaits in test code (timing-dependent — tests may pass but assertions run before operations complete)

**Coverage targets by area:**
- **Core services** (CompilerService, ProjectService, SettingsService): unit tests required
- **Database generators** (MssqlGenerator, etc.): Testcontainers test required, using named database — not `master`
- **Blazor components**: bunit tests for any new UI behavior
- **E2E scenarios**: Playwright test for any user-visible flow
- **WebServer tests**: currently empty — flag if new endpoints are added with no coverage

### 🛠️ LinqStudio Conventions

Every source file must comply:

| Convention | Rule |
|-----------|------|
| Nullable reference types | `#nullable enable` is project-wide — no `!` without a guard, no `?` suppression without reason |
| File-scoped namespaces | `namespace LinqStudio.X;` — never block-scoped `namespace LinqStudio.X { }` |
| Expression-bodied members | Use `=>` for single-expression properties and methods |
| Implicit usings | Do not add redundant `using System;` — already implicit |
| TreatWarningsAsErrors | Zero warnings policy — any new warning is a build break |
| Async naming | Async methods end in `Async`; sync methods must not |
| IDisposable pattern | Blazor components implementing `IDisposable` must unsubscribe events in `Dispose()` |
| `using`/`await using` | Disposable resources must be disposed; never leave them open |

### 🔧 Error Handling

The project uses a three-layer error handling strategy (decision #7). Flag deviations:

- **Layer 1 (manual):** Expected exceptions must be caught with `ErrorHandlingService` — not silently swallowed
- **Layer 2 (global):** `AppErrorBoundary` catches unexpected exceptions — do not add duplicate global handlers
- **Layer 3 (UI):** Errors must surface to the user via `ErrorDialog` — never silently discard

**Common violations:**
- Empty `catch` blocks (especially in `CompilerService` — historically the most vulnerable area)
- `_ = Task.Run(...)` with no error surface
- Provider callbacks returning `null` silently on failure

### 🧹 Cleanup

Flag but treat as Low severity:

- Dead `using` directives after refactoring
- Commented-out code blocks
- `Console.WriteLine` or `Debug.Write` in production code (not test code)
- `GC.SuppressFinalize()` on classes with no finalizer
- Redundant assignments (e.g., `_flag = false` immediately after initialization)
- Methods with misleading names (not matching what they do)
- Magic strings that should be constants

### ⚠️ Test-Specific Checks

| Check | Why |
|-------|-----|
| `FluentAssertions` usage | Banned — use `Assert.*` (xUnit) only |
| Missing `await` in test methods | Timing-dependent failure; tests appear to pass but assertions may run before ops complete |
| Testcontainers fixtures using `master` | Must use named database — production uses named databases, bugs only surface there |
| Skipped tests without explanation | Must include implementation notes in skip message |
| Test methods without clear arrange/act/assert structure | Reduces readability and future maintainability |

---

## Output Format

Always produce a review in exactly this structure:

```
## Code Review — {Agent Name or "Review"}

### ✅ Looks Good
- {Brief note on things done well, or "No issues in this area."}

### ⚠️ Findings

#### {File or area name}
- **[Severity: High]** {Specific description. File, line if known. What to fix. Why it matters.}
- **[Severity: Medium]** {Specific description.}
- **[Severity: Low]** {Specific description.}

### 🧪 Missing Tests
- {Description of untested code path or behavior. Suggested test name if obvious.}

### 🧹 Cleanup
- {Dead code, commented blocks, unnecessary complexity — Low severity only.}

### Summary
{1-2 sentences: overall quality grade and what must be addressed before this is considered done.}
```

### Severity Guide

| Level | When to use |
|-------|-------------|
| **High** | Likely bug; missing error handling on a critical path; broken or missing-await test; builds would fail if strict linting were applied |
| **Medium** | Code smell that could cause issues under load or edge cases; missing test for changed behavior; convention deviation that affects maintainability |
| **Low** | Naming inconsistency; cleanup item; minor deviation from conventions that doesn't affect correctness |

---

## What NOT To Do

These are hard rules. Violating them breaks the team's review contract.

- **Do NOT make any code changes.** Not even one-line fixes. Review only.
- **Do NOT open PRs or create branches.** That is the implementer's job.
- **Do NOT re-flag items already documented in `.squad/decisions.md`.** Check it first.
- **Do NOT commit changes.** Team directive: only snakex64 commits and pushes (decisions.md, 2026-03-11).
- **Do NOT guess at intent.** If a change is ambiguous, read the full source file for context before flagging.
- **Do NOT flag style that doesn't affect correctness.** No opinions on indentation, spacing, or personal preference.
- **Do NOT flag the Monaco 500ms delay.** It is a documented workaround (decision #6).
- **Do NOT approve changes that have unaddressed High severity findings.** Escalate to the Coordinator instead.

---

## After the Review

1. Write the review summary to `.squad/decisions/inbox/{agent}-review-{brief-slug}.md`
2. Append any new learnings to your agent history file if you have one
3. Do NOT fix anything. Do NOT create branches. Hand back to the Coordinator.

---

## Anti-Patterns

### Anti-Pattern 1: Reviewing without reading decisions.md first
**What happens:** Flagging known trade-offs as new issues, wasting the team's time re-litigating settled decisions.  
**Fix:** Always read `.squad/decisions.md` before starting. Check monitoring items and known issues sections.

### Anti-Pattern 2: Surface-level diff review
**What happens:** Reviewing only the changed lines without reading the full context. Missing issues introduced by indirect changes — e.g., a null returned from a refactored method that a caller doesn't guard against.  
**Fix:** When diffs touch core services (CompilerService, ProjectWorkspace, QueriesWorkspace, any generator), read the full file.

### Anti-Pattern 3: Flagging test coverage that already exists
**What happens:** Recommending tests for paths that are already covered, lowering signal-to-noise in the review.  
**Fix:** Before flagging a missing test, search the test project for the class or method name. Confirm the test doesn't exist.

### Anti-Pattern 4: Missing `await` in test code treated as Low severity
**What happens:** Missing `await` in test async methods looks benign but is a timing-dependent bug — assertions can run before operations complete, producing flaky tests that pass most of the time.  
**Fix:** Always flag missing `await` in test code as **High severity**, not Low.

### Anti-Pattern 5: Ignoring empty catch blocks in CompilerService
**What happens:** CompilerService has historically had silent exception swallowing. Empty catch blocks here mean assembly load failures and Roslyn errors are invisible in production.  
**Fix:** Flag every empty or no-op catch block in `CompilerService` as **High severity**.

### Anti-Pattern 6: Accepting FluentAssertions usage
**What happens:** FluentAssertions is banned (decisions.md, known issue #1). Letting it through in new test code sets back the standardization effort.  
**Fix:** Flag any `using FluentAssertions;` or `.Should().Be(...)` usage as **Medium severity** and note the required replacement (`Assert.*`).

### Anti-Pattern 7: Accepting fire-and-forget without error handling
**What happens:** `_ = SomeAsync()` or `Task.Run(...)` with no `.ContinueWith` means unhandled exceptions vanish silently.  
**Fix:** Flag every fire-and-forget with no error handling as **High severity**.

### Anti-Pattern 8: Approving incomplete Testcontainers fixtures
**What happens:** Database generator tests that connect to `master` instead of a named database pass locally but miss production-only bugs.  
**Fix:** Flag any database fixture that doesn't create and use a named database as **Medium severity**.
