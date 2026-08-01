using LinqStudio.Abstractions;
using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;
using LinqStudio.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace LinqStudio.Core.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddLinqStudio(this IServiceCollection services)
	{
		services.AddSingleton<ProjectVersionConfig>();
		services.AddSingleton<ProjectService>();
		services.AddSingleton<QueryService>();

		AddLinqStudioOptions(services);

		services.AddSingleton<ISettingsService, SettingsService>();

		// Register the shared Roslyn workspace service (before services that depend on it)
		services.AddSingleton<RoslynWorkspaceService>();

		// register the CompilerServiceFactory so Blazor components can create CompilerService instances
		services.AddScoped<ICompilerServiceFactory, CompilerServiceFactory>();

		services.AddScoped<IDbContextGenerator, DbContextGenerator>();

		services.AddScoped<IQueryExecutionService, QueryExecutionService>();

		return services;
	}

	private static void AddLinqStudioOptions(IServiceCollection services)
	{
		foreach (var settingType in typeof(ServiceCollectionExtensions).Assembly
			.GetTypes()
			.Where(x => typeof(IUserSettingsSection).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract))
		{
			typeof(ServiceCollectionExtensions)
				.GetMethod(nameof(AddOptions), BindingFlags.NonPublic | BindingFlags.Static)!
				.MakeGenericMethod(settingType)
				.Invoke(null, [services]);
		}
	}

	public static IServiceCollection AddFileSystemRepositories(this IServiceCollection services, string basePath)
	{
		services.AddSingleton(new FileSystemStorageOptions { BasePath = basePath });
		services.AddScoped<IProjectRepository, FileSystemProjectRepository>();
		services.AddScoped<IQueryRepository, FileSystemQueryRepository>();
		return services;
	}

	/// <summary>
	/// Generic AddOptions for a given IUserSettingsSection.
	/// This is needed to be able to use reflection and call the generic method with the correct type,
	/// since service.AddOptions doesn't have an overload that takes a "Type" parameter.
	/// </summary>
	private static void AddOptions<TSettings>(IServiceCollection services)
		where TSettings : class, IUserSettingsSection, new()
	{
		services
			.AddOptions<TSettings>()
			.BindConfiguration(new TSettings().SectionName);
	}
}
