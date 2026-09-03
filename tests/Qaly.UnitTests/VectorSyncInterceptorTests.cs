using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data.Interceptors;

namespace Qaly.UnitTests;

public sealed class VectorSyncInterceptorTests
{
    public static TheoryData<BaseEntity, EntityState, string, string> Contracts => new()
    {
        { new Project(), EntityState.Added, VectorSyncEventTypes.ProjectCreated, VectorSyncAggregateTypes.Project },
        { new Project(), EntityState.Modified, VectorSyncEventTypes.ProjectUpdated, VectorSyncAggregateTypes.Project },
        { new Project(), EntityState.Deleted, VectorSyncEventTypes.ProjectDeleted, VectorSyncAggregateTypes.Project },
        { new TaskItem(), EntityState.Added, VectorSyncEventTypes.TaskCreated, VectorSyncAggregateTypes.Task },
        { new TaskItem(), EntityState.Modified, VectorSyncEventTypes.TaskUpdated, VectorSyncAggregateTypes.Task },
        { new TaskItem(), EntityState.Deleted, VectorSyncEventTypes.TaskDeleted, VectorSyncAggregateTypes.Task },
        { new TaskComment(), EntityState.Added, VectorSyncEventTypes.CommentAdded, VectorSyncAggregateTypes.Comment },
        { new TaskComment(), EntityState.Modified, VectorSyncEventTypes.CommentUpdated, VectorSyncAggregateTypes.Comment },
        { new TaskComment(), EntityState.Deleted, VectorSyncEventTypes.CommentDeleted, VectorSyncAggregateTypes.Comment },
        { new TaskAttachment(), EntityState.Added, VectorSyncEventTypes.TaskAttachmentCreated, VectorSyncAggregateTypes.Attachment },
        { new TaskAttachment(), EntityState.Modified, VectorSyncEventTypes.TaskAttachmentUpdated, VectorSyncAggregateTypes.Attachment },
        { new TaskAttachment(), EntityState.Deleted, VectorSyncEventTypes.TaskAttachmentDeleted, VectorSyncAggregateTypes.Attachment }
    };

    [Theory]
    [MemberData(nameof(Contracts))]
    public void ResolveContract_UsesTheSameCanonicalNamesAsWorker(
        BaseEntity entity,
        EntityState state,
        string expectedEventType,
        string expectedAggregateType)
    {
        var contract = VectorSyncInterceptor.ResolveContract(entity, state);

        contract.Should().Be((expectedEventType, expectedAggregateType));
        VectorSyncEventTypes.Supported.Should().Contain(expectedEventType);
    }
}
