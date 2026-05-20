using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Wiki;

namespace Qaly.Application.Common.Interfaces;

public interface IWikiService
{
    Task<Result<IEnumerable<WikiPageDto>>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<WikiPageDto>> CreateAsync(Guid projectId, CreateWikiPageDto dto, CancellationToken ct = default);
    Task<Result<WikiPageDto>> UpdateAsync(Guid projectId, Guid id, UpdateWikiPageDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default);
}
