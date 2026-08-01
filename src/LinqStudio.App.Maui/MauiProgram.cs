using LinqStudio.Blazor.Extensions;
using LinqStudio.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinqStudio.App.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		builder.Configuration.AddJsonFile(
			LinqStudio.Core.Services.SettingsService.FILE_NAME,
			optional: true,
			reloadOnChange: true);

		var basePath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			"LinqStudio",
			"Projects");

		builder.Services
			.AddLinqStudio()
			.AddFileSystemRepositories(basePath)
			.AddLinqStudioBlazor();

		return builder.Build();
	}
}
