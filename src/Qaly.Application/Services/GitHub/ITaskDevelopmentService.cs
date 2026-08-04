using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.GitHub;

namespace Qaly.Application.Services.GitHub;

public interface ITaskDevelopmentService
{
    Task<Result<TaskDevelopmentDto>> GetAsync(Guid taskId, CancellationToken ct = default);
}
