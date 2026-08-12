using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiSafeTestOrchestratorApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-SAFE-TEST-01")]
    public async Task DevCapability_PreparesDurableManifest_WithoutStartingAProcess()
    {
        using var factory = IntegrationTestFactory.CreateWithSafeTestOrchestrator();
        using var client = factory.CreateClient();
        await EnsureTestUserAsync(factory);
        var csrf = (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;

        var session = await SendAsync<AiAssistantSessionDto>(client, csrf, "/api/ai/assistant/sessions",
            new CreateAiAssistantSessionRequestDto(new AiAssistantClientContextDto("/dashboard", "workspace")));
        var request = new AiAssistantTurnRequestDto(
            "Chạy test demo tất cả CAND đã implement",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid());
        using var turnRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/assistant/turns")
        {
            Content = JsonContent.Create(request)
        };
        turnRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        turnRequest.Headers.Add("Idempotency-Key", $"safe-test-preview-{Guid.NewGuid():N}");
        var response = await client.SendAsync(turnRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn!.Intent.Should().Be(AiSafeTestOrchestratorContract.CapabilityId);
        turn.SafeTestRunPreview.Should().NotBeNull();
        turn.SafeTestRunPreview!.RequiresConfirmation.Should().BeTrue();
        turn.SafeTestRunPreview.Suites.Should().HaveCount(3);
        turn.ProcessEvents.Should().NotContain(item => item.Status == "failed");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var persisted = await db.AssistantTestRuns.SingleAsync();
        persisted.Status.Should().Be("review_required");
        persisted.IdempotencyKey.Should().BeNull();
    }

    private static async Task<T> SendAsync<T>(HttpClient client, string csrf, string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task EnsureTestUserAsync(IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (await db.Users.AnyAsync(item => item.Id == factory.TestUserId)) return;
        db.Users.Add(new User
        {
            Id = factory.TestUserId,
            FullName = "Safe Test Owner",
            Email = "safe-test-owner@qaly.test",
            PasswordHash = "not-used",
            Role = "Admin",
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private sealed record CsrfResponse(string Token);
}
