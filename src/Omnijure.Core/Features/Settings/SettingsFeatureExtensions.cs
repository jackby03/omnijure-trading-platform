using Microsoft.Extensions.DependencyInjection;
using Omnijure.Core.Features.Settings.Api;

namespace Omnijure.Core.Features.Settings;

public static class SettingsFeatureExtensions
{
    public static IServiceCollection AddSettingsFeature(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsProvider, SettingsManager>();
        return services;
    }
}
