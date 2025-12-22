using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Idendity.Infrastructure.Extensions;

public static class KeyVaultExtensions
{
    /// <summary>
    /// Adds Azure Key Vault configuration provider using Managed Identity
    /// </summary>
    public static IConfigurationBuilder AddAzureKeyVaultIfConfigured(
        this IConfigurationBuilder builder)
    {
        var builtConfig = builder.Build();
        var keyVaultUri = builtConfig["KeyVault:Uri"];

        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            // Use DefaultAzureCredential which works with Managed Identity in Azure
            // and falls back to other authentication methods locally (VS, Azure CLI, etc.)
            builder.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeSharedTokenCacheCredential = true
                }));
        }

        return builder;
    }
}

