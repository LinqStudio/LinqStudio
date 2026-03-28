using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;
using LinqStudio.Core.Settings;
using LinqStudio.Databases;
using LinqStudio.Databases.PostgreSQL;
using LinqStudio.Databases.SQLite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinqStudio.Core.Services;

public class QueryExecutionService(
	IDbContextGenerator generator,
	RoslynWorkspaceService roslynWorkspaceService,
	IOptionsMonitor<QueryExecutionSettings> settings,
	ILogger<QueryExecutionService>? logger = null) : IQueryExecutionService
{
	private readonly IDbContextGenerator _generator = generator;
	private readonly RoslynWorkspaceService _roslynWorkspaceService = roslynWorkspaceService;
	private readonly IOptionsMonitor<QueryExecutionSettings> _settings = settings;
	private readonly ILogger<QueryExecutionService>? _logger = logger;

	public async Task<QueryExecutionResult> ExecuteQueryAsync(string userQuery, Models.Project project, CancellationToken cancellationToken = default)
	{ 
		var stopwatch = Stopwatch.StartNew();
		try
		{
			// Validate project has connection configured
			if (string.IsNullOrWhiteSpace(project.ConnectionString))
			{
				return QueryExecutionResult.FromError("No database connection configured", isCompileError: false, stopwatch.Elapsed);
			}

			// Generate models and DbContext from project's database
			var generatorResult = await _generator.GenerateAsync(project.QueryGenerator!, cancellationToken);

			// Step 1-2: Wrap user query in QueryContainer
			var wrappedQuery = _roslynWorkspaceService.WrapQuery(userQuery, generatorResult.ContextTypeName, generatorResult.Namespace);

			// Step 3: Compile to IL
			var (success, assembly, alc, diagnostics) = await CompileToAssemblyAsync(
				wrappedQuery,
				generatorResult.ModelFiles,
				generatorResult.DbContextCode,
				cancellationToken);

			if (!success || assembly == null)
			{
				alc?.Unload();
				stopwatch.Stop();
				var errorMessage = diagnostics;
				return QueryExecutionResult.FromError(errorMessage, isCompileError: true, stopwatch.Elapsed);
			}

			// All assembly-dependent execution is wrapped so alc.Unload() runs after results are materialized
			try
			{
				// Step 5: Instantiate DbContext with real connection
				var dbContextOptions = CreateDbContextOptions(project.DatabaseType, project.ConnectionString);
				var dbContextType = assembly.GetType($"{generatorResult.Namespace}.{generatorResult.ContextTypeName}");
				if (dbContextType == null)
				{
					return QueryExecutionResult.FromError($"Could not find DbContext type {generatorResult.ContextTypeName}", isCompileError: false, stopwatch.Elapsed);
				}

				await using var dbContext = Activator.CreateInstance(dbContextType, dbContextOptions) as DbContext;
				if (dbContext == null)
				{
					return QueryExecutionResult.FromError("Failed to instantiate DbContext", isCompileError: false, stopwatch.Elapsed);
				}

				// Step 6: Invoke QueryContainer.Query(dbContext)
				var queryContainerType = assembly.GetType($"{generatorResult.Namespace}.QueryContainer");
				if (queryContainerType == null)
				{
					return QueryExecutionResult.FromError("Could not find QueryContainer type", isCompileError: false, stopwatch.Elapsed);
				}

				var queryMethod = queryContainerType.GetMethod("Query");
				if (queryMethod == null)
				{
					return QueryExecutionResult.FromError("Could not find Query method", isCompileError: false, stopwatch.Elapsed);
				}

				var queryContainer = Activator.CreateInstance(queryContainerType);
				if (queryContainer == null)
				{
					return QueryExecutionResult.FromError("Failed to instantiate QueryContainer", isCompileError: false, stopwatch.Elapsed);
				}

				// Invoke the user query method, which should return Task<IQueryable<object>>
				var queryTask = queryMethod.Invoke(queryContainer, [dbContext]) as Task<IQueryable<object>>;
				if (queryTask == null)
				{
					return QueryExecutionResult.FromError("Query method did not return expected Task", isCompileError: false, stopwatch.Elapsed);
				}

				var queryable = await queryTask;

				// Step 7: Materialize results
				// Capture the SQL before materializing — ToQueryString() is only available before execution
				string? generatedSql = null;
				try { generatedSql = queryable.ToQueryString(); }
				catch { /* Some queries (e.g. raw SQL, projections) may not support ToQueryString */ }

				// Apply timeout if configured
				var timeoutSeconds = _settings.CurrentValue.TimeoutSeconds;
				if (timeoutSeconds > 0)
				{
					using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
					using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
					
					var items = await queryable.ToListAsync(linkedCts.Token);
					var (columnNames, rows) = ExtractResults(items);

					return new QueryExecutionResult
					{
						Rows = rows,
						ColumnNames = columnNames,
						Elapsed = stopwatch.Elapsed,
						GeneratedCSharp = wrappedQuery,
						GeneratedSql = generatedSql
					};
				}
				else
				{
					// No timeout (timeout = 0)
					var items = await queryable.ToListAsync(cancellationToken);
					var (columnNames, rows) = ExtractResults(items);

					return new QueryExecutionResult
					{
						Rows = rows,
						ColumnNames = columnNames,
						Elapsed = stopwatch.Elapsed,
						GeneratedCSharp = wrappedQuery,
						GeneratedSql = generatedSql
					};
				}
			}
			finally
			{
				alc?.Unload();
			}
		}
		catch (OperationCanceledException)
		{
			_logger?.LogWarning("Query execution cancelled");
			return QueryExecutionResult.FromError("Query execution was cancelled", isCompileError: false, stopwatch.Elapsed);
		}
		catch (Exception ex)
		{
			_logger?.LogError(ex, "Query execution failed with runtime error");
			return QueryExecutionResult.FromError(ex.Message, isCompileError: false, stopwatch.Elapsed);
		}
	}

	private DbContextOptions CreateDbContextOptions(DatabaseType databaseType, string connectionString)
	{
		var builder = new DbContextOptionsBuilder();

		// TODO use reflection to find an attribute or generator classes, then call abstract method on that. That way we handle any new types without forgetting this.
		switch (databaseType)
		{
			case DatabaseType.Mssql:
				builder.UseSqlServer(connectionString);
				break;
			case DatabaseType.MySql:
				builder.UseMySQL(connectionString);
				break;
			case DatabaseType.PostgreSql:
				builder.UseNpgsql(connectionString);
				break;
			case DatabaseType.Sqlite:
				builder.UseSqlite(connectionString);
				break;
			default:
				throw new NotSupportedException($"Database type {databaseType} is not supported");
		}

		return builder.Options;
	}

	private async Task<(bool Success, Assembly? Assembly, AssemblyLoadContext? Alc, string Diagnostics)> CompileToAssemblyAsync(
		string wrappedQuery,
		Dictionary<string, string> modelFiles,
		string dbContextCode,
		CancellationToken cancellationToken)
	{
		// Create workspace using shared service — disposed at end of method via using
		var workspaceResult = _roslynWorkspaceService.CreateWorkspace("QueryExecution");
		using var workspace = workspaceResult.Workspace;
		var projectId = workspaceResult.ProjectId;
		var solution = workspaceResult.Solution;

		// Add all documents at once
		solution = _roslynWorkspaceService.AddDocuments(
			solution,
			projectId,
			modelFiles,
			dbContextCode,
			wrappedQuery);

		// Get compilation
		var project = solution.GetProject(projectId);
		if (project == null)
		{
			return (false, null, null, "Failed to create project");
		}

		var compilation = await project.GetCompilationAsync(cancellationToken);
		if (compilation == null)
		{
			return (false, null, null, "Failed to get compilation");
		}

		// Change OutputKind to DynamicallyLinkedLibrary (fixes CS5001: no Main method needed)
		compilation = compilation.WithOptions(
			((CSharpCompilationOptions)compilation.Options).WithOutputKind(OutputKind.DynamicallyLinkedLibrary));

		// Emit to memory stream
		using var ms = new MemoryStream();
		var emitResult = compilation.Emit(ms, cancellationToken: cancellationToken);

		if (!emitResult.Success)
		{
			var errors = emitResult.Diagnostics
				.Where(d => d.Severity == DiagnosticSeverity.Error)
				.Select(d => $"{d.Id}: {d.GetMessage()}")
				.ToList();

			return (false, null, null, string.Join("\n", errors));
		}

		// Load assembly into a collectible AssemblyLoadContext — caller must call alc.Unload() after use
		ms.Position = 0;
		var alc = new AssemblyLoadContext("query-exec", isCollectible: true);
		var assembly = alc.LoadFromStream(ms);
		return (true, assembly, alc, string.Empty);
	}

	private static (IReadOnlyList<string> ColumnNames, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows) ExtractResults(List<object> items)
	{
		if (items.Count == 0) 
			return ([], []);

		var firstItem = items[0];
		var type = firstItem.GetType();

		// Check if it's a primitive/simple type
		if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
			|| type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid))
		{
			var columns = new[] { "Value" };
			var rows = items.Select(item =>
				(IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Value"] = item }
			).ToList();
			return (columns, rows);
		}

		// Use reflection for complex types
		var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
		if (props.Length == 0)
		{
			// Fallback for types with no public properties
			var columns2 = new[] { "Value" };
			var rows2 = items.Select(item =>
				(IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Value"] = item?.ToString() }
			).ToList();
			return (columns2, rows2);
		}

		var colNames = props.Select(p => p.Name).ToList();
		var resultRows = items.Select(item =>
		{
			var dict = new Dictionary<string, object?>();
			foreach (var prop in props)
			{
				try { dict[prop.Name] = prop.GetValue(item); }
				catch { dict[prop.Name] = null; }
			}
			return (IReadOnlyDictionary<string, object?>)dict;
		}).ToList();

		return (colNames, resultRows);
	}
}
