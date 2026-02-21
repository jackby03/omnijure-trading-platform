using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Omnijure.Core.Shared.Infrastructure.Security;
using Omnijure.Core.Features.Settings;
using Omnijure.Core.Features.Settings.Api;
using Omnijure.Core.Features.Settings.Model;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.App;

public static class ApplicationBootstrapper
{
    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICryptographyService, WindowsDpapiCryptographyService>();
        services.AddSettingsFeature();
        services.AddSingleton<IExchangeClientFactory, BinanceClientFactory>();
        services.AddSingleton<Omnijure.Core.Shared.Infrastructure.EventBus.IEventBus, Omnijure.Core.Shared.Infrastructure.EventBus.EventBus>();

        // UI Components
        services.AddSingleton<Omnijure.Visual.Rendering.PanelSystem>();
        services.AddSingleton<Omnijure.Visual.Rendering.PanelSystemRenderer>();
        services.AddSingleton<SidebarRenderer>();
        // Renderers: Dynamically discover and register all UI Panels
        var panelRendererType = typeof(Omnijure.Visual.Widgets.Panels.IPanelRenderer);
        var panelTypes = typeof(ApplicationBootstrapper).Assembly.GetTypes()
            .Where(p => panelRendererType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

        foreach (var type in panelTypes)
        {
            services.AddSingleton(panelRendererType, type);
        }
        
        services.AddSingleton<PanelContentRenderer>();
        services.AddSingleton<LayoutManager>();

        return services.BuildServiceProvider();
    }

    public static ChartTabManager InitializeState(ServiceProvider provider, LayoutManager layout)
    {
        var settings = provider.GetRequiredService<ISettingsProvider>();
        settings.Load();

        var exchangeFactory = provider.GetRequiredService<IExchangeClientFactory>();
        var chartTabs = new ChartTabManager(exchangeFactory);
        layout.SetChartTabs(chartTabs);

        // Apply layout from settings
        if (settings.Current.Layout.Panels.Count > 0)
        {
            layout.ImportLayout(settings.Current.Layout.Panels);
            layout.ImportActiveTabs(
                settings.Current.Layout.ActiveBottomTab,
                settings.Current.Layout.ActiveLeftTab,
                settings.Current.Layout.ActiveRightTab,
                settings.Current.Layout.ActiveCenterTab);
        }

        // Restore tabs from settings or create default
        if (settings.Current.Chart.Tabs.Count > 0)
        {
            foreach (var saved in settings.Current.Chart.Tabs)
            {
                var tab = chartTabs.AddTab(saved.Symbol, saved.Timeframe);
                if (Enum.TryParse<Omnijure.Visual.Rendering.ChartType>(saved.ChartType, out var ct))
                    tab.ChartType = ct;
                tab.Zoom = saved.Zoom;
            }
            chartTabs.SwitchTo(Math.Clamp(settings.Current.Chart.ActiveTabIndex, 0, chartTabs.Count - 1));
        }
        else
        {
            chartTabs.AddTab(settings.Current.Chart.DefaultSymbol, settings.Current.Chart.DefaultTimeframe);
        }

        return chartTabs;
    }
}
