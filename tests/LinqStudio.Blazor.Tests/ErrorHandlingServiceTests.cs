using Bunit;
using FluentAssertions;
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
		service.Should().NotBeNull();
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

		// Act
		var act = async () => await service.HandleErrorAsync(exception);

		// Assert - The method should not throw
		await act.Should().NotThrowAsync();
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

		// Act
		var act = async () => await service.HandleErrorAsync(exception, customMessage);

		// Assert - The method should not throw
		await act.Should().NotThrowAsync();
	}
}
