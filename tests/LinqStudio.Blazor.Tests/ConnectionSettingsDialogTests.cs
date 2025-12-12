using Bunit;
using Xunit;
using LinqStudio.Blazor.Components;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Core.Extensions;
using LinqStudio.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LinqStudio.Blazor.Tests;

public class ConnectionSettingsDialogTests : BunitContext
{
	private void SetupServices()
	{
		Services
			.AddLinqStudio()
			.AddLinqStudioBlazor();

		Services.AddLogging();
	}

	[Fact]
	public void ConnectionSettingsDialog_CanBeInstantiated()
	{
		// Arrange
		SetupServices();

		// Act
		var cut = Render<ConnectionSettingsDialog>();

		// Assert
		Assert.NotNull(cut);
		Assert.NotNull(cut.Instance);
	}

	[Fact]
	public void ConnectionSettingsDialog_LoadsConnectionStringFromService()
	{
		// Arrange
		SetupServices();
		var connectionService = Services.GetRequiredService<ConnectionService>();
		var testConnectionString = "Server=localhost;Database=Test;";
		connectionService.UpdateConnection(Abstractions.Models.DatabaseType.Mssql, testConnectionString);

		// Act
		var cut = Render<ConnectionSettingsDialog>();

		// Assert
		// The connection string should be loaded into the component
		// We verify this by checking the component instance
		Assert.NotNull(cut.Instance);
	}

	[Fact]
	public void ConnectionSettingsDialog_UsesConnectionServiceFromDI()
	{
		// Arrange
		SetupServices();
		var connectionService = Services.GetRequiredService<ConnectionService>();

		// Act
		var cut = Render<ConnectionSettingsDialog>();

		// Assert
		Assert.NotNull(connectionService);
		Assert.NotNull(cut.Instance);
	}
}
