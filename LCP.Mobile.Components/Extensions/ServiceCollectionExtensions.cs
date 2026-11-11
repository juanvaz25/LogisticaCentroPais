using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using LCP.Mobile.Components.Services;

namespace LCP.Mobile.Components.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLcpMobileComponents(this IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();
        services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
        services.AddScoped<CustomAuthStateProvider>(sp => (CustomAuthStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());
        services.AddScoped<IMobileApiClient, MobileApiClient>();
        services.AddSingleton<UiNotificationService>();

        return services;
    }
}
