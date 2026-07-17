using System;
using System.Collections.Generic;
using System.Linq;

namespace Qaly.Infrastructure.Services.AI.Providers;

public class AiProviderFactory
{
    private readonly IEnumerable<IAiProvider> _providers;

    public AiProviderFactory(IEnumerable<IAiProvider> providers)
    {
        _providers = providers;
    }

    public IAiProvider GetProvider(string providerName)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            throw new ArgumentException($"AI Provider '{providerName}' is not supported.");
        }
        return provider;
    }

    public bool TryGetProvider(string providerName, out IAiProvider? provider)
    {
        provider = _providers.FirstOrDefault(candidate =>
            string.Equals(candidate.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        return provider != null;
    }
}
