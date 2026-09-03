using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class TaskAttentionSignalWorkerTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly QalyDbContext _db;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public TaskAttentionSignalWorkerTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var owner = new User
        {
            Id = _ownerId,
            FullName = "Attention owner",
            Email = "attention-owner@qaly.test",
            PasswordHash = "test"
        };
        _db.Users.Add(owner);
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Attention Project",
            Code = "ATTN",
            OwnerId = _ownerId,
            Owner = owner
        });
        _db.SaveChanges();

        _provider = new ServiceCollection()
            .AddSingleton(_db)
            .AddSingleton(_notifications.Object)
            .BuildServiceProvider();
    }

    [Fact]
    public async Task ScanAsync_ProcessesTasksBeyondFirstBatch()
    {
        _db.TaskItems.AddRange(Enumerable.Range(1, 501).Select(index => new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            ReporterId = _ownerId,
            AssigneeId = _ownerId,
            Title = $"Assigned task {index}",
            Status = "Todo",
            Priority = "Medium",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(index)
        }));
        await _db.SaveChangesAsync();

        await CreateWorker().ScanAsync(CancellationToken.None);

        (await _db.TaskAttentionSignals.CountAsync()).Should().Be(501);
        _notifications.Verify(service => service.CreateAsync(
            _ownerId,
            It.IsAny<string>(),
            "Unseen",
            It.IsAny<Guid?>(),
            nameof(TaskItem),
            It.IsAny<CancellationToken>()), Times.Exactly(501));
    }

    [Fact]
    public async Task ScanAsync_ResolvesSignalWhenTaskIsClosed()
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            ReporterId = _ownerId,
            AssigneeId = _ownerId,
            Title = "Completed assigned task",
            Status = "Done",
            Priority = "Medium"
        };
        var signal = new TaskAttentionSignal
        {
            TaskItemId = task.Id,
            TaskItem = task,
            UserId = _ownerId,
            SignalType = "Unseen",
            FirstDetectedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastSentAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _db.TaskAttentionSignals.Add(signal);
        await _db.SaveChangesAsync();

        await CreateWorker().ScanAsync(CancellationToken.None);

        (await _db.TaskAttentionSignals.SingleAsync(item => item.Id == signal.Id))
            .ResolvedAt.Should().NotBeNull();
        _notifications.VerifyNoOtherCalls();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private TaskAttentionSignalWorker CreateWorker()
        => new(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TaskAttentionSignalWorker>.Instance);
}
