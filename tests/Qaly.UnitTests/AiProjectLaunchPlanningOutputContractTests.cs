using FluentAssertions;
using Qaly.Application.Services;
using Qaly.Application.DTOs.Ai;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiProjectLaunchPlanningOutputContractTests
{
    [Fact]
    public void TryParse_AcceptsStrictAcyclicDeliveryPlan()
    {
        var success = AiProjectLaunchPlanningOutputContract.TryParse(ValidJson(), out var plan, out var error);

        success.Should().BeTrue(error);
        plan!.Sprints.Should().HaveCount(1);
        plan.Sprints[0].Tasks.Should().HaveCount(2);
        plan.CriticalPathClientIds.Should().Equal("task-1", "task-2");
    }

    [Fact]
    public void TryParse_RejectsUnknownFields()
    {
        var json = ValidJson().Replace(
            "\"architectureProposal\"",
            "\"untrustedToolCall\":{},\"architectureProposal\"",
            StringComparison.Ordinal);

        AiProjectLaunchPlanningOutputContract.TryParse(json, out _, out var error).Should().BeFalse();
        error.Should().Contain("unknown field");
    }

    [Fact]
    public void TryParse_RejectsCyclicDependencies()
    {
        var json = ValidJson().Replace(
            "\"dependencyClientIds\":[]",
            "\"dependencyClientIds\":[\"task-2\"]",
            StringComparison.Ordinal);

        AiProjectLaunchPlanningOutputContract.TryParse(json, out _, out var error).Should().BeFalse();
        error.Should().Contain("cyclic");
    }

    [Fact]
    public void AiOutputValidator_RoutesLaunchPlanThroughStrictContract()
    {
        var validator = new AiOutputValidator();

        validator.Validate(ValidJson(), AiProjectOrchestrationContract.ModelPlanSchemaId, out var validError)
            .Should().BeTrue(validError);
        validator.Validate("{\"architectureProposal\":[]}", AiProjectOrchestrationContract.ModelPlanSchemaId, out var invalidError)
            .Should().BeFalse();
        invalidError.Should().NotBeNullOrWhiteSpace();
    }

    private static string ValidJson() => """
    {
      "architectureProposal":["Modular SPA"],
      "sprints":[{
        "clientId":"sprint-1",
        "name":"Foundation",
        "objective":"Deliver a working vertical slice",
        "startWeek":1,
        "durationWeeks":2,
        "exitCriteria":["Acceptance flow passes"],
        "tasks":[
          {
            "clientId":"task-1",
            "title":"Create the vertical slice",
            "description":"Implement the reviewed workflow.",
            "acceptanceCriteria":["Workflow passes"],
            "definitionOfDone":["Reviewed and tested"],
            "priority":"High",
            "estimatedHours":16,
            "requiredSkillNames":[],
            "dependencyClientIds":[]
          },
          {
            "clientId":"task-2",
            "title":"Verify the vertical slice",
            "description":"Run acceptance and operational checks.",
            "acceptanceCriteria":["Checks pass"],
            "definitionOfDone":["Evidence recorded"],
            "priority":"Medium",
            "estimatedHours":8,
            "requiredSkillNames":[],
            "dependencyClientIds":["task-1"]
          }
        ]
      }],
      "criticalPathClientIds":["task-1","task-2"],
      "collaborationProposal":["Daily dependency review"],
      "externalDeferred":["Repository provisioning requires a separate adapter"],
      "assumptions":["Estimates require team review"]
    }
    """;
}
