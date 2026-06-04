using System.Threading;
using System.Threading.Tasks;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI.Providers;

public interface IAiProvider
{
    string ProviderName { get; }
    Task<AiResponse> CompleteAsync(AiRequest request, AiProviderSetting config, CancellationToken cancellationToken = default);
}
