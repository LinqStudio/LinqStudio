using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.Tests;

public class ProjectTests
{
	[Fact]
	public void UpdateConnection_ThrowsArgumentException_WhenConnectionStringIsEmpty()
	{
		var project = new Project();
		Assert.Throws<ArgumentException>(() => project.UpdateConnection(DatabaseType.Mssql, string.Empty));
	}

	[Fact]
	public void UpdateConnection_ThrowsArgumentException_WhenConnectionStringIsWhitespace()
	{
		var project = new Project();
		Assert.Throws<ArgumentException>(() => project.UpdateConnection(DatabaseType.Mssql, "   "));
	}

	[Fact]
	public void CreateQueryGenerator_CreatesIndependentGenerator()
	{
		var project = new Project
		{
			DatabaseType = DatabaseType.Mssql,
			ConnectionString = "Server=test;Database=test;Integrated Security=true;TrustServerCertificate=true"
		};

		var cachedGenerator = project.QueryGenerator;
		var executionGenerator = project.CreateQueryGenerator();

		Assert.NotNull(cachedGenerator);
		Assert.NotNull(executionGenerator);
		Assert.NotSame(cachedGenerator, executionGenerator);
	}
}
