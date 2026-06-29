namespace DepinTracker.Application;

using DepinTracker.Application.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Application layer's use-case services. The services are stateless
/// and resolve their data via repository ports, so they are safe as singletons.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<PriceEngine>();
        services.AddSingleton<ExchangeRateEngine>();
        services.AddSingleton<ValuationService>();
        services.AddSingleton<ProjectService>();
        services.AddSingleton<WalletService>();
        services.AddSingleton<RewardImportService>();
        services.AddSingleton<AnalyticsService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<TaxReportGenerator>();
        return services;
    }
}
