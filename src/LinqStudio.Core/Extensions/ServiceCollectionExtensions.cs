using LinqStudio.Core.Abstractions;
using LinqStudio.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LinqStudio.Core.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddLinqStudio(this IServiceCollection services)
	{
		AddAndBindOptions(services);

		return services;
	}

	private static void AddAndBindOptions(IServiceCollection services)
	{
		services.AddSingleton<SettingsService>();

		foreach (var settingType in typeof(ServiceCollectionExtensions).Assembly
			.GetTypes()
			.Where(x => typeof(IUserSettingsSection).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract))
		{
			typeof(ServiceCollectionExtensions).GetMethod(nameof(AddOptions), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
				.MakeGenericMethod(settingType)
				.Invoke(null, [services]);
		}
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
