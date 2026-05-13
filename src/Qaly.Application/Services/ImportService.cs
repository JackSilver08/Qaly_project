using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Import;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class ImportService : IImportService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<ProjectLabel> _labelRepo;
    private readonly IRepository<TaskLabel> _taskLabelRepo;
    private readonly IRepository<ImportSession> _sessionRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImportService> _logger;

    private const int MaxRows = 2000;
    private const int PreviewRowCount = 5;

    private static readonly string[] ValidExtensions = [".csv", ".xlsx", ".tsv"];
    private static readonly string[] ValidStatuses = ["Todo", "InProgress", "OnHold", "InReview", "Done"];
    private static readonly string[] ValidPriorities = ["Low", "Medium", "High", "Critical"];

    // Auto-suggest mapping keywords (Vietnamese + English)
    private static readonly Dictionary<string, string[]> FieldKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Title"] = ["title", "tên", "name", "task", "tiêu đề", "tieu de", "công việc", "cong viec"],
        ["Description"] = ["description", "mô tả", "mo ta", "desc", "chi tiết", "chi tiet", "nội dung", "noi dung"],
        ["Status"] = ["status", "trạng thái", "trang thai", "column", "cột", "cot", "state"],
        ["Priority"] = ["priority", "ưu tiên", "uu tien", "độ ưu tiên", "do uu tien", "mức độ", "muc do"],
        ["DueDate"] = ["due", "deadline", "hạn", "han", "hạn chót", "han chot", "due_date", "duedate", "ngày hết hạn"],
        ["EstimatedHours"] = ["hours", "estimate", "giờ", "gio", "ước tính", "uoc tinh", "estimated"],
        ["Labels"] = ["label", "tag", "nhãn", "nhan", "tags", "labels", "thẻ", "the"],
    };

    // Status value normalization
    private static readonly Dictionary<string, string> StatusAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["todo"] = "Todo", ["backlog"] = "Todo", ["new"] = "Todo", ["mới"] = "Todo", ["moi"] = "Todo", ["open"] = "Todo",
        ["in progress"] = "InProgress", ["inprogress"] = "InProgress", ["doing"] = "InProgress",
        ["đang làm"] = "InProgress", ["dang lam"] = "InProgress", ["wip"] = "InProgress", ["active"] = "InProgress",
        ["review"] = "InReview", ["in review"] = "InReview", ["inreview"] = "InReview",
        ["đang review"] = "InReview", ["dang review"] = "InReview",
        ["done"] = "Done", ["completed"] = "Done", ["hoàn thành"] = "Done", ["hoan thanh"] = "Done",
        ["xong"] = "Done", ["finished"] = "Done", ["closed"] = "Done",
        ["hold"] = "OnHold", ["on hold"] = "OnHold", ["onhold"] = "OnHold",
        ["blocked"] = "OnHold", ["tạm dừng"] = "OnHold", ["tam dung"] = "OnHold", ["paused"] = "OnHold",
    };

    private static readonly Dictionary<string, string> PriorityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["low"] = "Low", ["thấp"] = "Low", ["thap"] = "Low",
        ["medium"] = "Medium", ["trung bình"] = "Medium", ["trung binh"] = "Medium", ["normal"] = "Medium",
        ["high"] = "High", ["cao"] = "High",
        ["critical"] = "Critical", ["urgent"] = "Critical", ["khẩn cấp"] = "Critical", ["khan cap"] = "Critical",
    };

    // Random colors for new labels
    private static readonly string[] LabelColors =
    [
        "#EF4444", "#F97316", "#F59E0B", "#22C55E", "#14B8A6",
        "#3B82F6", "#6366F1", "#8B5CF6", "#EC4899", "#64748B"
    ];

    public ImportService(
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<ProjectLabel> labelRepo,
        IRepository<TaskLabel> taskLabelRepo,
        IRepository<ImportSession> sessionRepo,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<ImportService> logger)
    {
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _labelRepo = labelRepo;
        _taskLabelRepo = taskLabelRepo;
        _sessionRepo = sessionRepo;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ParsedFileResult>> ParseFileAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!ValidExtensions.Contains(ext))
            return Result.Failure<ParsedFileResult>($"Định dạng file không hỗ trợ. Chỉ chấp nhận: {string.Join(", ", ValidExtensions)}");

        try
        {
            List<string> headers;
            List<List<string>> allRows;
            List<string>? sheetNames = null;

            if (ext == ".xlsx")
            {
                (headers, allRows, sheetNames) = ParseXlsx(fileStream);
            }
            else
            {
                var delimiter = ext == ".tsv" ? "\t" : ",";
                (headers, allRows) = ParseCsv(fileStream, delimiter);
            }

            if (headers.Count == 0)
                return Result.Failure<ParsedFileResult>("File không có dữ liệu hoặc không đọc được header.");

            if (allRows.Count > MaxRows)
                return Result.Failure<ParsedFileResult>($"File vượt quá giới hạn {MaxRows} dòng. Hiện có {allRows.Count} dòng.");

            var previewRows = allRows.Take(PreviewRowCount).ToList();
            var suggestions = AutoSuggestMappings(headers);

            return Result.Success(new ParsedFileResult(
                FileName: fileName,
                Headers: headers,
                PreviewRows: previewRows,
                TotalRowCount: allRows.Count,
                Suggestions: suggestions,
                SheetNames: sheetNames
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing file {FileName}", fileName);
            return Result.Failure<ParsedFileResult>($"Lỗi khi đọc file: {ex.Message}");
        }
    }

    public async Task<Result<ImportResult>> ExecuteImportAsync(
        Stream fileStream, string fileName, ImportRequest request, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!ValidExtensions.Contains(ext))
            return Result.Failure<ImportResult>("Định dạng file không hỗ trợ.");

        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Chưa đăng nhập.");

        // Determine target project
        Guid projectId;
        if (request.ProjectId.HasValue)
        {
            // Flow 2: Merge into existing project
            var project = await _projectRepo.GetByIdAsync(request.ProjectId.Value);
            if (project == null)
                return Result.Failure<ImportResult>("Không tìm thấy dự án.");
            projectId = project.Id;
        }
        else
        {
            // Flow 1: Create new project
            var projectName = request.NewProjectName?.Trim();
            if (string.IsNullOrWhiteSpace(projectName))
                projectName = Path.GetFileNameWithoutExtension(fileName);

            var newProject = new Project
            {
                Name = projectName,
                Code = GenerateProjectCode(projectName),
                OwnerId = userId,
                Status = "Active",
            };
            await _projectRepo.AddAsync(newProject);

            // Add owner as member
            await _memberRepo.AddAsync(new ProjectMember
            {
                ProjectId = newProject.Id,
                UserId = userId,
                Role = "Owner",
            });

            projectId = newProject.Id;
        }

        // Parse all rows
        List<string> headers;
        List<List<string>> allRows;

        try
        {
            if (ext == ".xlsx")
            {
                (headers, allRows, _) = ParseXlsx(fileStream, request.SheetName);
            }
            else
            {
                var delimiter = ext == ".tsv" ? "\t" : ",";
                (headers, allRows) = ParseCsv(fileStream, delimiter);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing file during import");
            return Result.Failure<ImportResult>($"Lỗi khi đọc file: {ex.Message}");
        }

        // Build field index map from mappings
        var fieldMap = request.Mappings
            .Where(m => m.TargetField != "Skip")
            .ToDictionary(m => m.TargetField, m => m.ColumnIndex);

        if (!fieldMap.ContainsKey("Title"))
            return Result.Failure<ImportResult>("Phải có ít nhất một cột được map vào 'Tiêu đề (Title)'.");

        // Load existing labels for this project
        var existingLabels = await _labelRepo.GetQueryable()
            .Where(l => l.ProjectId == projectId)
            .ToListAsync(ct);

        // Load existing task titles for duplicate check
        HashSet<string>? existingTitles = null;
        if (request.SkipDuplicates)
        {
            existingTitles = (await _taskRepo.GetQueryable()
                .Where(t => t.ProjectId == projectId)
                .Select(t => t.Title.ToLower().Trim())
                .ToListAsync(ct))
                .ToHashSet();
        }

        // Create import session
        var session = new ImportSession
        {
            ProjectId = projectId,
            UserId = userId,
            FileName = fileName,
            TotalRows = allRows.Count,
        };
        await _sessionRepo.AddAsync(session);

        int importedCount = 0;
        int skippedCount = 0;
        int newLabelsCreated = 0;
        var unmappedStatuses = new HashSet<string>();
        var statusDistribution = new Dictionary<string, int>();
        var random = new Random();

        foreach (var row in allRows)
        {
            // Get title
            var title = GetCellValue(row, fieldMap, "Title")?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                skippedCount++;
                continue;
            }

            // Duplicate check
            if (request.SkipDuplicates && existingTitles!.Contains(title.ToLower().Trim()))
            {
                skippedCount++;
                continue;
            }

            // Parse fields
            var description = GetCellValue(row, fieldMap, "Description");
            var rawStatus = GetCellValue(row, fieldMap, "Status");
            var rawPriority = GetCellValue(row, fieldMap, "Priority");
            var rawDueDate = GetCellValue(row, fieldMap, "DueDate");
            var rawHours = GetCellValue(row, fieldMap, "EstimatedHours");
            var rawLabels = GetCellValue(row, fieldMap, "Labels");

            // Normalize status
            var status = NormalizeStatus(rawStatus, unmappedStatuses);

            // Normalize priority
            var priority = NormalizePriority(rawPriority);

            // Parse due date
            DateTimeOffset? dueDate = ParseDate(rawDueDate);

            // Parse estimated hours
            int? estimatedHours = null;
            if (int.TryParse(rawHours, out var hours))
                estimatedHours = hours;

            // Create task
            var task = new TaskItem
            {
                Title = title,
                Description = description,
                Status = status,
                Priority = priority,
                DueDate = dueDate,
                EstimatedHours = estimatedHours,
                ProjectId = projectId,
                ReporterId = userId,
                ImportSessionId = session.Id,
            };
            await _taskRepo.AddAsync(task);

            // Handle labels
            if (!string.IsNullOrWhiteSpace(rawLabels))
            {
                var labelNames = rawLabels.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var labelName in labelNames)
                {
                    var existing = existingLabels.FirstOrDefault(
                        l => l.Name.Equals(labelName, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        existing = new ProjectLabel
                        {
                            Name = labelName,
                            Color = LabelColors[random.Next(LabelColors.Length)],
                            ProjectId = projectId,
                        };
                        await _labelRepo.AddAsync(existing);
                        existingLabels.Add(existing);
                        newLabelsCreated++;
                    }

                    await _taskLabelRepo.AddAsync(new TaskLabel
                    {
                        TaskItemId = task.Id,
                        ProjectLabelId = existing.Id,
                    });
                }
            }

            // Track distribution
            statusDistribution[status] = statusDistribution.GetValueOrDefault(status) + 1;
            importedCount++;

            if (request.SkipDuplicates)
                existingTitles!.Add(title.ToLower().Trim());
        }

        // Update session stats
        session.ImportedCount = importedCount;
        session.SkippedCount = skippedCount;

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ImportResult(
            ImportSessionId: session.Id,
            ProjectId: projectId,
            TotalRows: allRows.Count,
            ImportedCount: importedCount,
            SkippedCount: skippedCount,
            NewLabelsCreated: newLabelsCreated,
            UnmappedStatuses: unmappedStatuses.ToList(),
            StatusDistribution: statusDistribution
        ));
    }

    public async Task<Result<int>> UndoImportAsync(Guid importSessionId, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(importSessionId);
        if (session == null)
            return Result.Failure<int>("Không tìm thấy phiên import.");

        if (session.UserId != _currentUserService.UserId)
            return Result.Failure<int>("Bạn không có quyền undo phiên import này.");

        if (session.IsUndone)
            return Result.Failure<int>("Phiên import này đã được undo rồi.");

        var cutoff = session.CreatedAt.AddMinutes(30);
        if (DateTimeOffset.UtcNow > cutoff)
            return Result.Failure<int>("Đã quá thời hạn 30 phút để undo.");

        // Delete all tasks from this import session
        var tasksToDelete = await _taskRepo.GetQueryable()
            .Where(t => t.ImportSessionId == importSessionId)
            .ToListAsync(ct);

        // Delete associated task labels first
        var taskIds = tasksToDelete.Select(t => t.Id).ToList();
        var labelsToDelete = await _taskLabelRepo.GetQueryable()
            .Where(tl => taskIds.Contains(tl.TaskItemId))
            .ToListAsync(ct);

        foreach (var label in labelsToDelete)
            await _taskLabelRepo.DeleteAsync(label, ct);

        foreach (var task in tasksToDelete)
            await _taskRepo.DeleteAsync(task, ct);

        session.IsUndone = true;
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(tasksToDelete.Count);
    }

    public async Task<Result<List<ImportSessionDto>>> GetSessionsAsync(Guid projectId, CancellationToken ct = default)
    {
        var sessions = await _sessionRepo.GetQueryable()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ImportSessionDto(
                s.Id,
                s.FileName,
                s.ImportedCount,
                s.SkippedCount,
                s.IsUndone,
                !s.IsUndone && s.CreatedAt.AddMinutes(30) > DateTimeOffset.UtcNow,
                s.CreatedAt
            ))
            .ToListAsync(ct);

        return Result.Success(sessions);
    }

    // ─── Private Helpers ────────────────────────────────────────────

    private static (List<string> Headers, List<List<string>> Rows) ParseCsv(Stream stream, string delimiter)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord?.ToList() ?? [];

        var rows = new List<List<string>>();
        while (csv.Read())
        {
            var row = new List<string>();
            for (int i = 0; i < headers.Count; i++)
            {
                row.Add(csv.GetField(i) ?? string.Empty);
            }
            rows.Add(row);
        }

        return (headers, rows);
    }

    private static (List<string> Headers, List<List<string>> Rows, List<string> SheetNames) ParseXlsx(
        Stream stream, string? sheetName = null)
    {
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);

        var sheetNames = workbook.Worksheets.Select(ws => ws.Name).ToList();
        var worksheet = sheetName != null
            ? workbook.Worksheet(sheetName)
            : workbook.Worksheets.First();

        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
            return ([], [], sheetNames);

        var firstRow = usedRange.FirstRow();
        var headers = firstRow.Cells().Select(c => c.GetString().Trim()).ToList();

        var rows = new List<List<string>>();
        foreach (var row in usedRange.RowsUsed().Skip(1)) // Skip header
        {
            var rowData = new List<string>();
            for (int i = 1; i <= headers.Count; i++)
            {
                rowData.Add(row.Cell(i).GetString().Trim());
            }
            rows.Add(rowData);
        }

        return (headers, rows, sheetNames);
    }

    private static List<ColumnMappingSuggestion> AutoSuggestMappings(List<string> headers)
    {
        var suggestions = new List<ColumnMappingSuggestion>();
        var usedFields = new HashSet<string>();

        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i].Trim();
            string? suggested = null;

            foreach (var (field, keywords) in FieldKeywords)
            {
                if (usedFields.Contains(field)) continue;

                if (keywords.Any(k => header.Equals(k, StringComparison.OrdinalIgnoreCase) ||
                                       header.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    suggested = field;
                    usedFields.Add(field);
                    break;
                }
            }

            // Default: first unmapped column becomes Title if Title not yet assigned
            if (suggested == null && i == 0 && !usedFields.Contains("Title"))
            {
                suggested = "Title";
                usedFields.Add("Title");
            }

            suggestions.Add(new ColumnMappingSuggestion(i, header, suggested));
        }

        return suggestions;
    }

    private static string NormalizeStatus(string? rawStatus, HashSet<string> unmappedStatuses)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
            return "Todo";

        var trimmed = rawStatus.Trim();

        // Direct match
        if (ValidStatuses.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            return ValidStatuses.First(s => s.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        // Alias match
        if (StatusAliases.TryGetValue(trimmed, out var mapped))
            return mapped;

        // No match — fallback
        unmappedStatuses.Add(trimmed);
        return "Todo";
    }

    private static string NormalizePriority(string? rawPriority)
    {
        if (string.IsNullOrWhiteSpace(rawPriority))
            return "Medium";

        var trimmed = rawPriority.Trim();

        if (ValidPriorities.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            return ValidPriorities.First(p => p.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        if (PriorityAliases.TryGetValue(trimmed, out var mapped))
            return mapped;

        return "Medium";
    }

    private static DateTimeOffset? ParseDate(string? rawDate)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
            return null;

        string[] formats =
        [
            "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "MM/dd/yyyy",
            "dd-MM-yyyy", "MM-dd-yyyy", "yyyy-MM-ddTHH:mm:ss",
            "dd/MM/yyyy HH:mm", "MM/dd/yyyy HH:mm",
        ];

        if (DateTimeOffset.TryParseExact(rawDate.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var result))
            return result;

        if (DateTimeOffset.TryParse(rawDate.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fallback))
            return fallback;

        return null;
    }

    private static string? GetCellValue(List<string> row, Dictionary<string, int> fieldMap, string field)
    {
        if (!fieldMap.TryGetValue(field, out var index) || index >= row.Count)
            return null;
        var value = row[index];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string GenerateProjectCode(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return string.Join("", words.Take(3).Select(w => w[..1])).ToUpperInvariant();
        return name.Length >= 3 ? name[..3].ToUpperInvariant() : name.ToUpperInvariant();
    }
}
