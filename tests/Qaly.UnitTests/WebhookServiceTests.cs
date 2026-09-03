using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Webhook;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;
using Qaly.Infrastructure.Services;
using System.Net;

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
            Mock.Of<IRepository<WebhookOutboxMessage>>(),
            Mock.Of<IRepository<WebhookDeliveryLog>>(),
            projectRepository.Object,
            Mock.Of<ICurrentUserService>(),
            accessPolicy.Object,
            unitOfWork.Object,
            Mock.Of<IWebhookPublisher>(),
            AllowedEndpointPolicy(),
            Mock.Of<IAuditLogService>());

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

    [Fact]
    public async Task CreateAsync_RejectsEventThatThePlatformNeverPublishes()
    {
        var projectId = Guid.NewGuid();
        var repository = new Mock<IRepository<WebhookSubscription>>();
        var service = new WebhookService(
            repository.Object,
            Mock.Of<IRepository<WebhookOutboxMessage>>(),
            Mock.Of<IRepository<WebhookDeliveryLog>>(),
            Mock.Of<IRepository<Project>>(),
            Mock.Of<ICurrentUserService>(),
            Mock.Of<ITaskAccessPolicy>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IWebhookPublisher>(),
            AllowedEndpointPolicy(),
            Mock.Of<IAuditLogService>());

        var result = await service.CreateAsync(new CreateWebhookDto(
            projectId,
            "https://example.test/hook",
            string.Empty,
            ["comment.added"]));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("comment.added");
        repository.Verify(
            item => item.AddAsync(It.IsAny<WebhookSubscription>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(true, 200, 200)]
    [InlineData(false, 503, 502)]
    public async Task TriggerTestAsync_ReportsCanonicalDeliveryOutcome(
        bool delivered,
        int responseStatus,
        int expectedResultStatus)
    {
        var projectId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var projectRepository = new Mock<IRepository<Project>>();
        var webhookRepository = new Mock<IRepository<WebhookSubscription>>();
        var accessPolicy = new Mock<ITaskAccessPolicy>();
        var publisher = new Mock<IWebhookPublisher>();
        projectRepository.Setup(item => item.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = projectId, OwnerId = ownerId, Name = "Delivery" });
        webhookRepository.Setup(item => item.GetByIdAsync(webhookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookSubscription { Id = webhookId, ProjectId = projectId, PayloadUrl = "https://example.test/hook" });
        accessPolicy.Setup(item => item.CanManageWebhooksAsync(projectId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        publisher.Setup(item => item.DispatchToWebhookAsync(
                webhookId,
                "ping",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookDispatchReceipt(
                webhookId,
                "ping",
                "webhook:test",
                delivered ? "delivered" : "delivery_failed",
                delivered,
                delivered ? 1 : 3,
                responseStatus,
                Guid.NewGuid()));
        var service = new WebhookService(
            webhookRepository.Object,
            Mock.Of<IRepository<WebhookOutboxMessage>>(),
            Mock.Of<IRepository<WebhookDeliveryLog>>(),
            projectRepository.Object,
            Mock.Of<ICurrentUserService>(),
            accessPolicy.Object,
            Mock.Of<IUnitOfWork>(),
            publisher.Object,
            AllowedEndpointPolicy(),
            Mock.Of<IAuditLogService>());

        var result = await service.TriggerTestAsync(projectId, webhookId);

        result.StatusCode.Should().Be(expectedResultStatus);
        result.Data.Should().NotBeNull();
        result.Data!.IsDelivered.Should().Be(delivered);
        result.Data.ResponseStatusCode.Should().Be(responseStatus);
        result.Data.AttemptCount.Should().Be(delivered ? 1 : 3);
    }

    [Fact]
    public async Task Publisher_ReturnsSavedReceiptAndDeduplicatesConfirmedPayload()
    {
        var databaseName = $"webhook-receipt-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<QalyDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        await using var provider = services.BuildServiceProvider();
        var webhookId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var ownerId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = ownerId,
                Email = "webhook-owner@example.test",
                FullName = "Webhook owner",
                PasswordHash = "test-only"
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                OwnerId = ownerId,
                Name = "Webhook receipt project",
                Code = $"WH-{projectId:N}"
            });
            db.WebhookSubscriptions.Add(new WebhookSubscription
            {
                Id = webhookId,
                ProjectId = projectId,
                PayloadUrl = "https://example.test/hook",
                Secret = "secret",
                Events = "[\"task.created\"]"
            });
            await db.SaveChangesAsync();
        }

        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(item => item.CreateClient("WebhookClient"))
            .Returns(() => new HttpClient(new StaticResponseHandler(HttpStatusCode.OK)));
        var publisher = new WebhookPublisher(
            httpFactory.Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WebhookPublisher>.Instance,
            AllowedEndpointPolicy());

        var first = await publisher.DispatchToWebhookAsync(webhookId, "task.created", new { taskId = "T-1" });
        var duplicate = await publisher.DispatchToWebhookAsync(webhookId, "task.created", new { taskId = "T-1" });
        var firstOccurrenceId = Guid.NewGuid();
        var secondOccurrenceId = Guid.NewGuid();
        var firstOccurrence = await publisher.PublishOutboxAsync(
            firstOccurrenceId,
            projectId,
            "task.created",
            new { taskId = "T-1" });
        var firstOccurrenceRetry = await publisher.PublishOutboxAsync(
            firstOccurrenceId,
            projectId,
            "task.created",
            new { taskId = "T-1" });
        var secondOccurrence = await publisher.PublishOutboxAsync(
            secondOccurrenceId,
            projectId,
            "task.created",
            new { taskId = "T-1" });

        first.Status.Should().Be("delivered");
        first.IsDelivered.Should().BeTrue();
        first.DeliveryLogId.Should().NotBeNull();
        duplicate.Status.Should().Be("already_delivered");
        duplicate.DeliveryLogId.Should().Be(first.DeliveryLogId);
        firstOccurrence.IsComplete.Should().BeTrue();
        firstOccurrenceRetry.IsComplete.Should().BeTrue();
        secondOccurrence.IsComplete.Should().BeTrue();
        await using var verifyScope = provider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.WebhookDeliveryLogs.CountAsync()).Should().Be(3,
            "the same outbox occurrence is idempotent while two identical business occurrences remain distinct");
    }

    private static IWebhookEndpointPolicy AllowedEndpointPolicy()
    {
        var policy = new Mock<IWebhookEndpointPolicy>();
        policy.Setup(item => item.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
                WebhookEndpointValidation.Allowed(new Uri(url)));
        return policy.Object;
    }

    private sealed class StaticResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("ok")
            });
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
