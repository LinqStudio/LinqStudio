using LinqStudio.Abstractions;
using LinqStudio.Core.Models;
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
	/// Creates a CompilerService initialized from the given server connection's live database schema.
	/// Falls back to the demo model when no database connection is configured.
	/// </summary>
	/// <param name="connection">The server connection whose database schema drives EF Core code generation.</param>
	/// <param name="cancellationToken">Token to cancel the schema generation step.</param>
	/// <returns>
	/// A fully initialized <see cref="CompilerService"/> reflecting the connection's schema,
	/// or the demo-model service if <paramref name="connection"/> has no generator configured.
	/// </returns>
	public async Task<CompilerService> CreateFromConnectionAsync(ServerConnection connection, CancellationToken cancellationToken = default)
	{
		if (generator is null || connection.QueryGenerator is null)
		{
			return await CreateAsync();
		}

		var result = await generator.GenerateAsync(connection.QueryGenerator, cancellationToken);
		var svc = new CompilerService(result.ContextTypeName, result.Namespace, _roslynWorkspaceService, logger);
		await svc.Initialize(result.ModelFiles, result.DbContextCode);
		return svc;
	}
}
