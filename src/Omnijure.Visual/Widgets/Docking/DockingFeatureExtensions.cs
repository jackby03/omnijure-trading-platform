using Microsoft.Extensions.DependencyInjection;
using Omnijure.Visual.Rendering;
using Omnijure.Visual.Widgets.Docking.Api;

namespace Omnijure.Visual.Widgets.Docking;

public static class DockingFeatureExtensions
{
    public static IServiceCollection AddDockingFeature(this IServiceCollection services)
    {
        services.AddSingleton<IDockingManager, DockingManager>();
        services.AddSingleton<PanelChromeRenderer>();
        services.AddSingleton<DockingRenderer>();
        return services;
    }
}
