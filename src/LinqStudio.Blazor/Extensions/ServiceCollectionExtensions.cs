using LinqStudio.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace LinqStudio.Blazor.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddLinqStudioBlazor(this IServiceCollection services)
	{
		services.AddMudServices();

		services.AddScoped<MonacoProvidersService>();
		services.AddScoped<ErrorHandlingService>();

		return services;
	}
}
