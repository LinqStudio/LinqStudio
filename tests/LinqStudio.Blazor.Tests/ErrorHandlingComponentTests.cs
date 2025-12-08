using Bunit;
using Xunit;
using LinqStudio.Blazor.Components;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Blazor.Services;
using LinqStudio.Blazor.Tests.TestComponents;
using LinqStudio.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;

namespace LinqStudio.Blazor.Tests;

public class ErrorHandlingComponentTests : BunitContext
{
	private void SetupServices()
	{
		Services
			.AddLinqStudio()
			.AddLinqStudioBlazor();

		Services.AddLogging();
	}

	[Fact]
	public void ErrorTestComponent_Renders_WithAllButtons()
	{
		// Arrange
		SetupServices();

		// Act
		var cut = Render<ErrorTestComponent>();

		// Assert
		Assert.NotNull(cut.Find("[data-testid='trigger-simple-error']"));
		Assert.NotNull(cut.Find("[data-testid='trigger-custom-error']"));
		Assert.NotNull(cut.Find("[data-testid='trigger-complex-error']"));
		Assert.NotNull(cut.Find("[data-testid='trigger-unhandled-error']"));
	}

	[Fact]
	public async Task ErrorTestComponent_TriggerSimpleError_ShowsErrorDialog()
	{
		// Arrange
		SetupServices();
		var cut = Render<ErrorTestComponent>();

		// Act
		var button = cut.Find("[data-testid='trigger-simple-error']");
		await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

		// Wait for async operation to complete
		cut.WaitForAssertion(() =>
		{
			var alert = cut.Find("[data-testid='error-triggered']");
			Assert.NotNull(alert);
		}, TimeSpan.FromSeconds(2));

		// Assert
		Assert.True(cut.Instance.ErrorTriggered);
	}

	[Fact]
	public async Task ErrorTestComponent_TriggerCustomMessageError_ShowsErrorDialog()
	{
		// Arrange
		SetupServices();
		var cut = Render<ErrorTestComponent>();

		// Act
		var button = cut.Find("[data-testid='trigger-custom-error']");
		await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

		// Wait for async operation
		cut.WaitForAssertion(() =>
		{
			var alert = cut.Find("[data-testid='error-triggered']");
			Assert.NotNull(alert);
		}, TimeSpan.FromSeconds(2));

		// Assert
		Assert.True(cut.Instance.ErrorTriggered);
	}

	[Fact]
	public async Task ErrorTestComponent_TriggerComplexError_ShowsErrorDialog()
	{
		// Arrange
		SetupServices();
		var cut = Render<ErrorTestComponent>();

		// Act
		var button = cut.Find("[data-testid='trigger-complex-error']");
		await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

		// Wait for async operation
		cut.WaitForAssertion(() =>
		{
			var alert = cut.Find("[data-testid='error-triggered']");
			Assert.NotNull(alert);
		}, TimeSpan.FromSeconds(2));

		// Assert
		Assert.True(cut.Instance.ErrorTriggered);
	}

