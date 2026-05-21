using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Meeting;

namespace Qaly.Application.Services;

public interface IMeetingImportService
{
    Task<Result<MeetilyImportResult>> ImportMeetilyAsync(MeetilyImportRequest request, CancellationToken ct = default);
}
