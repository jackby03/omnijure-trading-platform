using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Omnijure.Core.Shared.Infrastructure.Security;
using Omnijure.Core.Features.Settings;
using Omnijure.Core.Features.Settings.Api;
using Omnijure.Core.Features.Settings.Model;
using Omnijure.Visual.Rendering;
using Omnijure.Visual.Widgets.Docking;
using Omnijure.Visual.Features.Charting;
using Omnijure.Visual.Features.Search;
using Omnijure.Visual.Features.Settings;
using Omnijure.Visual.Widgets.Toolbars;

namespace Omnijure.Visual.App;

public static class ApplicationBootstrapper
{
    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<ICryptographyService, WindowsDpapiCryptographyService>();
        services.AddSettingsFeature();
        services.AddSingleton<IEventBus, EventBus>();

        // Exchange
        services.AddSingleton<IExchangeClientFactory, BinanceClientFactory>();

        // Docking
        services.AddDockingFeature();

        // Toolbars & renderers
        services.AddSingleton<SidebarRenderer>();
        services.AddSingleton<LeftToolbarRenderer>();
        services.AddSingleton<StatusBarRenderer>();
        services.AddSingleton<SecondaryToolbarRenderer>();
        services.AddSingleton<ChartRenderer>();
        services.AddSingleton<ToolbarRenderer>();
        services.AddSingleton<SearchModalRenderer>();
        services.AddSingleton<SettingsModalRenderer>();
        services.AddSingleton<UiSettingsModal>();

        // Auto-discover and register all IPanelRenderer implementations
        var panelRendererType = typeof(Omnijure.Visual.Widgets.Panels.IPanelRenderer);
        var panelTypes = typeof(ApplicationBootstrapper).Assembly.GetTypes()
            .Where(p => panelRendererType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);
        foreach (var type in panelTypes)
            services.AddSingleton(panelRendererType, type);

        services.AddSingleton<PanelContentRenderer>();
        services.AddSingleton<ChartTabManager>();
        services.AddSingleton<LayoutManager>();

        return services.BuildServiceProvider();
    }

    public static ChartTabManager InitializeState(ServiceProvider provider, LayoutManager layout)
    {
        var settings = provider.GetRequiredService<ISettingsProvider>();
        settings.Load();

        var chartTabs = provider.GetRequiredService<ChartTabManager>();

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

        // Restore chart tabs from settings or create default
        if (settings.Current.Chart.Tabs.Count > 0)
        {
            foreach (var saved in settings.Current.Chart.Tabs)
            {
                var tab = chartTabs.AddTab(saved.Symbol, saved.Timeframe);
                if (Enum.TryParse<ChartType>(saved.ChartType, out var ct))
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
