using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.Loader;
using LinqStudioProject = LinqStudio.Core.Models.Project;

namespace LinqStudio.Core.Services;

/// <summary>
/// Builds and caches the generated project model for a user scope.
/// </summary>
/// <remarks>
/// Query executions compile only their wrapper source and reference the cached model
/// assembly. A new model is produced only after <see cref="Invalidate"/> is called.
/// </remarks>
public sealed class ProjectCompilationService(
	IDbContextGenerator generator,
	RoslynWorkspaceService roslynWorkspaceService,
	ILogger<ProjectCompilationService>? logger = null) : IDisposable
{
	// The build lock prevents duplicate model compilations when several editor operations
	// request the snapshot concurrently.
	private readonly IDbContextGenerator _generator = generator;
	private readonly RoslynWorkspaceService _roslynWorkspaceService = roslynWorkspaceService;
	private readonly ILogger<ProjectCompilationService>? _logger = logger;
	private readonly SemaphoreSlim _lock = new(1, 1);
	private readonly object _stateLock = new();
	// The owner reference is released when the snapshot is replaced or this service is disposed.
	private CompiledProjectSnapshot? _currentSnapshot;
	private bool _invalidated = true;
	private long _invalidationVersion;
	private long _version;
	private bool _disposed;

	/// <summary>
	/// Gets the current compiled model or builds it when the cache is invalid.
	/// </summary>
	public async Task<CompiledProjectSnapshotLease> GetOrBuildAsync(
		LinqStudioProject project,
		CancellationToken cancellationToken = default)
	{
		await _lock.WaitAsync(cancellationToken);
		try
		{
			while (true)
			{
				long buildInvalidationVersion;
				lock (_stateLock)
				{
					ObjectDisposedException.ThrowIf(_disposed, this);
					if (!_invalidated && _currentSnapshot is not null)
						return _currentSnapshot.AcquireLease();

					buildInvalidationVersion = _invalidationVersion;
				}

				var snapshot = await BuildAsync(project, cancellationToken);
				lock (_stateLock)
				{
					if (_disposed)
					{
						snapshot.Dispose();
						throw new ObjectDisposedException(nameof(ProjectCompilationService));
					}
					if (buildInvalidationVersion != _invalidationVersion)
					{
						snapshot.Dispose();
						continue;
					}

					if (_currentSnapshot is not null)
						_currentSnapshot.Release();

					_currentSnapshot = snapshot;
					_invalidated = false;
					return snapshot.AcquireLease();
				}
			}
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Marks the cached model for rebuilding before the next request.
	/// </summary>
	public void Invalidate()
	{
		lock (_stateLock)
		{
			if (_disposed)
				return;

			_invalidated = true;
			_invalidationVersion++;
		}
	}

	/// <summary>
	/// Generates, compiles, and loads the project model into a collectible context.
	/// </summary>
	private async Task<CompiledProjectSnapshot> BuildAsync(
		LinqStudioProject project,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(project.ConnectionString))
			throw new InvalidOperationException("A database connection is required to compile the project model.");

		var databaseGenerator = project.CreateQueryGenerator();
		var databases = await databaseGenerator.GetDatabasesAsync(cancellationToken);
		if (databases.Count == 0)
			throw new InvalidOperationException("No databases are available for this connection.");

		var contextTypeNames = CodeGenerationNaming.GetDbContextTypeNames(databases.Select(database => database.Name));
		var generatedContexts = new List<(DatabaseInfo Database, DbContextGeneratorResult Result)>(databases.Count);
		foreach (var database in databases)
		{
			var result = await _generator.GenerateAsync(
				databaseGenerator,
				database.Name,
				project.CustomRelationships
					.Where(relationship => relationship.DatabaseName.Equals(database.Name, StringComparison.OrdinalIgnoreCase))
					.ToList(),
				contextTypeNames[database.Name],
				cancellationToken);
			generatedContexts.Add((database, result));
		}

		var modelFiles = generatedContexts
			.SelectMany(context => context.Result.ModelFiles.Select(file =>
				new KeyValuePair<string, string>($"{context.Result.ContextTypeName}.{file.Key}", file.Value)))
			.ToDictionary(file => file.Key, file => file.Value, StringComparer.OrdinalIgnoreCase);
		var dbContextFiles = generatedContexts.ToDictionary(
			context => $"{context.Result.ContextTypeName}.cs",
			context => context.Result.DbContextCode,
			StringComparer.OrdinalIgnoreCase);
		var sourceFiles = modelFiles
			.Concat(dbContextFiles)
			.ToDictionary(file => file.Key, file => file.Value, StringComparer.OrdinalIgnoreCase);

		var workspaceResult = _roslynWorkspaceService.CreateWorkspace($"GeneratedModels{Interlocked.Increment(ref _version)}");
		using var workspace = workspaceResult.Workspace;
		var solution = _roslynWorkspaceService.AddSourceDocuments(
			workspaceResult.Solution,
			workspaceResult.ProjectId,
			sourceFiles,
			"");
		var roslynProject = solution.GetProject(workspaceResult.ProjectId)
			?? throw new InvalidOperationException("Failed to create the generated model project.");
		var compilation = await roslynProject.GetCompilationAsync(cancellationToken)
			?? throw new InvalidOperationException("Failed to compile the generated model project.");

		compilation = compilation.WithOptions(
			((CSharpCompilationOptions)compilation.Options)
				.WithOutputKind(OutputKind.DynamicallyLinkedLibrary)
				.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default));

		await using var stream = new MemoryStream();
		var emitResult = compilation.Emit(stream, cancellationToken: cancellationToken);
		if (!emitResult.Success)
		{
			var diagnostics = string.Join(
				Environment.NewLine,
				emitResult.Diagnostics
					.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
					.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}"));
			throw new InvalidOperationException($"Generated model compilation failed:{Environment.NewLine}{diagnostics}");
		}

		var assemblyBytes = stream.ToArray();
		var loadContext = new AssemblyLoadContext($"linqstudio-model-{Guid.NewGuid():N}", isCollectible: true);
		Assembly assembly;
		using (var assemblyStream = new MemoryStream(assemblyBytes, writable: false))
			assembly = loadContext.LoadFromStream(assemblyStream);

		var metadataReference = MetadataReference.CreateFromImage(assemblyBytes);
		var contexts = generatedContexts
			.Select(context => new CompiledProjectContext(
				context.Database.Name,
				context.Result.ContextTypeName,
				context.Result.Namespace))
			.ToList();

		_logger?.LogInformation(
			"[ProjectCompilationService] Built model snapshot with {ContextCount} contexts",
			contexts.Count);

		return new CompiledProjectSnapshot(
			contexts,
			modelFiles,
			dbContextFiles,
			assemblyBytes,
			loadContext,
			assembly,
			metadataReference);
	}

	public void Dispose()
	{
		lock (_stateLock)
		{
			if (_disposed)
				return;

			_disposed = true;
			_currentSnapshot?.Release();
			_currentSnapshot = null;
		}
		_lock.Dispose();
		GC.SuppressFinalize(this);
	}
}
