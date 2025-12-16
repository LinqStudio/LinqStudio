namespace LinqStudio.Core.Services;

/// <summary>
/// Scoped factory used by UI pages to create and initialize CompilerService instances.
/// This factory currently returns a CompilerService pre-initialized with a small hard-coded model
/// and a DbContext class so the editor has something to complete against out-of-the-box.
/// </summary>
public class CompilerServiceFactory
{
	private readonly CompilerServiceProvider _provider;

	private readonly string _defaultContextTypeName = "TestDbContext";
	private readonly string _defaultProjectNamespace = "LinqStudio.TestModels";

	public CompilerServiceFactory(CompilerServiceProvider provider)
	{
		_provider = provider;
	}

	/// <summary>
	/// Create a new CompilerService instance and initialize it with a small hard-coded model.
	/// </summary>
	public async Task<CompilerService> CreateAsync()
	{
		// Get or create the compiler service from the provider
		var svc = await _provider.GetOrCreateAsync(_defaultContextTypeName, _defaultProjectNamespace);

		// Hard-coded example model files and a DbContext. These are intentionally minimal so the
		// editor has types and a context to provide completions for.
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

		// Initialize with hard-coded models
		await svc.Initialize(models, dbContext);

		return svc;
	}
}
