using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using Qaly.Application.Services;
using Qaly.Domain.Entities;

namespace Qaly.UnitTests;

public class AiExportServiceTests
{
    [Fact]
    public async Task ExportProjectToWordAsync_ReturnsReadableDocx()
    {
        var service = new AiExportService();
        var project = new Project
        {
            Name = "Qaly Demo",
            Description = "Demo report",
            Status = "Active",
            Tasks =
            {
                new TaskItem
                {
                    Title = "Prepare E2E proof",
                    Status = "InProgress",
                    Priority = "High",
                    DueDate = new DateTimeOffset(2026, 5, 27, 0, 0, 0, TimeSpan.Zero)
                }
            }
        };

        var bytes = await service.ExportProjectToWordAsync(project);

        bytes.Should().NotBeEmpty();
        bytes.Take(2).Should().Equal((byte)'P', (byte)'K');

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var text = document.MainDocumentPart!.Document.InnerText;

        text.Should().Contain("Project report: Qaly Demo");
        text.Should().Contain("Prepare E2E proof");
        text.Should().Contain("High");
    }
}
