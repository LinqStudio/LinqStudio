using LinqStudio.Blazor.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace LinqStudio.Blazor.Services;

public class ErrorHandlingService(IDialogService dialogService, ILogger<ErrorHandlingService> logger)
{
	private readonly IDialogService _dialogService = dialogService;
	private readonly ILogger<ErrorHandlingService> _logger = logger;

	/// <summary>
	/// Handles an exception by logging it and showing an error dialog.
	/// </summary>
	/// <param name="exception">The exception that occurred.</param>
	/// <param name="customMessage">Optional custom message to display instead of the exception message.</param>
	public async Task HandleErrorAsync(Exception exception, string? customMessage = null)
	{
		_logger.LogError(exception, "An error occurred: {Message}", customMessage ?? exception.Message);

		var parameters = new DialogParameters
		{
			{ nameof(ErrorDialog.Message), customMessage ?? exception.Message },
			{ nameof(ErrorDialog.StackTrace), exception.ToString() }
		};

		var options = new DialogOptions
		{
			CloseOnEscapeKey = true,
			MaxWidth = MaxWidth.Medium,
			FullWidth = true
		};

		await _dialogService.ShowAsync<ErrorDialog>("Error", parameters, options);
	}
}
