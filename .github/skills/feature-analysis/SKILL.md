---
name: feature-analysis
description: >
  Covers how to perform deep feature analysis and produce structured feature design documents.
  Use this skill when adding a new feature, extending an existing feature, planning a significant
  code change, or writing a feature analysis document. Keywords: feature, analysis, design,
  implementation plan, architecture, add feature, edit feature, feature request, new capability,
  feature planning, feature design, roadmap, proposal. This skill MUST be loaded by the
  orchestrator agent as well as the worker agent.
---

# Skill: Feature Analysis

## Overview

When asked to analyse a feature (new or modified), produce a structured markdown document that covers the full picture: where the feature fits in the existing codebase, what currently exists in that area, what files must change, the data model, end-to-end technical architecture, permissions, an ordered implementation roadmap, and any open questions.

Generating this as a separate structured markdown document allows for a clear and comprehensive analysis that can be easily shared, reviewed, and referenced by other coding agents — **especially before any implementation begins**.

Use a worker agent per feature phase if a large feature is broken into multiple phases (max 5 agents in parallel, use 'sync' mode). Each worker agent must be told to load this `feature-analysis` skill.

---

## Step-by-Step Workflow

### Step 1 — Deep codebase analysis

Before writing a single line of the document, explore the codebase thoroughly in the area where the feature will be added:

- Read actual code — never assume. Every file path, class name, and line number you cite must be real and verified.
- Identify the existing patterns used for similar features so yours is consistent.

### Step 2 — Write the analysis document

Follow the **Document Structure** section below exactly. Save the document to the current repo as a markdown file.
The worker agent(s) should tell the Orchestrator agent the list of open questions. 

### Step 3 — Chat summary (keep it brief)

After saving the file, post **only a short bullet-point summary** in chat:
- The 3–5 most important findings (complexity rating, key files that change, major risk or open question).
- The full Windows path to the analysis file, clearly labelled

- Tell the user explicitly: **"Read the full file for all details, diagrams, code snippets, and the implementation roadmap."**

Never dump the full document into chat. The file is the deliverable.

### Step 4 — Ask the user for next steps (orchestrator only)

After the worker agent completes the analysis, the **orchestrator** must use the `ask_user` tool to answer any open questions left.
Once everything is answered, use ask_user tool with the following options:

```
Question: "The feature analysis is ready. What would you like to do next?"

Options:
  1. "Implement as proposed (You, as the user, MUST read the full analysis file first before starting)"
  2. "I will start a clean session before implementing"
  3. allow_freeform: true  — for the user to request edits or adjustments to the plan
```

**If the user chooses option 1 (implement):**
- The orchestrator must instruct the worker agent(s) to **read the full analysis file in its entirety** before writing any code. This is mandatory — do not skip it.
- For large features, spin up one worker agent per phase (up to 5 in parallel). Each worker agent must be told to load the `feature-analysis` skill and to read the analysis file first.

**If the user requests edits/adjustments (freeform):**
- The orchestrator must **spin up a new worker agent** to apply the requested changes to the analysis document. The orchestrator must never edit the analysis file directly.
- Provide the worker agent with: the full Windows path to the analysis file, the user's requested changes, and instructions to load the `feature-analysis` skill.
- After edits are saved, repeat Step 4 (ask for next steps again).

---

## Document Structure

Each feature analysis document must follow the sections below. Keep each section focused. Use real code from the codebase, not invented examples.

---

### Title

Format: `FEATURE — <Short description of what is being added/changed and where>`

Immediately below the title, include this header block:

```
| Field        | Value                                               |
|--------------|-----------------------------------------------------|
| Complexity   | 🔴 Large / 🟠 Medium / 🟡 Small                    |
| Component    | e.g. Frontend / Backend / DB / SignalR / All layers |
| Summary      | One sentence describing what the feature does.      |
```

---

### Description

1-paragraph high-level overview of the feature: what it does, why it is needed, and who benefits from it. Avoid implementation details here — save those for later sections.

---

### 1. Current State Analysis

What currently exists in the relevant area of the codebase. Be exhaustive:

- Exact file paths, class names, method names, and line numbers.
- Paste real code snippets (not pseudocode). Use inline comments to point at the key lines.
- Explain what the existing code does and where the feature will hook in.

Include an ASCII flow diagram showing the current data/navigation flow for the affected area:

```
Client (e.g. DashboardComponent)
  │
  ├─► API call: POST /api/xyz
  │     └─► XyzController.Create()       ← line 42, XyzController.cs
  │           └─► XyzService.CreateAsync()  ← line 88, XyzService.cs
  │                 └─► _repository.AddAsync()
  │
  └─► SignalR hub push (if applicable)
```

