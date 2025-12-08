using Bunit;
using Xunit;
using LinqStudio.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace LinqStudio.Blazor.Tests;

public class ErrorHandlingServiceTests : BunitContext
{
	[Fact]
	public void ErrorHandlingService_CanBeCreated()
	{
		// Arrange
		Services.AddMudServices();
		Services.AddScoped<ErrorHandlingService>();
		Services.AddLogging();

		// Act
		var service = Services.GetRequiredService<ErrorHandlingService>();

		// Assert
		Assert.NotNull(service);
	}

	[Fact]
	public async Task HandleErrorAsync_DoesNotThrow_WithExceptionMessage()
	{
		// Arrange
		Services.AddMudServices();
		Services.AddScoped<ErrorHandlingService>();
		Services.AddLogging();

		var service = Services.GetRequiredService<ErrorHandlingService>();
		var exception = new InvalidOperationException("Test error message");

		// Act & Assert - The method should not throw
		await service.HandleErrorAsync(exception);
	}

	[Fact]
	public async Task HandleErrorAsync_DoesNotThrow_WithCustomMessage()
	{
		// Arrange
		Services.AddMudServices();
		Services.AddScoped<ErrorHandlingService>();
		Services.AddLogging();

		var service = Services.GetRequiredService<ErrorHandlingService>();
		var exception = new InvalidOperationException("Original error");
		var customMessage = "Custom error message";

		// Act & Assert - The method should not throw
		await service.HandleErrorAsync(exception, customMessage);
	}
}
