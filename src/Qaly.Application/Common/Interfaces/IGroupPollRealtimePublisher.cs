using Qaly.Application.DTOs.Groups;

namespace Qaly.Application.Common.Interfaces;

public interface IGroupPollRealtimePublisher
{
    Task PublishPollUpdatedAsync(Guid groupId, Guid pollId, GroupPollResultsDto results, DateTimeOffset updatedAt, CancellationToken ct = default);
}

public sealed class NullGroupPollRealtimePublisher : IGroupPollRealtimePublisher
{
    public Task PublishPollUpdatedAsync(Guid groupId, Guid pollId, GroupPollResultsDto results, DateTimeOffset updatedAt, CancellationToken ct = default)
        => Task.CompletedTask;
}
