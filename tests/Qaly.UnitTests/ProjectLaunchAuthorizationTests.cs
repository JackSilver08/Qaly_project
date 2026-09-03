using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class ProjectLaunchAuthorizationTests
{
    [Fact]
    public async Task AnalyzeAsync_WithoutOrganizationScope_DoesNotFallBackToAnotherTenant()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Organizations.Add(new Organization
        {
            Name = "Unrelated tenant",
            OwnerId = Guid.NewGuid(),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(service => service.Role).Returns("User");
        var service = new ProjectLaunchService(db, currentUser.Object, Mock.Of<IAiGateway>());
        var request = new AiAssistantTurnRequestDto("Khởi chạy dự án mới");
        var context = new AiAssistantExecutionContextDto([], [], []);

        var result = await service.AnalyzeAsync(request, context);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Brief.Should().BeNull();
        result.Data.Conversation.ConversationDisposition.Should().Be("clarification");
        result.Data.Conversation.Questions.Should().ContainSingle(question => question.Id == "launch.organization");
    }

    [Fact]
    public async Task AnalyzeAsync_WithUnrelatedOrganizationSource_ReturnsNotFoundWithoutProviderCall()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var organization = new Organization
        {
            Name = "Unrelated tenant",
            OwnerId = Guid.NewGuid(),
            IsActive = true
        };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(service => service.Role).Returns("User");
        var gateway = new Mock<IAiGateway>();
        var service = new ProjectLaunchService(db, currentUser.Object, gateway.Object);
        var source = new AiAssistantContextSourceEnvelopeDto(
            AiAssistantContextContract.OrganizationSummarySource,
            $"organization:{organization.Id:D}",
            "organization",
            organization.Name,
            DateTimeOffset.UtcNow,
            "canonical",
            "internal",
            "hash",
            new Dictionary<string, object?> { ["organizationId"] = organization.Id },
            [],
            "database");
        var request = new AiAssistantTurnRequestDto("Khởi chạy dự án mới");
        var context = new AiAssistantExecutionContextDto([], [source], []);

        var result = await service.AnalyzeAsync(request, context);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        gateway.Verify(
            item => item.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleReadableOrganizations_RequiresExplicitScope()
    {
        await using var db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var currentUserId = Guid.NewGuid();
        var currentUser = new User
        {
            Id = currentUserId,
            FullName = "Multi tenant user",
            Email = "multi-tenant@qaly.test",
            IsActive = true
        };
        var first = new Organization { Name = "First tenant", Code = "FIRST-TENANT", OwnerId = currentUserId, IsActive = true };
        var second = new Organization { Name = "Second tenant", Code = "SECOND-TENANT", OwnerId = currentUserId, IsActive = true };
        db.AddRange(currentUser, first, second);
        await db.SaveChangesAsync();

        var identity = new Mock<ICurrentUserService>();
        identity.SetupGet(service => service.UserId).Returns(currentUserId);
        identity.SetupGet(service => service.Role).Returns("User");
        var service = new ProjectLaunchService(db, identity.Object, Mock.Of<IAiGateway>());

        var result = await service.AnalyzeAsync(
            new AiAssistantTurnRequestDto("Khởi chạy dự án mới"),
            new AiAssistantExecutionContextDto([], [], []));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Brief.Should().BeNull();
        result.Data.Conversation.Questions.Should().ContainSingle(question => question.Id == "launch.organization");
    }
}
