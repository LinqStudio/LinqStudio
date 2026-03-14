using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace LinqStudio.Core.Services;

/// <summary>
/// Provides shared Roslyn workspace creation and query wrapping functionality.
/// Centralizes duplicate code from CompilerService and QueryExecutionService.
/// </summary>
public class RoslynWorkspaceService(ILogger<RoslynWorkspaceService>? logger = null)
{
	private readonly ILogger<RoslynWorkspaceService>? _logger = logger;

	/// <summary>
	/// Creates a new AdhocWorkspace with a project pre-configured with all EF Core metadata references.
	/// </summary>
	/// <param name="projectName">The name of the project to create in the workspace.</param>
	/// <returns>A tuple containing the workspace, project ID, and initial solution.</returns>
	public (AdhocWorkspace Workspace, ProjectId ProjectId, Solution Solution) CreateWorkspace(string projectName)
	{
		var workspace = new AdhocWorkspace();
		var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create());
		var solution = workspace.AddSolution(solutionInfo);
		var projectId = ProjectId.CreateNewId();

		var projectInfo = ProjectInfo.Create(
			projectId,
			VersionStamp.Create(),
			projectName,
			projectName,
			LanguageNames.CSharp);

		solution = solution.AddProject(projectInfo);

		// Add all metadata references
		var references = GetMetadataReferences();
		solution = solution.WithProjectMetadataReferences(projectId, references);

		return (workspace, projectId, solution);
	}

	/// <summary>
	/// Returns the complete set of MetadataReferences for EF Core + all DB providers + common system assemblies.
	/// Uses the comprehensive assembly list from QueryExecutionService (includes SQLite, PostgreSQL, MySQL).
	/// </summary>
	public IReadOnlyList<MetadataReference> GetMetadataReferences()
	{
		// Use the complete list from QueryExecutionService - it has all DB providers
		var efCoreAssemblies = new[]
		{
			"Microsoft.EntityFrameworkCore",
			"Microsoft.EntityFrameworkCore.Relational",
			"Microsoft.EntityFrameworkCore.SqlServer", // Must be able to remove those that aren't needed based on the project type
			"Microsoft.EntityFrameworkCore.Sqlite",
			"Npgsql.EntityFrameworkCore.PostgreSQL",
			"MySql.EntityFrameworkCore",
			"System.Linq",
			"System.Linq.Queryable"
		};

		var references = new List<MetadataReference>();

		// Try to load priority assemblies first
		foreach (var asmName in efCoreAssemblies)
		{
			var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == asmName);
			if (asm == null)
			{
				try
				{
					asm = Assembly.Load(asmName);
				}
				catch (Exception ex)
				{
					_logger?.LogWarning(ex, "[RoslynWorkspaceService] Error loading assembly {AsmName}", asmName);
				}
			}

			if (asm != null)
			{
				references.Add(MetadataReference.CreateFromFile(asm.Location));
			}
		}

		// Add all remaining assemblies from the current domain
		foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
		{
			try
			{
				if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location) && !efCoreAssemblies.Contains(asm.GetName().Name))
				{
					references.Add(MetadataReference.CreateFromFile(asm.Location));
				}
			}
			catch (Exception ex)
			{
				_logger?.LogWarning(ex, "[RoslynWorkspaceService] Error adding metadata reference for {AsmName}", asm.GetName().Name);
			}
		}

		return references;
	}

	/// <summary>
	/// Wraps a user LINQ query in a QueryContainer class for Roslyn analysis or compilation.
	/// </summary>
	/// <param name="userQuery">The user's LINQ query code.</param>
	/// <param name="contextTypeName">The DbContext type name (e.g., "MyDbContext").</param>
	/// <param name="projectNamespace">The namespace for the generated QueryContainer class.</param>
	/// <param name="beforeReturn">Text to prepend before the user query (default: "return").</param>
	/// <returns>Complete C# source code with the wrapped query.</returns>
	public string WrapQuery(string userQuery, string contextTypeName, string projectNamespace, string beforeReturn = "return")
	{
		if (!userQuery.TrimEnd().EndsWith(';'))
			userQuery += ";";

		return $$"""
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace {{projectNamespace}};

public class QueryContainer
{
    public async Task<IQueryable<object>> Query({{contextTypeName}} context)
    {
        {{beforeReturn}} {{userQuery}}
    }
}
""";
	}

	/// <summary>
	/// Adds model files, a DbContext file, and a query wrapper file to an existing Roslyn project.
	/// Returns the updated solution.
	/// </summary>
	public Solution AddDocuments(
		Solution solution,
		ProjectId projectId,
		IReadOnlyDictionary<string, string> modelFiles,
		string dbContextCode,
		string wrappedQuery,
		string queryFileName = "QueryContainer.cs")
	{
		foreach (var (fileName, code) in modelFiles)
		{
			var docId = DocumentId.CreateNewId(projectId);
			solution = solution.AddDocument(docId, fileName, SourceText.From(code));
		}

		var dbContextDocId = DocumentId.CreateNewId(projectId);
		solution = solution.AddDocument(dbContextDocId, "DbContext.cs", SourceText.From(dbContextCode));

		var queryDocId = DocumentId.CreateNewId(projectId);
		solution = solution.AddDocument(queryDocId, queryFileName, SourceText.From(wrappedQuery));

		return solution;
	}
}
