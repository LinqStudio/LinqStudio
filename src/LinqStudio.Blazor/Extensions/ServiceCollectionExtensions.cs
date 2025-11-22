using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace LinqStudio.Blazor.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLinqStudio(this IServiceCollection services)
    {
        services.AddMudServices();

        return services;
    }
}
