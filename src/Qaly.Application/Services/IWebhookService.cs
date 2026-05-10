using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Webhook;

namespace Qaly.Application.Services;

public interface IWebhookService
{
    Task<Result<IEnumerable<WebhookDto>>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<WebhookDto>> CreateAsync(CreateWebhookDto dto, CancellationToken ct = default);
    Task<Result<WebhookDto>> UpdateAsync(Guid id, UpdateWebhookDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result> TriggerTestAsync(Guid id, CancellationToken ct = default);
}
