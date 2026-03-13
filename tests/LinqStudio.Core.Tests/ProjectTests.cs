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
	public void UpdateConnection_UpdatesConnectionString_WhenValid()
	{
		var project = new Project();
		project.UpdateConnection(DatabaseType.Mssql, "Server=.;Database=MyDb;");
		Assert.Equal("Server=.;Database=MyDb;", project.ConnectionString);
		Assert.Equal(DatabaseType.Mssql, project.DatabaseType);
	}
}