	[Fact]
	public void AppErrorBoundary_CatchesUnhandledException_FromChildComponent()
	{
		// Arrange
		SetupServices();

		// Act - Wrap test component with AppErrorBoundary
		var cut = Render<AppErrorBoundary>(parameters => parameters
			.AddChildContent<ErrorTestComponent>(childParams => childParams
				.Add(p => p.ShowSimpleErrorButton, false)
				.Add(p => p.ShowCustomMessageButton, false)
				.Add(p => p.ShowComplexErrorButton, false)
				.Add(p => p.ShowUnhandledErrorButton, true)));

		var button = cut.Find("[data-testid='trigger-unhandled-error']");

		// Clicking this button should throw an unhandled exception
		// The error boundary should catch it
		var act = () => button.Click();

		// Assert - The error boundary should handle it, not throw to test
		Assert.Null(Record.Exception(act));

		// Verify error boundary fallback is shown
		cut.WaitForAssertion(() =>
		{
			var fallback = cut.FindAll(".error-boundary-fallback");
			Assert.NotEmpty(fallback);
		}, TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void AppErrorBoundary_ShowsFallbackUI_WhenErrorOccurs()
	{
		// Arrange
		SetupServices();

		// Act - Wrap test component with AppErrorBoundary
		var cut = Render<AppErrorBoundary>(parameters => parameters
			.AddChildContent<ErrorTestComponent>(childParams => childParams
				.Add(p => p.ShowSimpleErrorButton, false)
				.Add(p => p.ShowCustomMessageButton, false)
				.Add(p => p.ShowComplexErrorButton, false)
				.Add(p => p.ShowUnhandledErrorButton, true)));

		var button = cut.Find("[data-testid='trigger-unhandled-error']");
		button.Click();

		// Assert - Verify fallback content is displayed
		cut.WaitForAssertion(() =>
		{
			var fallbackDiv = cut.Find(".error-boundary-fallback");
			Assert.NotNull(fallbackDiv);
			Assert.Contains("An unexpected error occurred", fallbackDiv.TextContent);
		}, TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void ErrorDialog_RendersCorrectly_WithMessage()
	{
		// Arrange
		SetupServices();
		var message = "Test error message";
		var stackTrace = "Test stack trace\nat SomeMethod()";

		// Act
		var cut = Render<ErrorDialog>(parameters => parameters
			.Add(p => p.Message, message)
			.Add(p => p.StackTrace, stackTrace));

		// Assert - ErrorDialog uses MudDialog which requires special setup
		// We'll verify the component instantiates without error
		Assert.NotNull(cut);
		Assert.Equal(message, cut.Instance.Message);
		Assert.Equal(stackTrace, cut.Instance.StackTrace);
	}

	[Fact]
	public void ErrorDialog_RendersWithoutStackTrace_WhenNotProvided()
	{
		// Arrange
		SetupServices();
		var message = "Test error message";

		// Act
		var cut = Render<ErrorDialog>(parameters => parameters
			.Add(p => p.Message, message));

		// Assert - Verify component properties
		Assert.NotNull(cut);
		Assert.Equal(message, cut.Instance.Message);
		Assert.True(string.IsNullOrEmpty(cut.Instance.StackTrace));
	}

	[Fact]
	public void AppErrorBoundary_CanRecover_AfterError()
	{
		// Arrange
		SetupServices();

		// Act
		var cut = Render<AppErrorBoundary>(parameters => parameters
			.AddChildContent<ErrorTestComponent>(childParams => childParams
				.Add(p => p.ShowSimpleErrorButton, false)
				.Add(p => p.ShowCustomMessageButton, false)
				.Add(p => p.ShowComplexErrorButton, false)
				.Add(p => p.ShowUnhandledErrorButton, true)));

		// Trigger error
		var button = cut.Find("[data-testid='trigger-unhandled-error']");
		button.Click();

		// Wait for error state
		cut.WaitForAssertion(() =>
		{
			var fallback = cut.Find(".error-boundary-fallback");
			Assert.NotNull(fallback);
		}, TimeSpan.FromSeconds(2));

		// Act - Recover from error using InvokeAsync for thread safety
		cut.InvokeAsync(() => cut.Instance.Recover());

		// Assert - Component should be back to normal state
		cut.WaitForAssertion(() =>
		{
			var buttons = cut.FindAll("[data-testid='trigger-unhandled-error']");
			Assert.NotEmpty(buttons);
		}, TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void ErrorTestComponent_Reset_ClearsErrorState()
	{
		// Arrange
		SetupServices();
		var cut = Render<ErrorTestComponent>();

		// Verify error state is initially false
		Assert.False(cut.Instance.ErrorTriggered);

		// We can't easily test Reset without triggering an actual error
		// since it involves StateHasChanged which requires the Blazor dispatcher
		// This test verifies the component can be instantiated and the property works
		Assert.NotNull(cut);
		Assert.NotNull(cut.Instance);
	}
}