---

### 2. Where the Feature Fits

Architectural recommendation for where the new feature slots into the existing structure. Justify your choice by referencing existing patterns (name the files/classes that follow the same pattern).

Include a diagram of the proposed structure — showing new components alongside the existing ones they integrate with:

```
Client (NewFeatureComponent)          Existing Neighbours
  │                                     │
  ├─► NEW: GET /api/newfeature  ──────► NewFeatureController  (new)
  │                                       └─► NewFeatureService (new)
  │                                             └─► existing IRepository<T>
  │
  └─► existing SignalR hub (if reused)
```

Avoid repeating yourself between step 1 and 2.

---

### 3. Files That Need to Change

For **each file** that must be created or modified, provide:

1. **Full path** — e.g. `Services/NewFeatureService.cs`
2. **Nature of change** — Create / Add method / Modify method / Register DI / Add route / etc.
3. **Description of changes** - Avoid writing entire snippets here unless the change is very small (<5 lines). Focus on describing the purpose of the change(s) and how it fits into the feature.

Group files into layers (e.g. Frontend, Backend, DB) and order them by implementation phase (see roadmap section). For large features, this can be a multi-level hierarchy.

---

### 4. Data Model

Describe new or modified entities:

- New DB tables / columns / indexes — with their purpose.
- Relationships to existing models (foreign keys, navigation properties).
- Any new DTOs or API contracts (request/response shapes).
- If no DB changes are needed, explicitly state that.

---

### 5. Technical Architecture

End-to-end description of how the feature works at runtime:

- **Data flow** — from user action to DB write and back to the UI.
- **API contract** — HTTP method, route, request body, response shape, status codes.
- **Real-time updates** — if SignalR or polling is involved, describe the hub method and client handler.
- **Error handling** — which exceptions can be thrown, how they map to HTTP status codes, what the UI shows.
- **Caching / performance** — any caching strategy needed, expected data volume.

Use a sequence or flow diagram where helpful:

```
User clicks "Export"
  │
  ├─► POST /api/newfeature/export  { ids: [...] }
  │     └─► NewFeatureController.ExportAsync()
  │           └─► NewFeatureService.ExportAsync()
  │                 ├─► Validate permissions  ← throws 403 if denied
  │                 ├─► Fetch rows from DB
  │                 └─► Stream CSV response
  │
  └─► Browser triggers file download
```

---

### 6. Implementation Roadmap

Break the implementation into ordered phases. Each phase must be independently deployable (or at least independently testable).

For each phase:
- Phase name and goal
- Concrete deliverables: list of files to create or modify (use full paths)
- Dependencies on other phases

Include an ASCII dependency diagram:

```
Phase 1: Data Model & Migrations
  │
  └─► Phase 2: Backend Service + Controller
        │
        ├─► Phase 3a: Frontend component
        └─► Phase 3b: Permissions wiring
              │
              └─► Phase 4: Integration tests
```

> **Parallelism note:** For large features, split one phase into subphases (i.e. 3a, 3b) that can be worked in parallel without causing issues.
One worker agent can be used per sub phase (up to 5 in parallel). 
Each worker agent must be told to load the `feature-analysis` skill and to read this analysis document *in full* before writing any code.

---

### 8. Open Questions

List ambiguities that must be resolved before (or early in) implementation:

- Product decisions: behaviour edge cases, UX choices, scope boundaries.
- Architecture decisions: which existing abstraction to extend vs. create new, breaking-change risk.
- Data decisions: migration strategy for existing data, nullability, defaults.

Format each as a numbered question with a brief note on who should answer it:

```
1. Should the export support partial selection or always export all rows?
   → Product decision

2. Does this feature require a new DB migration or can it reuse the existing `Settings` table?
   → Architecture decision — check with team lead before Phase 1 begins.
```

Open questions should be asked to the user using the ask_user tool **after the analysis is complete but before implementation begins**. The orchestrator must not proceed to implementation until all open questions are resolved.

---

## Gotchas

- **No pseudocode.** Every code snippet in the document must compile or at least be syntactically real.
- **Cite real line numbers.**
- **Follow existing patterns.** Before proposing a new abstraction, search for how the closest existing feature was built. Match its naming, DI registration style, and error-handling approach.
- **Keep chat output short.** The document is the deliverable. Chat should only contain the bullet summary and the file path.
- **Always give the full Windows path.** Never use `~/...`, relative paths, or Unix-style paths when telling the user where the file is saved.
- **Orchestrator must not edit the analysis file directly.** If the user requests changes to the plan, spin up a worker agent to do it.
- **Worker agents must read the file before coding.** When implementing, always instruct the worker agent to open and read the full analysis document before touching any source files.
