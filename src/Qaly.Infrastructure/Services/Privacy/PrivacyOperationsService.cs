using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Privacy;
using Qaly.Application.Services;
using Qaly.Domain.Interfaces;

namespace Qaly.Infrastructure.Services.Privacy;

public sealed class PrivacyOperationsService : IPrivacyOperationsService
{
    private readonly IPrivacyWorkStore _store;
    private readonly IPrivacyWorkProcessor _processor;
    private readonly ICurrentUserService _currentUser;
    private readonly PrivacyV4Options _options;

    public PrivacyOperationsService(
        IPrivacyWorkStore store,
        IPrivacyWorkProcessor processor,
        ICurrentUserService currentUser,
        IOptions<PrivacyV4Options> options)
    {
        _store = store;
        _processor = processor;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<Result<PrivacyWorkerRunResultDto>> RunNextAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue || !ProjectRoleRules.IsSystemAdmin(_currentUser.Role))
        {
            return Result.Forbidden<PrivacyWorkerRunResultDto>();
        }

        if (!_options.Enabled)
        {
            return Result.Failure<PrivacyWorkerRunResultDto>(
                "Privacy v4 is disabled.",
                503,
                PrivacyErrorCodes.Disabled);
        }

        var workerId = $"manual:{_currentUser.UserId.Value:N}:{Guid.NewGuid():N}";
        var leaseDuration = TimeSpan.FromSeconds(Math.Max(10, _options.LeaseSeconds));
        var lease = await _store.ClaimNextAsync(workerId, leaseDuration, ct);
        if (lease == null)
        {
            return Result.Success(new PrivacyWorkerRunResultDto(false, null, null, "empty"));
        }

        try
        {
            await _processor.ProcessAsync(lease, workerId, ct);
            return Result.Success(new PrivacyWorkerRunResultDto(true, lease.Kind, lease.WorkId, "processed"));
        }
        catch (Exception ex)
        {
            await _store.AbandonLeaseAsync(
                lease,
                workerId,
                PrivacyErrorCodes.WorkerUnavailable,
                ex.Message,
                ct);
            return Result.Failure<PrivacyWorkerRunResultDto>(
                "Privacy work failed and was scheduled according to retry policy.",
                503,
                PrivacyErrorCodes.WorkerUnavailable);
        }
    }
}
