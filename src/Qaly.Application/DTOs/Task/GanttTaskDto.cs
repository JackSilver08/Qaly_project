namespace Qaly.Application.DTOs.Task;

public class GanttTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int Progress { get; set; }
    public bool IsCriticalPath { get; set; }
    public List<Guid> Dependencies { get; set; } = new();
}
