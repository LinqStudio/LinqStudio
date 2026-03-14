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
}
