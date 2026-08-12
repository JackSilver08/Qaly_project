using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiNativeConversationLaunchContractTests
{
    [Fact]
    public void NativeModuleCoverage_CoversEveryMainModuleAndOperationWithoutFalseNativeClaims()
    {
        var matrix = AiNativeModuleCoverageCatalog.Matrix();
        matrix.SchemaId.Should().Be(AiNativeModuleCoverageContract.SchemaId);
        matrix.CoveragePercent.Should().Be(100);
        matrix.Modules.Select(item => item.ModuleId).Should().Contain(
            ["workspace", "organizations", "projects", "tasks", "sprints", "teams",
             "skills_capacity", "groups_polls", "meetings", "calendar", "wiki",
             "analytics_reports", "notifications", "settings"]);
        matrix.Modules.Should().OnlyContain(module => module.Operations.Select(item => item.Operation)
            .OrderBy(item => item)
            .SequenceEqual(AiNativeModuleCoverageContract.RequiredOperations.OrderBy(item => item)));
        matrix.Modules.SelectMany(module => module.Operations)
            .Where(operation => operation.Status == "native")
            .Should().OnlyContain(operation => !string.IsNullOrWhiteSpace(operation.CapabilityId));
        matrix.Modules.SelectMany(module => module.Operations)
            .Should().OnlyContain(operation => operation.Status == "native" ||
                operation.Status == "existing_native_surface" || operation.Status == "guided");
    }
    private static readonly string[] ValidScope = ["Authentication", "Core workflow"];
    private static readonly string[] ValidSuccessMeasures = ["Acceptance flow passes"];
    private static readonly string[] ValidAssumptions = ["One product owner is available"];
    private static readonly string[] ValidUnknowns = ["Exact launch date"];

    [Fact]
    public void ProjectLaunchCapability_IsArtifactOnlyAndUsesRegisteredContracts()
    {
        AiAssistantCapabilityCatalog.TryGet(AiProjectLaunchContract.CapabilityId, out var descriptor)
            .Should().BeTrue();
        descriptor.Kind.Should().Be("artifact");
        descriptor.RiskClass.Should().Be("read_only_proposal");
        descriptor.ConfirmationPolicy.Should().Be("none");
        descriptor.InputSchemaId.Should().Be(AiProjectLaunchContract.RequestSchemaId);
        descriptor.OutputSchemaId.Should().Be(AiProjectLaunchContract.BriefSchemaId);
        descriptor.ContextSources.Should().Contain(AiAssistantContextContract.OrganizationRulebookSource);
    }

    [Fact]
    public void ProjectLaunchModelContract_RequiresUsefulBoundedJson()
    {
        var valid = JsonSerializer.Serialize(new
        {
            proposedProjectName = "Customer SPA",
            objective = "Deliver the first production release.",
            scope = ValidScope,
            exclusions = Array.Empty<string>(),
            successMeasures = ValidSuccessMeasures,
            facts = Array.Empty<string>(),
            assumptions = ValidAssumptions,
            unknowns = ValidUnknowns
        });

        AiProjectLaunchOutputContract.TryParse(valid, out var output, out var error).Should().BeTrue(error);
        output!.Scope.Should().HaveCount(2);
        AiProjectLaunchOutputContract.TryParse("{}", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void ManualGuidance_ContainsOnlyServerRegisteredRoutes()
    {
        var guidance = AiAssistantManualGuidanceRegistry.ForCapability(AiProjectLaunchContract.CapabilityId);
        guidance.SchemaId.Should().Be(AiAssistantConversationContract.GuidanceSchemaId);
        guidance.Steps.Should().HaveCount(3);
        guidance.Steps.Should().OnlyContain(step => AiAssistantManualGuidanceRegistry.IsRegisteredRoute(step.Route));
    }

    [Fact]
    public async Task Rulebook_DraftRequiresManagerAndActivationSupersedesPreviousVersion()
    {
        await using var db = CreateContext();
        var owner = User("Owner");
        var member = User("Member");
        var organization = new Organization
        {
            Name = "Rulebook Organization",
            Code = "RULES",
            OwnerId = owner.Id,
            IsActive = true
        };
        db.AddRange(owner, member, organization,
            new OrganizationMember { OrganizationId = organization.Id, UserId = owner.Id, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = organization.Id, UserId = member.Id, Role = OrganizationRoleRules.Member });
        await db.SaveChangesAsync();

        var ownerService = CreateRulebookService(db, owner.Id);
        var rules = new[]
        {
            new OrganizationWorkRuleDto("max_active_projects", "capacity", "block", "Limit active projects", 5, "projects")
        };
        var first = await ownerService.CreateDraftAsync(
            organization.Id, new CreateOrganizationWorkRuleSetRequestDto(rules));
        first.IsSuccess.Should().BeTrue(first.Error);
        var firstActive = await ownerService.ActivateAsync(
            organization.Id, first.Data!.RuleSetId, new ActivateOrganizationWorkRuleSetRequestDto(first.Data.Revision));
        firstActive.IsSuccess.Should().BeTrue(firstActive.Error);

        var second = await ownerService.CreateDraftAsync(
            organization.Id, new CreateOrganizationWorkRuleSetRequestDto(rules));
        var secondActive = await ownerService.ActivateAsync(
            organization.Id, second.Data!.RuleSetId, new ActivateOrganizationWorkRuleSetRequestDto(second.Data.Revision));
        secondActive.Data!.Version.Should().Be(2);
        (await db.OrganizationWorkRuleSets.SingleAsync(item => item.Id == first.Data.RuleSetId)).Status
            .Should().Be("superseded");
        (await ownerService.GetEffectiveAsync(organization.Id)).Data!.RuleSetId.Should().Be(second.Data.RuleSetId);

        var memberService = CreateRulebookService(db, member.Id);
        var forbidden = await memberService.CreateDraftAsync(
            organization.Id, new CreateOrganizationWorkRuleSetRequestDto(rules));
        forbidden.StatusCode.Should().Be(403);
    }

    private static OrganizationWorkRulebookService CreateRulebookService(QalyDbContext db, Guid userId)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.UserId).Returns(userId);
        current.SetupGet(item => item.Role).Returns("User");
        current.SetupGet(item => item.IsAuthenticated).Returns(true);
        return new OrganizationWorkRulebookService(db, current.Object);
    }

    private static QalyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User User(string name)
        => new()
        {
            FullName = name,
            Email = $"{Guid.NewGuid():N}@rulebook.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        };
}
