using System.Text.Json;
using System.Globalization;
using FluentAssertions;
using Qaly.Application.DTOs.Ai;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class TaskSkillSuggestionContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid TaskId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OrganizationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FrontendSkillId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid BackendSkillId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly string SourceRef = $"task:{TaskId:D}";
    private static readonly string[] DuplicateSourceRefs = [SourceRef, SourceRef];
    private static readonly string[] DuplicateUnmappedTerms = ["GraphQL", "graphql"];

    [Fact]
    public void TryBuildResult_AuthorizedCatalog_ReconcilesCanonicalNameLevelAndSource()
    {
        var provider = JsonSerializer.Serialize(new
        {
            schemaId = TaskSkillAiContract.SchemaId,
            taskId = TaskId,
            sourceVersion = "source-v1",
            dataState = "ready",
            suggestions = new[]
            {
                new
                {
                    skillId = FrontendSkillId,
                    canonicalName = "invented client name",
                    requiredLevel = "proficient",
                    confidence = 0.846m,
                    rationale = "The task explicitly requires a Vue component.",
                    sourceRefs = DuplicateSourceRefs
                }
            },
            unmappedTerms = DuplicateUnmappedTerms,
            generatedAt = DateTimeOffset.Parse("2026-07-27T10:00:00Z", CultureInfo.InvariantCulture)
        }, JsonOptions);

        var valid = TaskSkillSuggestionContract.TryBuildResult(
            provider,
            Snapshot(),
            out var resultJson,
            out var error);

        valid.Should().BeTrue(error);
        using var result = JsonDocument.Parse(resultJson);
        var suggestion = result.RootElement.GetProperty("suggestions")[0];
        suggestion.GetProperty("skillId").GetGuid().Should().Be(FrontendSkillId);
        suggestion.GetProperty("canonicalName").GetString().Should().Be("Vue.js");
        suggestion.GetProperty("requiredLevel").GetString().Should().Be("Proficient");
        suggestion.GetProperty("confidence").GetDecimal().Should().Be(0.85m);
        suggestion.GetProperty("sourceRefs").EnumerateArray().Select(item => item.GetString())
            .Should().Equal(SourceRef);
        result.RootElement.GetProperty("unmappedTerms").GetArrayLength().Should().Be(1);
        TaskSkillSuggestionContract.TryValidateFinal(resultJson, out error).Should().BeTrue(error);
    }

    [Fact]
    public void TryBuildResult_OutsideCatalogOrUngroundedReference_FailsClosed()
    {
        var outsideCatalog = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var invalidSkill = ProviderOutput(outsideCatalog, SourceRef);
        var invalidSource = ProviderOutput(FrontendSkillId, "task:ffffffff-ffff-ffff-ffff-ffffffffffff");

        TaskSkillSuggestionContract.TryBuildResult(
            invalidSkill,
            Snapshot(),
            out _,
            out var skillError).Should().BeFalse();
        skillError.Should().Contain("catalog");

        TaskSkillSuggestionContract.TryBuildResult(
            invalidSource,
            Snapshot(),
            out _,
            out var sourceError).Should().BeFalse();
        sourceError.Should().Contain("authorized task source");
    }

    [Fact]
    public void TryBuildEmptyResult_EmptyCatalog_ProducesValidatedNoMutationResult()
    {
        var emptySnapshot = Snapshot(skills: []);

        TaskSkillSuggestionContract.SnapshotIsEmpty(emptySnapshot).Should().BeTrue();
        TaskSkillSuggestionContract.TryBuildEmptyResult(
            emptySnapshot,
            out var resultJson,
            out var error).Should().BeTrue(error);

        using var result = JsonDocument.Parse(resultJson);
        result.RootElement.GetProperty("dataState").GetString().Should().Be("empty");
        result.RootElement.GetProperty("suggestions").GetArrayLength().Should().Be(0);
        TaskSkillSuggestionContract.TryValidateFinal(resultJson, out error).Should().BeTrue(error);
    }

    [Theory]
    [InlineData("""{"schemaId":"task_skill_suggestion.v1","taskId":"11111111-1111-1111-1111-111111111111","sourceVersion":"v","dataState":"ready","suggestions":null,"unmappedTerms":[],"generatedAt":"2026-07-27T10:00:00Z"}""")]
    [InlineData("""{"schemaId":"task_skill_suggestion.v1","taskId":"11111111-1111-1111-1111-111111111111","sourceVersion":"v","dataState":"ready","suggestions":[],"unmappedTerms":null,"generatedAt":"2026-07-27T10:00:00Z"}""")]
    public void TryValidateFinal_NullCollections_FailsClosedWithoutThrowing(string payload)
    {
        var action = () => TaskSkillSuggestionContract.TryValidateFinal(payload, out _);

        action.Should().NotThrow();
        action().Should().BeFalse();
    }

    [Fact]
    public void TryValidateFinal_InconsistentStateDuplicateSkillsOrNonCanonicalLevel_FailsClosed()
    {
        var suggestion = new TaskSkillSuggestionItemDto(
            FrontendSkillId,
            "Vue.js",
            "Proficient",
            0.9m,
            "Grounded rationale.",
            [SourceRef]);
        var readyWithoutSuggestions = JsonSerializer.Serialize(new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            TaskId,
            "source-v1",
            "ready",
            [],
            [],
            DateTimeOffset.Parse("2026-07-27T10:00:00Z", CultureInfo.InvariantCulture)),
            JsonOptions);
        var duplicateSkills = JsonSerializer.Serialize(new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            TaskId,
            "source-v1",
            "ready",
            [suggestion, suggestion],
            [],
            DateTimeOffset.Parse("2026-07-27T10:00:00Z", CultureInfo.InvariantCulture)),
            JsonOptions);
        var nonCanonicalLevel = JsonSerializer.Serialize(new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            TaskId,
            "source-v1",
            "ready",
            [suggestion with { RequiredLevel = "proficient" }],
            [],
            DateTimeOffset.Parse("2026-07-27T10:00:00Z", CultureInfo.InvariantCulture)),
            JsonOptions);

        TaskSkillSuggestionContract.TryValidateFinal(readyWithoutSuggestions, out _).Should().BeFalse();
        TaskSkillSuggestionContract.TryValidateFinal(duplicateSkills, out _).Should().BeFalse();
        TaskSkillSuggestionContract.TryValidateFinal(nonCanonicalLevel, out _).Should().BeFalse();
    }

    [Fact]
    public void TryBuildResult_InvalidSchemaOrSourceVersion_FailsClosed()
    {
        var wrongSchema = ProviderOutput(FrontendSkillId, SourceRef)
            .Replace(TaskSkillAiContract.SchemaId, "task_skill_suggestion.v2", StringComparison.Ordinal);
        var wrongVersion = ProviderOutput(FrontendSkillId, SourceRef)
            .Replace("source-v1", "source-v2", StringComparison.Ordinal);

        TaskSkillSuggestionContract.TryBuildResult(
            wrongSchema,
            Snapshot(),
            out _,
            out var schemaError).Should().BeFalse();
        schemaError.Should().Contain("schemaId");

        TaskSkillSuggestionContract.TryBuildResult(
            wrongVersion,
            Snapshot(),
            out _,
            out var versionError).Should().BeFalse();
        versionError.Should().Contain("sourceVersion");
    }

    private static string ProviderOutput(Guid skillId, string sourceRef)
        => JsonSerializer.Serialize(new
        {
            schemaId = TaskSkillAiContract.SchemaId,
            taskId = TaskId,
            sourceVersion = "source-v1",
            dataState = "ready",
            suggestions = new[]
            {
                new
                {
                    skillId,
                    canonicalName = "anything",
                    requiredLevel = "Expert",
                    confidence = 0.75m,
                    rationale = "Grounded rationale.",
                    sourceRefs = new[] { sourceRef }
                }
            },
            unmappedTerms = Array.Empty<string>(),
            generatedAt = DateTimeOffset.Parse("2026-07-27T10:00:00Z", CultureInfo.InvariantCulture)
        }, JsonOptions);

    private static string Snapshot(IReadOnlyList<object>? skills = null)
        => JsonSerializer.Serialize(new
        {
            schemaId = TaskSkillAiContract.SnapshotSchemaId,
            task = new
            {
                id = TaskId,
                projectId = ProjectId,
                organizationId = OrganizationId,
                taskRowVersion = "task-v1",
                title = "Build the Vue task card and ASP.NET API",
                description = "Implement a Vue component backed by ASP.NET Core.",
                priority = "High",
                isPrivate = false,
                sourceRef = SourceRef
            },
            sourceVersion = "source-v1",
            catalogVersion = "catalog-v1",
            language = "vi",
            skills = skills ??
            [
                new { id = FrontendSkillId, name = "Vue.js", description = "Frontend component engineering" },
                new { id = BackendSkillId, name = "ASP.NET Core", description = "Backend API engineering" }
            ]
        }, JsonOptions);
}
