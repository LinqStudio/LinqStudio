using Microsoft.Extensions.Logging;

namespace LinqStudio.Core.Services;

/// <summary>
/// Scoped factory used by UI pages to create and initialize CompilerService instances.
/// </summary>
public class CompilerServiceFactory(ILogger<CompilerService>? logger = null)
{
	private readonly string _defaultContextTypeName = "TestDbContext";
	private readonly string _defaultProjectNamespace = "LinqStudio.TestModels";

	/// <summary>
	/// Create a new CompilerService instance and initialize it with a small hard-coded model.
	/// </summary>
	public async Task<CompilerService> CreateAsync()
	{
		var svc = new CompilerService(_defaultContextTypeName, _defaultProjectNamespace, logger);

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
}
