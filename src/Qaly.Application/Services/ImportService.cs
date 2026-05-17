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

public partial class ImportService : IImportService
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
    private readonly IAiService _aiService;

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
        ["Assignee"] = ["assignee", "user", "người", "nguoi", "người làm", "người thực hiện", "thực hiện", "member", "nhân viên"],
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
        ILogger<ImportService> logger,
        IAiService aiService)
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
        _aiService = aiService;
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
            LogParseFileFailed(_logger, ex, fileName);
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
            var project = await _projectRepo.GetByIdAsync(request.ProjectId.Value, ct);
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
            await _projectRepo.AddAsync(newProject, ct);

            // Add owner as member
            await _memberRepo.AddAsync(new ProjectMember
            {
                ProjectId = newProject.Id,
                UserId = userId,
                Role = "Owner",
            }, ct);

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
            LogImportParseFailed(_logger, ex);
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
                .Select(t => t.Title)
                .ToListAsync(ct))
                .Select(NormalizeTitle)
                .ToHashSet(StringComparer.Ordinal);
        }

        // Create import session
        var session = new ImportSession
        {
            ProjectId = projectId,
            UserId = userId,
            FileName = fileName,
            TotalRows = allRows.Count,
        };
        await _sessionRepo.AddAsync(session, ct);

        // Load project members to map Assignee
        var projectMembers = await _memberRepo.GetQueryable()
            .Include(m => m.User)
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.User)
            .ToListAsync(ct);

        var defaultAssigneeId = request.DefaultAssigneeId;
        if (defaultAssigneeId.HasValue)
        {
            var canUseDefaultAssignee = defaultAssigneeId.Value == userId ||
                await _memberRepo.GetQueryable()
                    .AnyAsync(m => m.ProjectId == projectId && m.UserId == defaultAssigneeId.Value, ct);

            if (!canUseDefaultAssignee)
            {
                return Result.Failure<ImportResult>("Người phụ trách mặc định không thuộc dự án.");
            }
        }

        // Create quick lookup for Assignee (by Email or FullName)
        var memberLookup = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in projectMembers)
        {
            if (m != null)
            {
                if (!string.IsNullOrWhiteSpace(m.Email))
                    memberLookup[m.Email] = m.Id;
                if (!string.IsNullOrWhiteSpace(m.FullName))
                    memberLookup[m.FullName] = m.Id;
            }
        }

        int importedCount = 0;
        int skippedCount = 0;
        int newLabelsCreated = 0;
        var unmappedStatuses = new HashSet<string>();
        var statusDistribution = new Dictionary<string, int>();
        var skippedRowsList = new List<SkippedRowDto>();
        var tasksToInsert = new List<TaskItem>();
        var taskLabelsToInsert = new List<TaskLabel>();

        // Lấy max SortOrder hiện tại của từng cột (status) để tối ưu hiển thị dòng
        var maxSortOrders = await _taskRepo.GetQueryable()
            .Where(t => t.ProjectId == projectId)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, MaxSort = g.Max(t => t.SortOrder) })
            .ToDictionaryAsync(x => x.Status, x => x.MaxSort, ct);

        var random = new Random();

        // 1. Phân loại AI trước khi import (nếu được bật)
        var aiCategorizationDict = new Dictionary<int, AiCategorizationResult>();
        if (request.EnableAiCategorization)
        {
            var tasksToCategorize = new List<AiCategorizationRequest>();
            for (int i = 0; i < allRows.Count; i++)
            {
                var row = allRows[i];
                int rowIndex = request.FirstRowIsHeader ? i + 2 : i + 1;
                var title = GetCellValue(row, fieldMap, "Title")?.Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                var rawStatus = GetCellValue(row, fieldMap, "Status");
                var rawPriority = GetCellValue(row, fieldMap, "Priority");
                
                // Nếu thiếu Status hoặc Priority, thử đoán bằng Heuristic trước
                if (string.IsNullOrWhiteSpace(rawStatus) || string.IsNullOrWhiteSpace(rawPriority))
                {
                    var heuristic = HeuristicGuess(title);
                    
                    bool stillNeedStatus = string.IsNullOrWhiteSpace(rawStatus) && string.IsNullOrWhiteSpace(heuristic.Status);
                    bool stillNeedPriority = string.IsNullOrWhiteSpace(rawPriority) && string.IsNullOrWhiteSpace(heuristic.Priority) && string.IsNullOrWhiteSpace(request.DefaultPriority);
                    
                    if (stillNeedStatus || stillNeedPriority)
                    {
                        var desc = GetCellValue(row, fieldMap, "Description");
                        tasksToCategorize.Add(new AiCategorizationRequest(rowIndex, title, desc));
                    }
                    else
                    {
                        // Đã đoán được bằng Rule-based Heuristic, không cần gọi LLM
                        aiCategorizationDict[rowIndex] = new AiCategorizationResult(
                            rowIndex, 
                            heuristic.Status ?? string.Empty, 
                            heuristic.Priority ?? string.Empty, 
                            heuristic.Labels.ToArray()
                        );
                    }
                }
            }

            if (tasksToCategorize.Count > 0)
            {
                // Limit maximum tasks to send to AI per import to prevent long waiting times and spam
                const int maxAiTasks = 100;
                var aiTasksToProcess = tasksToCategorize.Take(maxAiTasks).ToList();
                
                // Chunk into smaller batches so Ollama context window doesn't overflow
                const int batchSize = 20;
                for (int j = 0; j < aiTasksToProcess.Count; j += batchSize)
                {
                    var chunk = aiTasksToProcess.Skip(j).Take(batchSize).ToList();
                    var chunkResults = await _aiService.CategorizeTasksBatchAsync(chunk);
                    foreach (var res in chunkResults)
                    {
                        aiCategorizationDict[res.RowIndex] = res;
                    }

                    // Wait 2 seconds between chunks to let Ollama cool down
                    if (j + batchSize < aiTasksToProcess.Count)
                    {
                        await Task.Delay(2000, ct);
                    }
                }
            }
        }

        for (int i = 0; i < allRows.Count; i++)
        {
            var row = allRows[i];
            int rowIndex = request.FirstRowIsHeader ? i + 2 : i + 1;
            // Get title
            var title = GetCellValue(row, fieldMap, "Title")?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                skippedCount++;
                skippedRowsList.Add(new SkippedRowDto(rowIndex, "Thiếu tiêu đề (Title)"));
                continue;
            }

            // Duplicate check
            if (request.SkipDuplicates && existingTitles!.Contains(NormalizeTitle(title)))
            {
                skippedCount++;
                skippedRowsList.Add(new SkippedRowDto(rowIndex, "Trùng lặp tiêu đề"));
                continue;
            }

            // Parse fields
            var description = GetCellValue(row, fieldMap, "Description");
            var rawStatus = GetCellValue(row, fieldMap, "Status");
            var rawPriority = GetCellValue(row, fieldMap, "Priority");
            var rawDueDate = GetCellValue(row, fieldMap, "DueDate");
            var rawHours = GetCellValue(row, fieldMap, "EstimatedHours");
            var rawLabels = GetCellValue(row, fieldMap, "Labels");
            var rawAssignee = GetCellValue(row, fieldMap, "Assignee")?.Trim();

            // Normalize status
            var status = NormalizeStatus(rawStatus, unmappedStatuses);

            // Normalize priority
            var priority = string.IsNullOrWhiteSpace(rawPriority) && !string.IsNullOrWhiteSpace(request.DefaultPriority)
                ? request.DefaultPriority
                : NormalizePriority(rawPriority);

            // Apply AI classification if applicable
            var aiLabels = new List<string>();
            if (aiCategorizationDict.TryGetValue(rowIndex, out var aiResult))
            {
                if (string.IsNullOrWhiteSpace(rawStatus) && !string.IsNullOrWhiteSpace(aiResult.Status))
                    status = NormalizeStatus(aiResult.Status, unmappedStatuses);
                    
                if (string.IsNullOrWhiteSpace(rawPriority) && !string.IsNullOrWhiteSpace(aiResult.Priority))
                    priority = NormalizePriority(aiResult.Priority);
                    
                if (aiResult.Labels != null && aiResult.Labels.Length > 0)
                {
                    aiLabels.AddRange(aiResult.Labels);
                }
            }

            // Parse due date
            DateTimeOffset? dueDate = ParseDate(rawDueDate);

            // Parse estimated hours
            int? estimatedHours = null;
            if (int.TryParse(rawHours, out var hours))
                estimatedHours = hours;

            // Map Assignee
            Guid? assigneeId = null;
            if (!string.IsNullOrWhiteSpace(rawAssignee) && memberLookup.TryGetValue(rawAssignee, out var matchedUserId))
            {
                assigneeId = matchedUserId;
            }
            else if (string.IsNullOrWhiteSpace(rawAssignee) && defaultAssigneeId.HasValue)
            {
                assigneeId = defaultAssigneeId.Value;
            }
            else if (string.IsNullOrWhiteSpace(rawAssignee) && request.AssignToMeIfEmpty)
            {
                assigneeId = userId;
            }

            // Tính toán SortOrder để task nằm ở cuối cột (tối ưu dòng)
            if (!maxSortOrders.TryGetValue(status, out int currentSortOrder))
            {
                currentSortOrder = 0;
            }
            currentSortOrder++;
            maxSortOrders[status] = currentSortOrder;

            // Create task
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = description,
                Status = status,
                Priority = priority,
                DueDate = dueDate,
                EstimatedHours = estimatedHours,
                ProjectId = projectId,
                ReporterId = userId,
                AssigneeId = assigneeId,
                ImportSessionId = session.Id,
                SortOrder = currentSortOrder
            };
            tasksToInsert.Add(task);

            // Handle labels
            var labelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(rawLabels))
            {
                var csvLabels = rawLabels.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l));
                foreach (var l in csvLabels) labelNames.Add(l);
            }
            foreach (var al in aiLabels)
            {
                if (!string.IsNullOrWhiteSpace(al)) labelNames.Add(al.Trim());
            }

            if (labelNames.Count > 0)
            {
                foreach (var labelName in labelNames)
                {
                    var existing = existingLabels.FirstOrDefault(
                        l => l.Name.Equals(labelName, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        existing = new ProjectLabel
                        {
                            Id = Guid.NewGuid(),
                            Name = labelName,
                            Color = LabelColors[random.Next(LabelColors.Length)],
                            ProjectId = projectId,
                        };
                        await _labelRepo.AddAsync(existing, ct);
                        existingLabels.Add(existing);
                        newLabelsCreated++;
                    }

                    taskLabelsToInsert.Add(new TaskLabel
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
                existingTitles!.Add(NormalizeTitle(title));
        }

        // Bulk insert
        if (tasksToInsert.Count > 0)
            await _taskRepo.AddRangeAsync(tasksToInsert, ct);

        if (taskLabelsToInsert.Count > 0)
            await _taskLabelRepo.AddRangeAsync(taskLabelsToInsert, ct);

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
            StatusDistribution: statusDistribution,
            SkippedRows: skippedRowsList
        ));
    }

    public async Task<Result<int>> UndoImportAsync(Guid importSessionId, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(importSessionId, ct);
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

    private static (string? Status, string? Priority, List<string> Labels) HeuristicGuess(string title)
    {
        string? status = null;
        string? priority = null;
        var labels = new List<string>();
        
        var lowerTitle = title.ToLowerInvariant();

        // 1. Guess Priority & Labels
        if (lowerTitle.Contains("lỗi") || lowerTitle.Contains("bug") || lowerTitle.Contains("crash") || lowerTitle.Contains("fix") || lowerTitle.Contains("sửa"))
        {
            labels.Add("Bug");
            priority ??= "High";
        }
        else if (lowerTitle.Contains("thêm") || lowerTitle.Contains("tạo") || lowerTitle.Contains("feature") || lowerTitle.Contains("chức năng"))
        {
            labels.Add("Feature");
            priority ??= "Medium";
        }

        if (lowerTitle.Contains("gấp") || lowerTitle.Contains("urgent") || lowerTitle.Contains("asap") || lowerTitle.Contains("ngay"))
        {
            priority = "Critical";
        }
        else if (lowerTitle.Contains("hotfix"))
        {
            priority = "Critical";
            labels.Add("Hotfix");
        }

        // 2. Guess Status
        if (lowerTitle.Contains("hoàn thành") || lowerTitle.Contains("xong") || lowerTitle.Contains("[done]"))
        {
            status = "Done";
        }
        else if (lowerTitle.Contains("đang làm") || lowerTitle.Contains("đang xử lý") || lowerTitle.Contains("[wip]"))
        {
            status = "InProgress";
        }
        else if (lowerTitle.Contains("kiểm tra") || lowerTitle.Contains("review") || lowerTitle.Contains("test"))
        {
            status = "InReview";
        }

        return (status, priority, labels);
    }

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

    private static string NormalizeTitle(string title)
        => title.Trim().ToLowerInvariant();

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Error parsing file {FileName}")]
    private static partial void LogParseFileFailed(ILogger logger, Exception exception, string fileName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error parsing file during import")]
    private static partial void LogImportParseFailed(ILogger logger, Exception exception);
}
