using LinqStudio.Abstractions;
using LinqStudio.Core.Models;
using LinqStudio.Core.Interfaces;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.CodeGeneration;
using Microsoft.Extensions.Logging;

namespace LinqStudio.Core.Services;

/// <summary>
/// Scoped factory used by UI pages to create and initialize CompilerService instances.
/// </summary>
/// <remarks>
/// Each call to <see cref="CreateAsync"/> or <see cref="CreateFromProjectAsync"/> allocates
/// a new Roslyn <c>AdhocWorkspace</c> via <see cref="RoslynWorkspaceService"/>, adds EF Core
/// model files and the generated DbContext as in-memory documents, and builds the compilation.
/// Callers should retain the returned <see cref="CompilerService"/> rather than invoking the
/// factory on every keystroke.
/// </remarks>
/// <param name="roslynWorkspaceService">Service that manages Roslyn workspace and document creation.</param>
/// <param name="generator">
/// Optional EF Core code generator used by <see cref="CreateFromProjectAsync"/>.
/// When <see langword="null"/> both factory methods fall back to the built-in demo model.
/// </param>
/// <param name="logger">Optional logger forwarded to each created <see cref="CompilerService"/>.</param>
public class CompilerServiceFactory(RoslynWorkspaceService roslynWorkspaceService, IDbContextGenerator? generator = null, ILogger<CompilerService>? logger = null) : ICompilerServiceFactory
{
	private readonly RoslynWorkspaceService _roslynWorkspaceService = roslynWorkspaceService;
	private readonly string _defaultContextTypeName = "TestDbContext";
	private readonly string _defaultProjectNamespace = "LinqStudio.TestModels";

	/// <summary>
	/// Create a new CompilerService instance and initialize it with a small hard-coded model.
	/// </summary>
	/// <returns>
	/// A fully initialized <see cref="CompilerService"/> backed by the demo schema
	/// (<c>Person</c> entity + <c>TestDbContext</c> using an in-memory database).
	/// </returns>
	public async Task<CompilerService> CreateAsync()
	{
		var svc = new CompilerService(_defaultContextTypeName, _defaultProjectNamespace, _roslynWorkspaceService, logger);

		var models = new Dictionary<string, string>
		{
			["Person.cs"] =
@"using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LinqStudio.TestModels;

public class Person
{
	[Key]
	public int Id { get; set; }

	public string? Name { get; set; }

	public int Age { get; set; }
}
",
		};

		var dbContext =
@"using Microsoft.EntityFrameworkCore;
using LinqStudio.TestModels;

namespace LinqStudio.TestModels;

public class TestDbContext : DbContext
{
	public DbSet<Person> People { get; set; } = null!;

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		// Intentionally in-memory / stubbed for compilation-only scenarios
		optionsBuilder.UseInMemoryDatabase(""LinqStudioTestDb"");
	}
}
";

		await svc.Initialize(models, dbContext);
		return svc;
	}

	/// <summary>
	/// Creates a CompilerService initialized from the given project's live database schema.
	/// Falls back to the demo model when no database connection is configured on the project.
	/// </summary>
	/// <param name="project">The project whose database schema drives EF Core code generation.</param>
	/// <param name="cancellationToken">Token to cancel the schema generation step.</param>
	/// <returns>
	/// A fully initialized <see cref="CompilerService"/> reflecting the project's schema,
	/// or the demo-model service if <paramref name="project"/> has no generator configured.
	/// </returns>
	public async Task<CompilerService> CreateFromProjectAsync(Project project, CancellationToken cancellationToken = default)
	{
		if (generator is null || project.QueryGenerator is null)
		{
			return await CreateAsync();
		}

		var databases = await project.QueryGenerator.GetDatabasesAsync(cancellationToken);
		if (databases.Count == 0)
			return await CreateAsync();

		var contextTypeNames = CodeGenerationNaming.GetDbContextTypeNames(databases.Select(database => database.Name));
		var generatedContexts = new List<(DatabaseInfo Database, DbContextGeneratorResult Result)>(databases.Count);
		foreach (var database in databases)
		{
			var result = await generator.GenerateAsync(
				project.QueryGenerator,
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
				new KeyValuePair<string, string>(
					$"{context.Result.ContextTypeName}.{file.Key}",
					file.Value)))
			.ToDictionary(file => file.Key, file => file.Value, StringComparer.OrdinalIgnoreCase);
		var dbContextFiles = generatedContexts.ToDictionary(
			context => $"{context.Result.ContextTypeName}.cs",
			context => context.Result.DbContextCode,
			StringComparer.OrdinalIgnoreCase);
		var svc = new CompilerService(
			generatedContexts
				.Select(context => new QueryDbContextParameter(
					context.Result.ContextTypeName,
					context.Result.Namespace,
					CodeGenerationNaming.GetDbContextParameterNameFromTypeName(context.Result.ContextTypeName)))
				.ToList(),
			"GeneratedModels",
			_roslynWorkspaceService,
			logger);
		await svc.Initialize(modelFiles, dbContextFiles);
		return svc;
	}
}
