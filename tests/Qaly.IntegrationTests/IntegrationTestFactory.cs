using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Qaly.Infrastructure.Data;
using StackExchange.Redis;
using Moq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using System.Text.Json;

namespace Qaly.IntegrationTests;

public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"QalyIntegrationTests-{Guid.NewGuid()}";
    public Guid TestUserId { get; } = Guid.Parse("B0000000-0000-0000-0000-000000000000");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["Redis:ConnectionString"] = "localhost:6379", // Just to satisfy Program.cs
                ["AiJobsV4:Enabled"] = "true",
                ["AiJobsV4:WorkerEnabled"] = "false",
                ["AiJobsV4:BudgetUiEnabled"] = "true",
                ["AiJobsV4:TaskSkillSuggestionEnabled"] = "true",
                ["AiJobsV4:ActionComposerEnabled"] = "true",
                ["AiJobsV4:ActionComposerTaskCreateEnabled"] = "true",
                ["AiJobsV4:AssistantSessionEnabled"] = "true",
                ["AiJobsV4:AssistantContextRegistryEnabled"] = "true",
                ["AiJobsV4:AssistantResearchPlanEnabled"] = "true",
                ["AiJobsV4:AssistantGoalPlannerEnabled"] = "true",
                ["PrivacyV4:Enabled"] = "true",
                ["PrivacyV4:WorkerEnabled"] = "false",
                ["PrivacyV4:EnforceSensitiveIngestion"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Mock Redis
            var redisMock = new Mock<IConnectionMultiplexer>();
            var dbMock = new Mock<IDatabase>();
            redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
            redisMock.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns(Array.Empty<System.Net.EndPoint>());
            services.AddSingleton(redisMock.Object);
            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.RemoveAll<QalyDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<QalyDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Storage.IDatabaseProvider>();
            services.AddDbContext<QalyDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Mock other heavy infrastructure
            services.AddSingleton(new Mock<IAiIngestionService>().Object);
            services.AddSingleton(new Mock<IAiService>().Object);
            services.RemoveAll<IAiGateway>();
            services.AddSingleton<IAiGateway>(new IntegrationAiGateway());

            // Test Auth
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
        });
    }

    private sealed class IntegrationAiGateway : IAiGateway
    {
        private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

        public Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            if (string.Equals(request.ExpectedSchemaId, TaskDraftAiContract.SchemaId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(request.ValidationContextJson))
            {
                var snapshot = JsonSerializer.Deserialize<TaskDraftSourceSnapshotDto>(
                    request.ValidationContextJson,
                    WebJsonOptions)!;
                var taskDraftContent = JsonSerializer.Serialize(new
                {
                    schemaId = TaskDraftAiContract.SchemaId,
                    dataState = "ready",
                    tasks = new[]
                    {
                        new
                        {
                            clientId = "integration-task-1",
                            title = "Review selected-message deliverable",
                            description = "Created only as a reviewable source-linked draft.",
                            priority = "High",
                            status = "Todo",
                            dueDate = (DateTimeOffset?)null,
                            assigneeId = (Guid?)null,
                            selected = true,
                            confidence = 0.84m,
                            sourceRefs = new[] { snapshot.Sources[0].Ref }
                        },
                        new
                        {
                            clientId = "integration-task-2",
                            title = "Prepare follow-up acceptance notes",
                            description = "Second option remains unselected during selective confirmation.",
                            priority = "Medium",
                            status = "Todo",
                            dueDate = (DateTimeOffset?)null,
                            assigneeId = (Guid?)null,
                            selected = true,
                            confidence = 0.72m,
                            sourceRefs = new[] { snapshot.Sources[0].Ref }
                        }
                    }
                });
                return Task.FromResult(new AiResponse
                {
                    Content = taskDraftContent,
                    ProviderName = "IntegrationProvider",
                    ModelName = "task-draft-fixture-v1",
                    InputTokens = 80,
                    OutputTokens = 120
                });
            }

            if (string.Equals(request.ExpectedSchemaId, GroupSummaryAiContract.SchemaId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(request.ValidationContextJson))
            {
                var snapshot = JsonSerializer.Deserialize<GroupSummarySnapshotDto>(
                    request.ValidationContextJson,
                    WebJsonOptions)!;
                var sourceRef = snapshot.SourceRefs[0].Key;
                return Task.FromResult(new AiResponse
                {
                    Content = JsonSerializer.Serialize(new
                    {
                        summary = "Nhóm đã thống nhất một hướng xử lý từ các tin nhắn được chọn.",
                        summarySourceRefs = new[] { sourceRef },
                        keyDecisions = new[] { new { text = "Ưu tiên hoàn thành đầu việc đã nêu.", sourceRefs = new[] { sourceRef } } },
                        openQuestions = Array.Empty<object>(),
                        actionCandidates = new[] { new { title = "Theo dõi đầu việc", details = "Xác nhận tiến độ với nhóm.", sourceRefs = new[] { sourceRef } } }
                    }),
                    ProviderName = "IntegrationProvider",
                    ModelName = "group-summary-fixture-v1",
                    InputTokens = 60,
                    OutputTokens = 90
                });
            }

            if (string.Equals(request.ExpectedSchemaId, DashboardStrategicBriefAiContract.SchemaId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(request.ValidationContextJson))
            {
                return Task.FromResult(new AiResponse
                {
                    Content = JsonSerializer.Serialize(new
                    {
                        summaryPoints = new[] { new { text = "Snapshot cho thấy phạm vi dự án hiện tại cần được theo dõi.", metricRefs = new[] { "projectCount" }, sourceRefs = Array.Empty<string>() } },
                        risks = new[] { new { severity = "medium", title = "Cần kiểm tra các task quá hạn.", metricRefs = new[] { "overdue" }, sourceRefs = Array.Empty<string>() } },
                        priorities = new[] { new { title = "Rà soát công việc mở", rationale = "Ưu tiên theo số task quá hạn trong snapshot.", metricRefs = new[] { "overdue" }, sourceRefs = Array.Empty<string>() } }
                    }),
                    ProviderName = "IntegrationProvider",
                    ModelName = "dashboard-brief-fixture-v1",
                    InputTokens = 80,
                    OutputTokens = 110
                });
            }

            if (string.Equals(request.ExpectedSchemaId, MeetingChecknoteAiContract.SchemaId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(request.ValidationContextJson))
            {
                using var contextDocument = JsonDocument.Parse(request.ValidationContextJson);
                var transcript = contextDocument.RootElement.GetProperty("transcript").GetString() ?? string.Empty;
                if (transcript.Contains("FORCE_PROVIDER_FAILURE", StringComparison.Ordinal))
                {
                    return Task.FromResult(new AiResponse
                    {
                        IsSuccess = false,
                        ErrorCode = AiErrorCodes.ProviderUnavailable,
                        ErrorMessage = "Forced meeting provider failure."
                    });
                }
                if (transcript.Contains("FORCE_SCHEMA_INVALID", StringComparison.Ordinal))
                {
                    return Task.FromResult(new AiResponse
                    {
                        Content = "{}",
                        ProviderName = "IntegrationProvider",
                        ModelName = "meeting-invalid-fixture-v1"
                    });
                }
                var evidence = transcript.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? transcript;
                return Task.FromResult(new AiResponse
                {
                    Content = JsonSerializer.Serialize(new
                    {
                        summary = "Cuộc họp đã nêu một đầu việc cần theo dõi.",
                        summaryEvidence = new[] { evidence },
                        decisions = Array.Empty<object>(),
                        risks = Array.Empty<object>(),
                        actionItems = new[]
                        {
                            new { title = "Theo dõi nội dung cuộc họp", description = "Kiểm tra đầu việc đã nêu.", suggestedOwner = (string?)null, dueDate = (string?)null, priority = "Medium", evidence }
                        }
                    }),
                    ProviderName = "IntegrationProvider",
                    ModelName = "meeting-checknote-fixture-v1",
                    InputTokens = 90,
                    OutputTokens = 100
                });
            }

            if (string.Equals(request.ExpectedSchemaId, AiAssistantGoalPlanningContract.SchemaId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(request.ValidationContextJson))
            {
                var context = JsonSerializer.Deserialize<AiAssistantGoalPlanningValidationContextDto>(
                    request.ValidationContextJson, WebJsonOptions)!;
                var normalized = context.Message.ToLowerInvariant();
                var isTask = normalized.Contains("task") || normalized.Contains("nhiệm vụ");
                var isResearch = normalized.Contains("phân tích") &&
                    (normalized.Contains("đề xuất") || normalized.Contains("phương án"));
                var isUnsupportedCreate = normalized.Contains("dự án mới") || normalized.Contains("project mới");
                var isDemoTest = normalized.Contains("test demo") || normalized.Contains("test tất cả");
                var skillId = isUnsupportedCreate || isDemoTest ? null
                    : isTask ? AiAssistantContextContract.TaskCreateCapability
                    : isResearch ? AiAssistantContextContract.ResearchPlanCapability
                    : AiAssistantContextContract.GroundedReadCapability;
                var ranked = skillId == null
                    ? Array.Empty<object>()
                    : new object[] { new { skillId, fitReason = "Phù hợp với mục tiêu đã phân tích.", confidence = 0.91 } };
                var missing = isUnsupportedCreate || isDemoTest
                    ? new object[] { isDemoTest
                        ? new { skillId = "demo.test.run.v1", title = "Chạy bộ test/demo", reason = "Chưa có test adapter an toàn.", suggestedPath = "Bổ sung allowlist sandbox và report read-only." }
                        : new { skillId = "project.create.v1", title = "Soạn dự án", reason = "Chưa có adapter tạo dự án.", suggestedPath = "Bổ sung draft/review/confirm contract cho project." } }
                    : Array.Empty<object>();
                var scope = new
                {
                    scopeType = context.ClientContext?.ProjectId.HasValue == true ? "project" : "workspace",
                    projectId = context.ClientContext?.ProjectId,
                    entityType = context.ClientContext?.EntityType,
                    entityId = context.ClientContext?.EntityId,
                    label = "Model proposed scope",
                    confidence = 0.9,
                    reason = "Server will reconcile this scope."
                };
                var content = JsonSerializer.Serialize(new
                {
                    schemaId = AiAssistantGoalPlanningContract.SchemaId,
                    promptId = AiAssistantGoalPlanningContract.PromptId,
                    promptVersion = AiAssistantGoalPlanningContract.PromptVersion,
                    objective = context.Message,
                    userJob = isUnsupportedCreate ? "Tạo dự án mới" : isDemoTest ? "Chạy kiểm thử demo" : "Hoàn thành yêu cầu trong Qaly",
                    intentFacets = new[] { isUnsupportedCreate ? "project_create" : isDemoTest ? "demo_test" : skillId! },
                    scopes = new[] { scope },
                    constraints = Array.Empty<string>(),
                    unknowns = Array.Empty<object>(),
                    assumptions = Array.Empty<string>(),
                    rankedSkills = ranked,
                    missingSkills = missing,
                    riskLevel = isTask || isUnsupportedCreate ? "medium" : "low",
                    requiresConfirmation = isTask || isUnsupportedCreate,
                    disposition = isUnsupportedCreate || isDemoTest ? "unsupported_but_analyzed" : "plannable",
                    confidence = 0.91,
                    warnings = Array.Empty<string>(),
                    workPlan = new
                    {
                        schemaId = AiAssistantGoalPlanningContract.WorkPlanSchemaId,
                        objective = context.Message,
                        scope,
                        selectedSkillIds = skillId == null ? Array.Empty<string>() : new[] { skillId },
                        steps = new[]
                        {
                            new { stepId = "S1", kind = "analyze", publicLabel = "Phân tích mục tiêu", skillId = (string?)null, sourceIds = Array.Empty<string>(), dependencyIds = Array.Empty<string>(), expectedOutputSchemaId = (string?)null, verificationIds = new[] { "goal_contract_valid" }, mutationClass = "none", state = "planned" }
                        },
                        blockingUnknowns = Array.Empty<object>(),
                        maxSteps = 8,
                        maxAttemptsPerStep = 2,
                        stopConditions = new[] { "policy_denied" },
                        requiresPlanApproval = isTask
                    }
                });
                return Task.FromResult(new AiResponse
                {
                    Content = content,
                    ProviderName = "IntegrationProvider",
                    ModelName = "goal-planner-fixture-v1",
                    InputTokens = 70,
                    OutputTokens = 120
                });
            }

            if (!string.Equals(request.ExpectedSchemaId, AiAssistantResearchPlanContract.SchemaId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(request.ValidationContextJson))
            {
                return Task.FromResult(new AiResponse
                {
                    IsSuccess = false,
                    ErrorCode = "TEST_PROVIDER_NOT_CONFIGURED",
                    ErrorMessage = "Integration provider only supports assistant_research_plan.v1."
                });
            }

            {
            var context = JsonSerializer.Deserialize<AiAssistantResearchValidationContextDto>(
                request.ValidationContextJson,
                    WebJsonOptions)!;
            var sourceRef = context.AllowedSourceRefs[0];
            var content = JsonSerializer.Serialize(new
            {
                schemaId = AiAssistantResearchPlanContract.SchemaId,
                promptId = AiAssistantResearchPlanContract.PromptId,
                promptVersion = AiAssistantResearchPlanContract.PromptVersion,
                objective = context.Objective,
                scope = new { scopeType = context.ProjectId.HasValue ? "project" : "workspace", projectId = context.ProjectId, label = context.ScopeLabel, sourceRefs = context.AllowedSourceRefs },
                findings = new[] { new { findingId = "F1", statement = "Có dữ liệu dự án được cấp quyền để lập kế hoạch.", severity = "info", confidence = 0.88, sourceRefs = new[] { sourceRef } } },
                unknowns = new[] { new { unknownId = "U1", question = "Capacity kỳ tới chưa được xác nhận.", blocking = false } },
                assumptions = new[] { "Capacity hiện tại được giữ nguyên cho tới khi người dùng xác nhận." },
                options = new[] { new { optionId = "O1", title = "Thực hiện theo ưu tiên", outcome = "Tạo một lộ trình có thể review.", tradeOffs = new[] { "Cần xác nhận capacity." }, estimatedEffort = "1-2 ngày", risk = "Ước lượng có thể thay đổi." } },
                recommendedOptionId = "O1",
                recommendationRationale = "Phương án dựa trên nguồn hiện có và giữ unknown tách biệt.",
                proposedActions = new object[]
                {
                    new { actionId = "A1", capabilityId = "task.create.v1", title = "Soạn task theo phương án", dependencyIds = Array.Empty<string>(), draftInput = new { message = context.Objective }, sourceRefs = new[] { sourceRef }, executionEligible = false, eligibilityReason = "server_reconciles" },
                    new { actionId = "A2", capabilityId = "project.create.v1", title = "Đề xuất project khác", dependencyIds = new[] { "A1" }, draftInput = new { name = "Proposal only" }, sourceRefs = new[] { sourceRef }, executionEligible = true, eligibilityReason = "model_claim" }
                },
                warnings = new[] { "Không action nào được tự động chạy." },
                privacyNotes = new[] { "model_claim" },
                freshnessAt = DateTimeOffset.UnixEpoch,
                generatedAt = DateTimeOffset.UnixEpoch,
                actualProvider = "model_claim",
                actualModel = "model_claim"
            });
            return Task.FromResult(new AiResponse
            {
                Content = content,
                ProviderName = "IntegrationProvider",
                ModelName = "research-fixture-v1",
                InputTokens = 120,
                OutputTokens = 240
            });
            }
        }
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.TryGetValue("X-Test-Auth", out var authMode))
            {
                var mode = authMode.ToString();
                if (string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                if (string.Equals(mode, "Invalid", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(AuthenticateResult.Fail("Invalid test authentication."));
                }
            }

            var userId = Request.Headers.TryGetValue("X-Test-UserId", out var userIdHeader)
                ? userIdHeader.ToString()
                : "B0000000-0000-0000-0000-000000000000";
            var role = Request.Headers.TryGetValue("X-Test-Role", out var roleHeader)
                ? roleHeader.ToString()
                : "User";

            var claims = new[] 
            { 
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
