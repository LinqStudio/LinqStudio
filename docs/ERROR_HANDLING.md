# Error Handling in LinqStudio

This document describes the error handling mechanism implemented in LinqStudio for displaying user-friendly error messages in the UI.

## Overview

LinqStudio provides a comprehensive error handling solution that includes:
- A centralized error handling service that displays errors in clean, professional MudBlazor dialogs
- An error boundary component that catches unhandled exceptions globally
- Automatic logging of all errors

## Components

### ErrorHandlingService

Located at: `src/LinqStudio.Blazor/Services/ErrorHandlingService.cs`

The `ErrorHandlingService` is a scoped service that provides methods to handle exceptions and display error dialogs.

**Key Method:**
```csharp
Task HandleErrorAsync(Exception exception, string? customMessage = null)
```

- `exception`: The exception that occurred
- `customMessage`: Optional custom user-friendly message to display instead of the exception message

### ErrorDialog Component

Located at: `src/LinqStudio.Blazor/Components/ErrorDialog.razor`

A MudBlazor dialog component that displays:
- An error icon and title
- The error message in a prominent alert box
- An expandable "Technical Details" section with the full exception details and stack trace
- A "Close" button to dismiss the dialog

### AppErrorBoundary Component

Located at: `src/LinqStudio.Blazor/Components/AppErrorBoundary.razor`

A custom error boundary component that:
- Catches unhandled exceptions globally in the application
- Automatically displays the error dialog using `ErrorHandlingService`
- Shows a fallback error alert in case the error dialog fails to display
- Logs all unhandled exceptions

The error boundary is automatically applied to the entire application via the `Routes.razor` component.

## Usage

### Basic Usage

1. **Inject the service** into your Razor component or page:

```csharp
@inject ErrorHandlingService ErrorHandlingService
```

2. **Handle exceptions** in your code:

```csharp
try
{
    // Your code that might throw an exception
    await SomeOperationAsync();
}
catch (Exception ex)
{
    // Show error dialog with the exception message
    await ErrorHandlingService.HandleErrorAsync(ex);
}
```

### Custom Error Message

You can provide a custom, user-friendly message while still logging the technical exception:

```csharp
try
{
    // Your code that might throw an exception
    await SaveDataAsync();
}
catch (Exception ex)
{
    // Show custom message to user, technical details in expandable section
    await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save your data. Please try again.");
}
```

## Examples

### Example 1: Settings Page

The Settings page uses the error handling service to display errors when saving settings:

```csharp
try
{
    await SettingsService.Save(settingsSections);
}
catch (Exception ex)
{
    await ErrorHandlingService.HandleErrorAsync(ex, SharedResource.SettingsPage_Error_ErrorSavingSetting);
}
```

### Example 2: Unhandled Exceptions

Unhandled exceptions are automatically caught by the `AppErrorBoundary` component:

```csharp
// No try-catch needed - unhandled exceptions are automatically caught
// and displayed using the error dialog
private void SomeMethod()
{
    throw new InvalidOperationException("This will be caught by AppErrorBoundary");
}
```

## Registration

The service is automatically registered in the DI container when you call `AddLinqStudioBlazor()` in your `Program.cs`:

```csharp
services.AddLinqStudioBlazor();
```

This registers:
- `ErrorHandlingService` as a scoped service
- MudBlazor services (including `IDialogService` used by the error handling service)

The error boundary is automatically applied in `Routes.razor`:
```csharp
<AppErrorBoundary>
    <Router AppAssembly="..." NotFoundPage="...">
        ...
    </Router>
</AppErrorBoundary>
```

## Features

✅ **User-Friendly Error Messages**: Display clear, concise error messages to users
✅ **Technical Details**: Expandable section with full exception details and stack trace for debugging
✅ **Automatic Error Catching**: AppErrorBoundary catches all unhandled exceptions globally
✅ **Logging**: Automatically logs all errors using `ILogger<ErrorHandlingService>`
✅ **MudBlazor Integration**: Uses MudBlazor's dialog system for consistent UI
✅ **Customizable**: Supports custom error messages while preserving technical details
✅ **Keyboard Accessible**: Dialog can be closed with the Escape key
✅ **Fallback UI**: Shows error alert if dialog fails to display

## Dialog Appearance

The error dialog features:
- **Error Icon**: Red error icon for visual indication
- **Error Title**: "Error" heading
- **Message Alert**: The error message in a bordered error alert
- **Technical Details**: Collapsible expansion panel with stack trace
  - Monospace font for readability
  - Scrollable content area (max height: 400px)
  - Word wrapping for long lines
- **Close Button**: Primary button to dismiss the dialog

## Testing

### Unit Tests

Unit tests are available in `tests/LinqStudio.Blazor.Tests/ErrorHandlingServiceTests.cs`:

```bash
dotnet test tests/LinqStudio.Blazor.Tests/LinqStudio.Blazor.Tests.csproj
```

The unit tests verify:
- Service creation and dependency injection
- Error handling without throwing exceptions
- Support for custom error messages

### E2E Tests

End-to-end tests are available in `tests/LinqStudio.App.WebServer.E2ETests/ErrorHandlingE2ETests.cs`:

```bash
dotnet test tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj
```

The E2E tests verify:
- Error boundary catches unhandled exceptions
- Error dialogs are displayed correctly
- Error handling service is available in all pages
- Error dialogs can be closed properly

## Best Practices

1. **Always catch and handle exceptions** in user-facing operations
2. **Provide user-friendly messages** when the technical exception message is not clear
3. **Use the service consistently** throughout the application for uniform error handling
4. **Log exceptions** - The service automatically logs all errors
5. **Don't expose sensitive information** in custom error messages
6. **Let the error boundary handle unexpected errors** - No need to add try-catch everywhere

## Architecture

The error handling system is designed with multiple layers:

1. **ErrorHandlingService**: Manual error handling for expected exceptions
2. **AppErrorBoundary**: Automatic catching of unhandled exceptions
3. **ErrorDialog**: Consistent UI for all error displays
4. **Logging**: All errors are automatically logged for diagnostics

This ensures that no errors slip through unnoticed and all errors are presented consistently to users.

## Future Enhancements

Potential improvements:
- Add support for different severity levels (Warning, Info, Success)
- Support for snackbar notifications for non-critical errors
- Configurable dialog options (size, close behavior, etc.)
- Error reporting integration
