using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class EmailDigestWorkerTests
{
    [Fact]
    public async Task DueDigest_FailureRetriesSameDeliveryKey_ThenDeliversExactlyOnce()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User
        {
            FullName = "Digest owner",
            Email = "digest@qaly.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        };
        var project = new Project
        {
            Name = "Digest project",
            Code = "DIGEST",
            OwnerId = user.Id,
            Status = "Active"
        };
        var subscription = new ProjectDigestSubscription
        {
            UserId = user.Id,
            ProjectId = project.Id,
            IsEnabled = true,
            DayOfWeek = (int)DateTimeOffset.UtcNow.DayOfWeek,
            LocalTimeMinutes = 540,
            TimeZoneId = "Asia/Saigon",
            NextDeliveryAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            LastDeliveryStatus = "scheduled"
        };
        var assignedTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = user.Id,
            AssigneeId = user.Id,
            Title = "Canonical digest task",
            Status = "Todo"
        };
        var projectTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = user.Id,
            Title = "Manager-visible project task",
            Status = "InProgress"
        };
        db.AddRange(user, project, subscription, assignedTask, projectTask);
        await db.SaveChangesAsync();

        var email = new FailOnceEmailService();
        var authorization = new StubAiNativeAuthorizationService();
        var services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IEmailService>(email)
            .AddSingleton<IAiNativeAuthorizationService>(authorization)
            .BuildServiceProvider();
        var worker = new EmailDigestWorker(
            NullLogger<EmailDigestWorker>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        await worker.SendDigestsAsync(CancellationToken.None);
        subscription.LastDeliveryStatus.Should().Be("retry");
        subscription.ConsecutiveFailureCount.Should().Be(1);
        subscription.LastDeliveryKey.Should().NotBeNullOrWhiteSpace();
        var stableKey = subscription.LastDeliveryKey;

        subscription.NextDeliveryAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        await worker.SendDigestsAsync(CancellationToken.None);

        subscription.LastDeliveryStatus.Should().Be("delivered");
        subscription.ConsecutiveFailureCount.Should().Be(0);
        subscription.LastDeliveryKey.Should().Be(stableKey);
        subscription.LastDeliveryAt.Should().NotBeNull();
        email.Attempts.Should().Be(2);
        email.Deliveries.Should().Be(1);
        email.Bodies.Should().OnlyContain(body => body.Contains(stableKey!, StringComparison.Ordinal));
        email.Bodies.Should().OnlyContain(body =>
            body.Contains("Manager-visible project task", StringComparison.Ordinal) &&
            body.Contains($"/projects/{project.Id}/tasks/{projectTask.Id}", StringComparison.Ordinal));

        await worker.SendDigestsAsync(CancellationToken.None);
        email.Attempts.Should().Be(2, "a delivered schedule must not be claimed twice");
        email.Deliveries.Should().Be(1);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DueDigest_AccessRevokedOrUserInactive_DisablesScheduleWithoutSending(
        bool userIsActive,
        bool projectCanRead)
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User
        {
            FullName = "Former member",
            Email = "former@qaly.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = userIsActive
        };
        var project = new Project
        {
            Name = "Restricted digest project",
            Code = "REVOKED",
            OwnerId = Guid.NewGuid(),
            Status = "Active"
        };
        var subscription = new ProjectDigestSubscription
        {
            UserId = user.Id,
            ProjectId = project.Id,
            IsEnabled = true,
            NextDeliveryAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            LastDeliveryStatus = "scheduled"
        };
        db.AddRange(user, project, subscription);
        await db.SaveChangesAsync();

        var email = new FailOnceEmailService();
        var authorization = new StubAiNativeAuthorizationService
        {
            ProjectAuthorization = projectCanRead
                ? new AiNativeProjectAuthorization(true, true, true, AiCapabilityTier.Full)
                : new AiNativeProjectAuthorization(false, false, false, AiCapabilityTier.None)
        };
        var services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IEmailService>(email)
            .AddSingleton<IAiNativeAuthorizationService>(authorization)
            .BuildServiceProvider();
        var worker = new EmailDigestWorker(
            NullLogger<EmailDigestWorker>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        await worker.SendDigestsAsync(CancellationToken.None);

        subscription.IsEnabled.Should().BeFalse();
        subscription.NextDeliveryAt.Should().BeNull();
        subscription.LastDeliveryStatus.Should().Be("access_revoked");
        email.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task DueDigest_ProviderCancellationWithoutHostShutdown_IsScheduledForRetry()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User
        {
            FullName = "Digest timeout owner",
            Email = "digest-timeout@qaly.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        };
        var project = new Project
        {
            Name = "Digest timeout project",
            Code = "DIGEST-TIMEOUT",
            OwnerId = user.Id,
            Status = "Active"
        };
        var subscription = new ProjectDigestSubscription
        {
            UserId = user.Id,
            ProjectId = project.Id,
            IsEnabled = true,
            NextDeliveryAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            LastDeliveryStatus = "scheduled"
        };
        db.AddRange(user, project, subscription);
        await db.SaveChangesAsync();
        var email = new ProviderCancellationEmailService();
        var services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IEmailService>(email)
            .AddSingleton<IAiNativeAuthorizationService>(new StubAiNativeAuthorizationService())
            .BuildServiceProvider();
        var worker = new EmailDigestWorker(
            NullLogger<EmailDigestWorker>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        await worker.SendDigestsAsync(CancellationToken.None);

        subscription.LastDeliveryStatus.Should().Be("retry");
        subscription.ConsecutiveFailureCount.Should().Be(1);
        subscription.LastError.Should().Contain("provider timeout");
        subscription.NextDeliveryAt.Should().BeAfter(DateTimeOffset.UtcNow);
        email.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task DueDigest_ProjectManager_DoesNotReceiveUnrelatedPrivateTask()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var manager = new User
        {
            FullName = "Project manager",
            Email = "manager@qaly.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        };
        var project = new Project
        {
            Name = "Private digest boundary",
            Code = "DIGEST-PRIVATE",
            OwnerId = Guid.NewGuid(),
            Status = "Active"
        };
        var subscription = new ProjectDigestSubscription
        {
            UserId = manager.Id,
            ProjectId = project.Id,
            IsEnabled = true,
            NextDeliveryAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            LastDeliveryStatus = "scheduled"
        };
        var publicTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = project.OwnerId,
            Title = "Visible public task",
            Status = "Todo"
        };
        var unrelatedPrivateTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = project.OwnerId,
            Title = "Secret private task",
            Status = "Todo",
            IsPrivate = true
        };
        var assignedPrivateTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = project.OwnerId,
            AssigneeId = manager.Id,
            Title = "Assigned private task",
            Status = "Todo",
            IsPrivate = true
        };
        db.AddRange(manager, project, subscription, publicTask, unrelatedPrivateTask, assignedPrivateTask);
        await db.SaveChangesAsync();

        var email = new CapturingEmailService();
        var authorization = new StubAiNativeAuthorizationService();
        var services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IEmailService>(email)
            .AddSingleton<IAiNativeAuthorizationService>(authorization)
            .BuildServiceProvider();
        var worker = new EmailDigestWorker(
            NullLogger<EmailDigestWorker>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        await worker.SendDigestsAsync(CancellationToken.None);

        email.Bodies.Should().ContainSingle();
        email.Bodies[0].Should().Contain("Visible public task");
        email.Bodies[0].Should().Contain("Assigned private task");
        email.Bodies[0].Should().NotContain("Secret private task");
    }

    private sealed class FailOnceEmailService : IEmailService
    {
        public int Attempts { get; private set; }
        public int Deliveries { get; private set; }
        public List<string> Bodies { get; } = [];

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            Attempts++;
            Bodies.Add(htmlBody);
            if (Attempts == 1) throw new InvalidOperationException("simulated provider timeout");
            Deliveries++;
            return Task.CompletedTask;
        }

        public Task SendTaskAssignmentNotificationAsync(string recipientEmail, string taskTitle, string projectName)
            => Task.CompletedTask;

        public Task SendDueDateReminderAsync(string recipientEmail, string taskTitle, DateTimeOffset dueDate)
            => Task.CompletedTask;
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public List<string> Bodies { get; } = [];

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            Bodies.Add(htmlBody);
            return Task.CompletedTask;
        }

        public Task SendTaskAssignmentNotificationAsync(string recipientEmail, string taskTitle, string projectName)
            => Task.CompletedTask;

        public Task SendDueDateReminderAsync(string recipientEmail, string taskTitle, DateTimeOffset dueDate)
            => Task.CompletedTask;
    }

    private sealed class ProviderCancellationEmailService : IEmailService
    {
        public int Attempts { get; private set; }

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            Attempts++;
            throw new TaskCanceledException("provider timeout");
        }

        public Task SendTaskAssignmentNotificationAsync(string recipientEmail, string taskTitle, string projectName)
            => Task.CompletedTask;

        public Task SendDueDateReminderAsync(string recipientEmail, string taskTitle, DateTimeOffset dueDate)
            => Task.CompletedTask;
    }

    private sealed class StubAiNativeAuthorizationService : IAiNativeAuthorizationService
    {
        public AiNativeSystemTier SystemTier { get; init; } = AiNativeSystemTier.Full;
        public AiNativeProjectAuthorization ProjectAuthorization { get; init; }
            = new(true, true, true, AiCapabilityTier.Full);

        public Task<AiNativeSystemTier> ResolveSystemTierAsync(
            Guid userId,
            string? systemRole,
            CancellationToken ct = default)
            => Task.FromResult(SystemTier);

        public Task<AiNativeProjectAuthorization> ResolveProjectAsync(
            Project project,
            Guid userId,
            bool isSystemAdmin,
            CancellationToken ct = default)
            => Task.FromResult(ProjectAuthorization);
    }
}
