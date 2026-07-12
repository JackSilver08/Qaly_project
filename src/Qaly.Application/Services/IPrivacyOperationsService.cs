using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Privacy;

namespace Qaly.Application.Services;

public interface IPrivacyOperationsService
{
    Task<Result<PrivacyWorkerRunResultDto>> RunNextAsync(CancellationToken ct = default);
}
