using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public class AiGatewayEvidenceTests : IDisposable
{
    private readonly QalyDbContext _context;

    public AiGatewayEvidenceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_SensitiveRequestWithoutConsent_ReturnsComplianceMockAndAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gateway = CreateGateway();

        var response = await gateway.ExecuteAsync(new AiRequest
        {
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            JobType = "rag_search",
            SystemPrompt = "system",
            Prompt = "contains sensitive transcript",
            ExpectedSchemaId = "TextAnswer.v1",
            IsSensitive = true
        });

        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("ComplianceMock");

        var audit = await _context.AiAuditEvents.SingleAsync();
        audit.TenantId.Should().Be(tenantId);
        audit.ProjectId.Should().Be(projectId);
        audit.ActorUserId.Should().Be(userId);
        audit.EventType.Should().Be("AI_BLOCKED");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDailyBudgetExceeded_ReturnsBudgetMock()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _context.AiBudgetPolicies.Add(new AiBudgetPolicy
        {
            TenantId = tenantId,
            ProjectId = projectId,
            DailyBudgetUsd = 0.01m,
            HardStopEnabled = true
        });
        _context.AiUsageLedger.Add(new AiUsageLedger
        {
            TenantId = tenantId,
            ProjectId = projectId,
            JobType = "rag_search",
            ProviderName = "OpenAI",
            ModelName = "gpt-test",
            EstimatedCostUsd = 0.01m,
            Status = "success"
        });
        await _context.SaveChangesAsync();

        var response = await CreateGateway().ExecuteAsync(new AiRequest
        {
            TenantId = tenantId,
            ProjectId = projectId,
            JobType = "rag_search",
            SystemPrompt = "system",
            Prompt = "next request",
            ExpectedSchemaId = "TextAnswer.v1",
            IsSensitive = false
        });

        response.IsMock.Should().BeTrue();
        response.ProviderName.Should().Be("BudgetMock");
    }

    [Fact]
    public async Task ExecuteAsync_GoldenDatasetCacheHit_ReturnsCachedResponseAndRecordsCostLedger()
    {
        var fixture = LoadGoldenDataset();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new AiRequest
        {
            TenantId = tenantId,
            ProjectId = projectId,
            UserId = userId,
            JobType = "meeting_import",
            SystemPrompt = fixture.SystemPrompt,
            Prompt = fixture.Prompt,
            ExpectedSchemaId = fixture.SchemaId,
            UseCache = true
        };

        _context.AiPromptCache.Add(new AiPromptCache
        {
            TenantId = tenantId,
            ProjectId = projectId,
            CacheKey = fixture.Id,
            JobType = request.JobType,
            SchemaId = fixture.SchemaId,
            ProviderName = fixture.ProviderName,
            ModelName = fixture.ModelName,
            RequestHash = ComputeHash(request),
            ResponseJson = fixture.ResponseJson,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        await _context.SaveChangesAsync();

        var response = await CreateGateway().ExecuteAsync(request);

        response.CacheHit.Should().BeTrue();
        response.Content.Should().Contain("Prepare demo script");
        response.ProviderName.Should().Be("GoldenDataset");

        var ledger = await _context.AiUsageLedger.SingleAsync();
        ledger.CacheHit.Should().BeTrue();
        ledger.EstimatedCostUsd.Should().Be(0m);
        ledger.ProviderName.Should().Be("GoldenDataset");
    }

    [Fact]
    public async Task QdrantSearchAsync_WithoutProjectFilter_ThrowsBeforeProviderCall()
    {
        using var service = CreateQdrantService();

        var act = () => service.SearchAsync([0.1f, 0.2f], "qaly", new VectorFilter
        {
            OwnerId = Guid.NewGuid()
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*project filter*");
    }

    [Fact]
    public async Task QdrantSearchAsync_WithoutCurrentUserFilter_ThrowsBeforeProviderCall()
    {
        using var service = CreateQdrantService();

        var act = () => service.SearchAsync([0.1f, 0.2f], "qaly", new VectorFilter
        {
            ProjectId = Guid.NewGuid()
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*current user filter*");
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private AiGateway CreateGateway()
        => new(
            Mock.Of<IChatClient>(),
            new AiCostService(_context),
            new AiComplianceService(_context),
            _context,
            NullLogger<AiGateway>.Instance);

    private static QdrantVectorStorageService CreateQdrantService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:QdrantUrl"] = "http://localhost:6333"
            })
            .Build();

        return new QdrantVectorStorageService(configuration);
    }

    private static GoldenDataset LoadGoldenDataset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-golden-dataset.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GoldenDataset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private static string ComputeHash(AiRequest request)
    {
        var rawData = request.SystemPrompt + "|" + request.Prompt + "|" + request.ExpectedSchemaId;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        var builder = new StringBuilder();
        foreach (var item in bytes)
        {
            builder.Append(item.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private sealed record GoldenDataset(
        string Id,
        string SchemaId,
        string ProviderName,
        string ModelName,
        string SystemPrompt,
        string Prompt,
        string ResponseJson);
}
