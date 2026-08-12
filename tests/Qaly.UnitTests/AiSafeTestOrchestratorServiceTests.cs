using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiSafeTestOrchestratorServiceTests
{
    [Fact]
    public async Task Prepare_Development_CreatesOneDurableReviewOnlyManifest()
    {
        var db = CreateContext();
        var user = new User { FullName = "Test Owner", Email = "safe-test@qaly.test", PasswordHash = "x", Role = "Admin" };
        var session = new AssistantSession { OwnerUserId = user.Id, Status = "active" };
        var turn = new AssistantTurn
        {
            SessionId = session.Id,
            Sequence = 1,
            ClientTurnId = Guid.NewGuid(),
            IdempotencyKey = "test-key",
            RequestHash = "hash",
            UserMessage = "Chạy test demo tất cả CAND",
            CorrelationId = "correlation"
        };
        db.AddRange(user, session, turn);
        await db.SaveChangesAsync();
        var service = CreateService(db, user.Id, Environments.Development, enabled: true);

        var first = await service.PrepareAsync(session.Id, turn.Id);
        var replay = await service.PrepareAsync(session.Id, turn.Id);

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        first.Data!.RunId.Should().Be(replay.Data!.RunId);
        first.Data.RequiresConfirmation.Should().BeTrue();
        first.Data.Suites.Should().HaveCount(3);
        (await db.AssistantTestRuns.CountAsync()).Should().Be(1);
        (await db.AssistantTestRuns.SingleAsync()).Status.Should().Be("review_required");
    }

    [Fact]
    public async Task Prepare_Production_IsPolicyBlockedBeforeAnyManifestOrProcess()
    {
        var db = CreateContext();
        var service = CreateService(db, Guid.NewGuid(), Environments.Production, enabled: true);

        var result = await service.PrepareAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorCode.Should().Be("policy_blocked");
        (await db.AssistantTestRuns.CountAsync()).Should().Be(0);
    }

    private static AiSafeTestOrchestratorService CreateService(
        QalyDbContext db,
        Guid userId,
        string environmentName,
        bool enabled)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(environmentName);
        environment.SetupGet(item => item.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        return new AiSafeTestOrchestratorService(
            db,
            currentUser.Object,
            environment.Object,
            Options.Create(new AiJobPlatformOptions { SafeTestOrchestratorEnabled = enabled }));
    }

    private static QalyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
