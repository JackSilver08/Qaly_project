using FluentAssertions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Webhook;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.UnitTests;

public sealed class WebhookServiceTests
{
    [Theory]
    [InlineData("webhook-secret", true)]
    [InlineData("", false)]
    public async Task CreateAsync_ReportsSecretPresenceWithoutReturningTheSecret(
        string secret,
        bool expectedHasSecret)
    {
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var webhookRepository = new Mock<IRepository<WebhookSubscription>>();
        var projectRepository = new Mock<IRepository<Project>>();
        var accessPolicy = new Mock<ITaskAccessPolicy>();
        var unitOfWork = new Mock<IUnitOfWork>();

        projectRepository
            .Setup(repository => repository.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = projectId, OwnerId = ownerId, Name = "Webhook project" });
        accessPolicy
            .Setup(policy => policy.CanManageWebhooksAsync(projectId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        webhookRepository
            .Setup(repository => repository.AddAsync(It.IsAny<WebhookSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookSubscription webhook, CancellationToken _) => webhook);
        unitOfWork
            .Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new WebhookService(
            webhookRepository.Object,
            projectRepository.Object,
            Mock.Of<ICurrentUserService>(),
            accessPolicy.Object,
            unitOfWork.Object,
            Mock.Of<IWebhookPublisher>(),
            AllowedEndpointPolicy());

        var result = await service.CreateAsync(new CreateWebhookDto(
            projectId,
            "https://example.test/qaly-webhook",
            secret,
            ["task.created"]));

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.HasSecret.Should().Be(expectedHasSecret);
        result.Data!.NotExposeSecretValue(secret);
    }

    private static IWebhookEndpointPolicy AllowedEndpointPolicy()
    {
        var policy = new Mock<IWebhookEndpointPolicy>();
        policy.Setup(item => item.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
                WebhookEndpointValidation.Allowed(new Uri(url)));
        return policy.Object;
    }
}

internal static class WebhookDtoAssertions
{
    public static void NotExposeSecretValue(this WebhookDto dto, string secret)
    {
        dto.GetType().GetProperties()
            .Select(property => property.GetValue(dto)?.ToString())
            .Should().NotContain(secret, "webhook DTOs must never expose the configured secret");
    }
}
