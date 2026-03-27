---
name: roslyn-workspace-management
description: Pattern for creating and managing Roslyn AdhocWorkspace instances with EF Core metadata references for C# code analysis and IntelliSense. Use this when working on CompilerService, query compilation, autocomplete, or any Roslyn-based feature.
---



## Overview
Pattern for creating and managing Roslyn `AdhocWorkspace` instances with EF Core metadata references for C# code analysis and compilation.

## When to Use
- Building IntelliSense/autocomplete features for C# code
- Compiling user-provided C# code at runtime
- Analyzing C# syntax trees and semantic models
- Any scenario requiring Roslyn compiler APIs with EF Core support

## Core Pattern

### 1. Assembly Loading Strategy
Load assemblies in priority order with fallback:

```csharp
// Priority list of assemblies (customize for your domain)
var efCoreAssemblies = new[]
{
    "Microsoft.EntityFrameworkCore",
    "Microsoft.EntityFrameworkCore.Relational",
    "Microsoft.EntityFrameworkCore.SqlServer",
    "Microsoft.EntityFrameworkCore.Sqlite",
    "Npgsql.EntityFrameworkCore.PostgreSQL",
    "MySql.EntityFrameworkCore",
    "System.Linq",
    "System.Linq.Queryable"
};

var references = new List<MetadataReference>();

// Step 1: Try to load priority assemblies
foreach (var asmName in efCoreAssemblies)
{
    // Try AppDomain first (already loaded)
    var asm = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == asmName);
    
    // Fall back to Assembly.Load()
    if (asm == null)
    {
        try { asm = Assembly.Load(asmName); }
        catch (Exception ex) { /* log warning */ }
    }
    
    if (asm != null)
    {
        references.Add(MetadataReference.CreateFromFile(asm.Location));
    }
}

// Step 2: Add all other non-dynamic assemblies from AppDomain
foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
{
    try
    {
        if (!asm.IsDynamic && 
            !string.IsNullOrEmpty(asm.Location) && 
            !efCoreAssemblies.Contains(asm.GetName().Name))
        {
            references.Add(MetadataReference.CreateFromFile(asm.Location));
        }
    }
    catch { /* skip assemblies that fail */ }
}
```

### 2. Workspace Creation
Create workspace with project and references:

```csharp
var workspace = new AdhocWorkspace();
var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create());
var solution = workspace.AddSolution(solutionInfo);
var projectId = ProjectId.CreateNewId();

var projectInfo = ProjectInfo.Create(
    projectId,
    VersionStamp.Create(),
    "MyProject",
    "MyProject",
    LanguageNames.CSharp);

solution = solution.AddProject(projectInfo);

// Add metadata references
solution = solution.WithProjectMetadataReferences(projectId, references);
```

### 3. Query Wrapping Pattern
Wrap user code in a container class for analysis:

```csharp
public string WrapQuery(string userQuery, string contextTypeName, string projectNamespace)
{
    if (!userQuery.TrimEnd().EndsWith(';'))
        userQuery += ";";

    return $$"""
using System;
using System.Linq;
using System.Threading.Tasks;

namespace {{projectNamespace}};

public class QueryContainer
{
    public async Task<IQueryable<object>> Query({{contextTypeName}} context)
    {
        return {{userQuery}}
    }
}
""";
}
```

### 4. Cursor Position Adjustment
When wrapping user code, adjust cursor position for Roslyn APIs:

```csharp
// Calculate offset from wrapper to user code
var placeholder = "__THIS_HERE__";
var prefixCode = WrapQuery(placeholder, contextTypeName, projectNamespace);
var wrappedCursorPosition = prefixCode.IndexOf(placeholder);

// Adjust user cursor position
var absolutePosition = wrappedCursorPosition + userCursorPosition;

// Use for completions, hover, etc.
var completions = await completionService.GetCompletionsAsync(document, absolutePosition);
```

## Architecture Decisions

### Stateless vs Stateful
- **Stateless (recommended)**: Create fresh workspace per operation, no shared state
  - Pros: Thread-safe, no lock contention, simpler lifecycle
  - Cons: Workspace creation overhead per call
  - Use for: Query execution, one-time compilation

- **Stateful**: Maintain long-lived workspace with document updates
  - Pros: Amortized workspace creation cost, reuse semantic models
  - Cons: Requires synchronization (SemaphoreSlim), complex lifecycle
  - Use for: IntelliSense where user edits same document repeatedly

### Service Registration
```csharp
// Stateless - singleton
services.AddSingleton<RoslynWorkspaceService>();

// Stateful - scoped (one per user session)
services.AddScoped<CompilerService>();
```

## Common Pitfalls

### 1. Missing Assemblies
**Problem**: Code compiles but Roslyn can't resolve types  
**Solution**: Ensure all domain-specific assemblies (EF Core, providers) are in priority list

### 2. Dynamic Assemblies
**Problem**: `NotSupportedException` when creating metadata reference  
**Solution**: Filter out dynamic assemblies with `!asm.IsDynamic` check

### 3. Cursor Position Off-By-One
**Problem**: Completions appear at wrong location  
**Solution**: Use placeholder technique (`__THIS_HERE__`) to calculate exact offset

### 4. Memory Leaks
**Problem**: Workspaces/solutions accumulate in memory  
**Solution**: Dispose workspace when done, or use stateless pattern

### 5. Parse Options
**Problem**: XML documentation doesn't appear in hover  
**Solution**: Apply `CSharpParseOptions(documentationMode: DocumentationMode.Diagnose)` to project

## LinqStudio Implementation

See `src/LinqStudio.Core/Services/RoslynWorkspaceService.cs` for production implementation.

### Key Classes
- **RoslynWorkspaceService**: Stateless singleton, creates workspaces with metadata
- **CompilerService**: Stateful scoped service for IntelliSense (long-lived workspace)
- **QueryExecutionService**: Uses RoslynWorkspaceService for one-time compilation

### Testing Pattern
```csharp
// Test fixture helper
private static RoslynWorkspaceService CreateRoslynWorkspaceService() => new();

[Fact]
public async Task Test_WithCompilerService()
{
    using var service = new CompilerService(
        "TestDbContext", 
        "Test", 
        CreateRoslynWorkspaceService());
    
    await service.Initialize(models, dbContextCode);
    // ... test completions, hover, etc.
}
```

## References
- Roslyn Documentation: https://github.com/dotnet/roslyn/wiki
- AdhocWorkspace API: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.adhocworkspace
- MetadataReference: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.metadatareference
