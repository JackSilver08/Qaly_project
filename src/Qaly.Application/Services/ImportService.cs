using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Import;
using Qaly.Application.Services.Tasks;
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
    private readonly ITaskAccessPolicy _taskAccessPolicy;

    private const int MaxRows = 2000;
    private const int PreviewRowCount = 5;
    private const int SortOrderStep = 1000;
    private const int MaxTaskTitleLength = 300;
    private const int MaxProjectNameLength = 200;
    private const int MaxLabelNameLength = 80;
    private const int MaxImportFileNameLength = 256;
    private const int MaxHours = 100000;

    private static readonly string[] ValidExtensions = [".csv", ".xlsx", ".tsv", ".txt", ".dsv", ".psv", ".json"];
    private static readonly string[] ValidStatuses = ["Todo", "InProgress", "OnHold", "InReview", "Done", "Cancelled"];
    private static readonly string[] ValidPriorities = ["Low", "Medium", "High", "Critical"];
    private static readonly HashSet<string> ValidTargetFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Title", "Description", "Status", "Priority", "DueDate", "EstimatedHours", "Labels", "Assignee", "Skip"
    };

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
        ["cancelled"] = "Cancelled", ["canceled"] = "Cancelled", ["cancel"] = "Cancelled", ["huy"] = "Cancelled",
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
        IAiService aiService,
        ITaskAccessPolicy taskAccessPolicy)
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
        _taskAccessPolicy = taskAccessPolicy;
    }

    public async Task<Result<ParsedFileResult>> ParseFileAsync(Stream fileStream, string fileName, string? sheetName = null, bool firstRowIsHeader = true, CancellationToken ct = default)
    {
        var safeFileName = NormalizeClientFileName(fileName);
        if (safeFileName.Length > MaxImportFileNameLength)
            return Result.Failure<ParsedFileResult>("Tên file vượt quá giới hạn 256 ký tự.", 400);

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!ValidExtensions.Contains(ext))
            return Result.Failure<ParsedFileResult>($"Định dạng file không hỗ trợ. Chỉ chấp nhận: {string.Join(", ", ValidExtensions)}");

        try
        {
            List<string> headers;
            List<List<string>> allRows;
            List<string>? sheetNames = null;

            if (ext == ".xlsx")
            {
                (headers, allRows, sheetNames) = ParseXlsx(fileStream, sheetName, firstRowIsHeader);
            }
            else if (ext == ".json")
            {
                (headers, allRows) = ParseJson(fileStream, firstRowIsHeader);
            }
            else
            {
                var delimiter = GetDelimitedTextSeparator(fileStream, ext);
                (headers, allRows) = ParseCsv(fileStream, delimiter, firstRowIsHeader);
            }

            if (headers.Count == 0)
                return Result.Failure<ParsedFileResult>("File không có dữ liệu hoặc không đọc được header.");

            if (allRows.Count > MaxRows)
                return Result.Failure<ParsedFileResult>($"File vượt quá giới hạn {MaxRows} dòng. Hiện có {allRows.Count} dòng.");

            var previewRows = allRows.Take(PreviewRowCount).ToList();
            var suggestions = AutoSuggestMappings(headers);

            return Result.Success(new ParsedFileResult(
                FileName: safeFileName,
                Headers: headers,
                PreviewRows: previewRows,
                TotalRowCount: allRows.Count,
                Suggestions: suggestions,
                SheetNames: sheetNames
            ));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogParseFileFailed(_logger, ex, safeFileName);
            return Result.Failure<ParsedFileResult>(
                "Không thể đọc file. Hãy kiểm tra định dạng, nội dung và thử lại.",
                400);
        }
    }

    public async Task<Result<ImportResult>> ExecuteImportAsync(
        Stream fileStream, string fileName, ImportRequest request, CancellationToken ct = default)
    {
        var safeFileName = NormalizeClientFileName(fileName);
        if (safeFileName.Length > MaxImportFileNameLength)
            return Result.Failure<ImportResult>("Tên file vượt quá giới hạn 256 ký tự.", 400);

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
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
            if (!await _taskAccessPolicy.CanManageProjectAsync(project.Id, project.OwnerId, ct))
                return Result.Forbidden<ImportResult>("Bạn không có quyền import vào dự án này.");
            projectId = project.Id;
        }
        else
        {
            // Flow 1: Create new project
            var projectName = request.NewProjectName?.Trim();
            if (string.IsNullOrWhiteSpace(projectName))
                projectName = Path.GetFileNameWithoutExtension(safeFileName);
            if (projectName.Length > MaxProjectNameLength)
                return Result.Failure<ImportResult>("Tên Project không được vượt quá 200 ký tự.", 400);

            var newProject = new Project
            {
                Name = projectName,
                Code = await GenerateUniqueProjectCodeAsync(projectName, ct),
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
                (headers, allRows, _) = ParseXlsx(fileStream, request.SheetName, request.FirstRowIsHeader);
            }
            else if (ext == ".json")
            {
                (headers, allRows) = ParseJson(fileStream, request.FirstRowIsHeader);
            }
            else
            {
                var delimiter = GetDelimitedTextSeparator(fileStream, ext);
                (headers, allRows) = ParseCsv(fileStream, delimiter, request.FirstRowIsHeader);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogImportParseFailed(_logger, ex);
            return Result.Failure<ImportResult>(
                "Không thể đọc file. Hãy kiểm tra định dạng, nội dung và thử lại.",
                400);
        }

        if (allRows.Count > MaxRows)
            return Result.Failure<ImportResult>($"File vượt quá giới hạn {MaxRows} dòng. Hiện có {allRows.Count} dòng.");

        if (request.Mappings is not { Count: > 0 })
            return Result.Failure<ImportResult>("Phải cấu hình mapping cột trước khi import.", 400);

        var unknownTargetField = request.Mappings.FirstOrDefault(mapping =>
            string.IsNullOrWhiteSpace(mapping.TargetField) || !ValidTargetFields.Contains(mapping.TargetField));
        if (unknownTargetField != null)
            return Result.Failure<ImportResult>("Mapping chứa field đích không được hỗ trợ.", 400);

        var activeMappings = request.Mappings
            .Where(m => m.TargetField != "Skip")
            .ToList();

        var invalidMapping = activeMappings.FirstOrDefault(m => m.ColumnIndex < 0 || m.ColumnIndex >= headers.Count);
        if (invalidMapping != null)
            return Result.Failure<ImportResult>("Mapping cột không hợp lệ. Vui lòng parse lại file và kiểm tra mapping.");

        var duplicateFields = activeMappings
            .GroupBy(m => m.TargetField, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateFields.Count > 0)
            return Result.Failure<ImportResult>($"Mỗi field chỉ được map một lần. Bị trùng: {string.Join(", ", duplicateFields)}.");

        // Build field index map from mappings
        var fieldMap = activeMappings.ToDictionary(m => m.TargetField, m => m.ColumnIndex, StringComparer.OrdinalIgnoreCase);

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
            FileName = safeFileName,
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
        int failedCount = 0;
        int duplicateSkippedCount = 0;
        int newLabelsCreated = 0;
        var unmappedStatuses = new HashSet<string>();
        var unmappedPriorities = new HashSet<string>();
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
                failedCount++;
                skippedRowsList.Add(new SkippedRowDto(rowIndex, "Thiếu tiêu đề (Title)", "Failed"));
                continue;
            }
            if (title.Length > MaxTaskTitleLength)
            {
                skippedCount++;
                failedCount++;
                skippedRowsList.Add(new SkippedRowDto(
                    rowIndex,
                    $"Tiêu đề vượt quá {MaxTaskTitleLength} ký tự",
                    "Failed"));
                continue;
            }

            // Duplicate check
            if (request.SkipDuplicates && existingTitles!.Contains(NormalizeTitle(title)))
            {
                skippedCount++;
                duplicateSkippedCount++;
                skippedRowsList.Add(new SkippedRowDto(rowIndex, "Trùng lặp tiêu đề", "Duplicate"));
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
            var status = string.IsNullOrWhiteSpace(rawStatus) && !string.IsNullOrWhiteSpace(request.DefaultStatus)
                ? NormalizeStatus(request.DefaultStatus, unmappedStatuses)
                : NormalizeStatus(rawStatus, unmappedStatuses);

            // Normalize priority
            var priority = string.IsNullOrWhiteSpace(rawPriority) && !string.IsNullOrWhiteSpace(request.DefaultPriority)
                ? NormalizePriority(request.DefaultPriority, unmappedPriorities)
                : NormalizePriority(rawPriority, unmappedPriorities);

            // Apply AI classification if applicable
            var aiLabels = new List<string>();
            if (aiCategorizationDict.TryGetValue(rowIndex, out var aiResult))
            {
                if (string.IsNullOrWhiteSpace(rawStatus) && string.IsNullOrWhiteSpace(request.DefaultStatus) && !string.IsNullOrWhiteSpace(aiResult.Status))
                    status = NormalizeStatus(aiResult.Status, unmappedStatuses);
                    
                if (string.IsNullOrWhiteSpace(rawPriority) && !string.IsNullOrWhiteSpace(aiResult.Priority))
                    priority = NormalizePriority(aiResult.Priority, unmappedPriorities);
                    
                if (aiResult.Labels != null && aiResult.Labels.Length > 0)
                {
                    aiLabels.AddRange(aiResult.Labels);
                }
            }

            // Parse due date
            DateTimeOffset? dueDate = ParseDate(rawDueDate);
            if (!string.IsNullOrWhiteSpace(rawDueDate) && dueDate == null)
            {
                skippedCount++;
                failedCount++;
                skippedRowsList.Add(new SkippedRowDto(rowIndex, "Hạn chót không đúng định dạng ngày", "Failed"));
                continue;
            }

            // Parse estimated hours
            int? estimatedHours = null;
            if (!string.IsNullOrWhiteSpace(rawHours))
            {
                if (!int.TryParse(rawHours, out var hours) || hours is < 0 or > MaxHours)
                {
                    skippedCount++;
                    failedCount++;
                    skippedRowsList.Add(new SkippedRowDto(
                        rowIndex,
                        $"Giờ ước tính phải là số từ 0 đến {MaxHours:N0}",
                        "Failed"));
                    continue;
                }
                estimatedHours = hours;
            }

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
            currentSortOrder += SortOrderStep;
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
            if (assigneeId.HasValue)
            {
                task.Assignees.Add(new TaskAssignment
                {
                    UserId = assigneeId.Value,
                    AssignedByUserId = userId
                });
            }
            tasksToInsert.Add(task);

            // Handle labels
            var labelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(rawLabels))
            {
                var csvLabels = rawLabels.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l) && l.Length <= MaxLabelNameLength);
                foreach (var l in csvLabels) labelNames.Add(l);
            }
            foreach (var al in aiLabels)
            {
                if (!string.IsNullOrWhiteSpace(al) && al.Trim().Length <= MaxLabelNameLength)
                    labelNames.Add(al.Trim());
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
            FailedCount: failedCount,
            DuplicateSkippedCount: duplicateSkippedCount,
            NewLabelsCreated: newLabelsCreated,
            UnmappedStatuses: unmappedStatuses.ToList(),
            UnmappedPriorities: unmappedPriorities.ToList(),
            StatusDistribution: statusDistribution,
            SkippedRows: skippedRowsList
        ));
    }

    public async Task<Result<int>> UndoImportAsync(Guid importSessionId, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(importSessionId, ct);
        if (session == null)
            return Result.Failure<int>("Không tìm thấy phiên import.");

        var project = await _projectRepo.GetByIdAsync(session.ProjectId, ct);
        if (project == null)
            return Result.Failure<int>("Không tìm thấy dự án của phiên import.", 404);

        if (!await _taskAccessPolicy.CanManageProjectAsync(project.Id, project.OwnerId, ct))
            return Result.Forbidden<int>("Bạn không còn quyền quản lý dự án của phiên import này.");

        if (session.UserId != _currentUserService.UserId)
            return Result.Forbidden<int>("Bạn không có quyền undo phiên import này.");

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
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
            return Result.Failure<List<ImportSessionDto>>("Không tìm thấy dự án.", 404);

        if (!await _taskAccessPolicy.CanAccessProjectAsync(project.Id, project.OwnerId, ct))
            return Result.Forbidden<List<ImportSessionDto>>();

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

    private static (List<string> Headers, List<List<string>> Rows) ParseCsv(Stream stream, string delimiter, bool firstRowIsHeader)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = firstRowIsHeader,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var csv = new CsvReader(reader, config);
        var headers = new List<string>();
        var rows = new List<List<string>>();
        if (firstRowIsHeader)
        {
            csv.Read();
            csv.ReadHeader();
            headers = csv.HeaderRecord?.ToList() ?? [];

            while (csv.Read())
            {
                rows.Add(ReadCsvRecord(csv, headers.Count));
            }
        }
        else
        {
            while (csv.Read())
            {
                var record = csv.Parser.Record?.ToList() ?? [];
                if (headers.Count == 0)
                    headers = Enumerable.Range(1, record.Count).Select(i => $"Column {i}").ToList();
                rows.Add(PadRow(record, headers.Count));
            }
        }

        return (headers, rows);
    }

    private static string GetDelimitedTextSeparator(Stream stream, string extension)
    {
        if (extension == ".tsv")
            return "\t";
        if (extension == ".psv")
            return "|";
        if (extension != ".txt" && extension != ".dsv")
            return ",";

        var originalPosition = stream.CanSeek ? stream.Position : 0;
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string? sample = null;
        while (!reader.EndOfStream && string.IsNullOrWhiteSpace(sample))
        {
            sample = reader.ReadLine();
        }

        if (stream.CanSeek)
            stream.Position = originalPosition;

        if (string.IsNullOrEmpty(sample))
            return ",";

        var candidates = new Dictionary<string, int>
        {
            ["\t"] = sample.Count(c => c == '\t'),
            [","] = sample.Count(c => c == ','),
            [";"] = sample.Count(c => c == ';'),
            ["|"] = sample.Count(c => c == '|')
        };

        var best = candidates.OrderByDescending(item => item.Value).First();
        return best.Value > 0 ? best.Key : ",";
    }

    private static (List<string> Headers, List<List<string>> Rows) ParseJson(Stream stream, bool firstRowIsHeader)
    {
        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        var root = NormalizeJsonRoot(document.RootElement);

        if (root.ValueKind == JsonValueKind.Array)
        {
            var elements = root.EnumerateArray().ToList();
            if (elements.Count == 0)
                return ([], []);

            if (elements.All(element => element.ValueKind == JsonValueKind.Object))
                return ParseJsonObjects(elements);

            if (elements.All(element => element.ValueKind == JsonValueKind.Array))
                return ParseJsonArrays(elements, firstRowIsHeader);
        }

        if (root.ValueKind == JsonValueKind.Object)
            return ParseJsonObjects([root]);

        return ([], []);
    }

    private static JsonElement NormalizeJsonRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return root;

        foreach (var propertyName in new[] { "tasks", "items", "rows", "data" })
        {
            if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array)
                return value;
        }

        return root;
    }

    private static (List<string> Headers, List<List<string>> Rows) ParseJsonObjects(List<JsonElement> objects)
    {
        var headers = new List<string>();
        var usedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in objects)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (usedHeaders.Add(property.Name))
                    headers.Add(property.Name);
            }
        }

        var rows = objects
            .Select(element => headers.Select(header =>
                element.TryGetProperty(header, out var value) ? JsonValueToCell(value) : string.Empty).ToList())
            .ToList();

        return (headers, rows);
    }

    private static (List<string> Headers, List<List<string>> Rows) ParseJsonArrays(List<JsonElement> arrays, bool firstRowIsHeader)
    {
        var rawRows = arrays
            .Select(element => element.EnumerateArray().Select(JsonValueToCell).ToList())
            .ToList();
        if (rawRows.Count == 0)
            return ([], []);

        var columnCount = rawRows.Max(row => row.Count);
        List<string> headers;
        IEnumerable<List<string>> dataRows;
        if (firstRowIsHeader)
        {
            headers = PadRow(rawRows[0], columnCount);
            dataRows = rawRows.Skip(1);
        }
        else
        {
            headers = Enumerable.Range(1, columnCount).Select(i => $"Column {i}").ToList();
            dataRows = rawRows;
        }

        return (headers, dataRows.Select(row => PadRow(row, columnCount)).ToList());
    }

    private static string JsonValueToCell(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };

    private static List<string> ReadCsvRecord(CsvReader csv, int columnCount)
    {
        var row = new List<string>();
        for (int i = 0; i < columnCount; i++)
        {
            row.Add(csv.GetField(i) ?? string.Empty);
        }
        return row;
    }

    private static List<string> PadRow(List<string> row, int columnCount)
    {
        if (row.Count >= columnCount)
            return row.Take(columnCount).ToList();

        var padded = new List<string>(row);
        while (padded.Count < columnCount)
            padded.Add(string.Empty);
        return padded;
    }

    private static (List<string> Headers, List<List<string>> Rows, List<string> SheetNames) ParseXlsx(
        Stream stream, string? sheetName = null, bool firstRowIsHeader = true)
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
        var columnCount = usedRange.ColumnCount();
        var headers = firstRowIsHeader
            ? firstRow.Cells(1, columnCount).Select(c => c.GetString().Trim()).ToList()
            : Enumerable.Range(1, columnCount).Select(i => $"Column {i}").ToList();

        var rows = new List<List<string>>();
        foreach (var row in usedRange.RowsUsed().Skip(firstRowIsHeader ? 1 : 0))
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

            if (IsGeneratedColumnHeader(header))
            {
                if (i == 0 && !usedFields.Contains("Title"))
                {
                    suggested = "Title";
                    usedFields.Add("Title");
                }
            }
            else
            {
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

    private static bool IsGeneratedColumnHeader(string header)
        => header.StartsWith("Column ", StringComparison.OrdinalIgnoreCase) &&
           int.TryParse(header["Column ".Length..], out _);

    private static string NormalizeStatus(string? rawStatus, HashSet<string> unmappedStatuses)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
            return "Todo";

        var trimmed = rawStatus.Trim();

        if (TaskStatusRules.IsValidStatus(trimmed))
            return TaskStatusRules.NormalizeStatus(trimmed);

        // Alias match
        if (StatusAliases.TryGetValue(trimmed, out var mapped))
            return mapped;

        // No match — fallback
        unmappedStatuses.Add(trimmed);
        return "Todo";
    }

    private static string NormalizePriority(string? rawPriority, HashSet<string> unmappedPriorities)
    {
        if (string.IsNullOrWhiteSpace(rawPriority))
            return "Medium";

        var trimmed = rawPriority.Trim();

        if (TaskStatusRules.IsValidPriority(trimmed))
            return TaskStatusRules.NormalizePriority(trimmed);

        if (PriorityAliases.TryGetValue(trimmed, out var mapped))
            return mapped;

        unmappedPriorities.Add(trimmed);
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

    private static string NormalizeClientFileName(string? fileName)
    {
        var normalized = (fileName ?? string.Empty).Replace('\\', '/');
        var safeName = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(safeName) ? "import" : safeName.Trim();
    }

    private static string GenerateProjectCode(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return string.Join("", words.Take(3).Select(w => w[..1])).ToUpperInvariant();
        return name.Length >= 3 ? name[..3].ToUpperInvariant() : name.ToUpperInvariant();
    }

    private async Task<string> GenerateUniqueProjectCodeAsync(string name, CancellationToken ct)
    {
        var baseCode = GenerateProjectCode(name);
        var prefix = $"{baseCode}-";
        var existingCodes = (await _projectRepo.GetQueryable()
            .Where(project => project.Code == baseCode || project.Code.StartsWith(prefix))
            .Select(project => project.Code)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingCodes.Contains(baseCode))
            return baseCode;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseCode}-{suffix}";
            if (!existingCodes.Contains(candidate))
                return candidate;
        }
    }

    private static string NormalizeTitle(string title)
        => title.Trim().ToLowerInvariant();

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Error parsing file {FileName}")]
    private static partial void LogParseFileFailed(ILogger logger, Exception exception, string fileName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error parsing file during import")]
    private static partial void LogImportParseFailed(ILogger logger, Exception exception);
}
