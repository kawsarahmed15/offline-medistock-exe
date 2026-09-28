using System;
using Medistock.Infrastructure.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Medistock.Infrastructure.Identity;

public static class DependencyInjection
{
    /// <summary>
    /// Registers identity/activation services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="activationServerBaseUri">
    /// Base URL of the Medistock CloudApi server. 
    /// Example: https://api.medistock.in or http://localhost:5000 for development.
    /// </param>
    public static IServiceCollection AddInfrastructureIdentity(
        this IServiceCollection services,
        Uri? activationServerBaseUri = null)
    {
        var baseUri = activationServerBaseUri
            ?? new Uri("http://localhost:5000"); // Default to local dev server

        services.AddHttpClient<IActivationService, ActivationService>(client =>
        {
            client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Medistock-Desktop/1.0");
        });

        return services;
    }
}
