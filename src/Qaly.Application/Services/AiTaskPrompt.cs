using System.Text;
using System.Text.RegularExpressions;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

/// <summary>Separates task content from assignment instructions. Never grants permissions or writes data.</summary>
public sealed record AiTaskPrompt(string Content, string? Assignee, bool LeaveUnassigned, string? Error)
{
    private const string Quotes = "\"[^\"]*\"|“[^”]*”|‘[^’]*’|`[^`]*`|(?<![\\p{L}\\p{N}])'[^']*'(?![\\p{L}\\p{N}])";

    public static AiTaskPrompt Parse(string message)
    {
        var raw = message.Normalize(NormalizationForm.FormC).Trim();
        var masked = Regex.Replace(raw, Quotes, match => new string(' ', match.Length));
        var text = Fold(masked);
        var leaveUnassigned = Regex.IsMatch(text, @"\b(?:de chua giao|giu chua giao|khong giao|chua giao|unassigned|do not assign|don't assign)\b");
        var instructions = Regex.Matches(text,
            @"\b(?:(?:giao(?!\s+(?:dien|thong|tiep|dich)\b)|gan|phan cong|assign)\b[^;:\r\n!?]{0,70}?\b(?:cho|to)|cho\s+(?:nguoi|nhan vien|thanh vien|member))(?=\s)");
        string? assignee = null;
        string? error = null;
        var content = raw;
        foreach (Match instruction in instructions.Cast<Match>().Reverse())
        {
            var prefix = text[..instruction.Index];
            // Bare "cho người" is an assignment header only immediately after the task noun.
            // Inside business content it can describe an audience, e.g. "giao diện cho người dùng".
            if (Regex.IsMatch(instruction.Value, @"^cho\s") &&
                !Regex.IsMatch(prefix, @"\b(?:tasks?|nhiem vu|cong viec)\s*$")) continue;
            var start = instruction.Index + instruction.Length;
            while (start < raw.Length && char.IsWhiteSpace(raw[start])) start++;
            var rest = raw[start..];
            var quoted = Regex.Match(rest, "^\\s*(?:\"(?<name>[^\"]+)\"|“(?<name>[^”]+)”|`(?<name>[^`]+)`|'(?<name>[^']+)')");
            var end = quoted.Success ? quoted.Length : rest.Length;
            if (!quoted.Success)
            {
                var stop = Regex.Match(Fold(rest), @"[,;:\r\n.!?]|\s+\b(?:trong|thuoc|voi|han|deadline|mo|chua|truoc|sau|nhe|nha|giup|for sprint|in sprint|with|due|before|va mo|and show)\b");
                if (stop.Success) end = stop.Index;
            }
            var name = quoted.Success ? quoted.Groups["name"].Value : rest[..end].Trim();
            name = Regex.Replace(name, @"^(?:người|nguoi|nhân viên|nhan vien|thành viên|thanh vien|member)\s+", "", RegexOptions.IgnoreCase);
            var negation = Regex.Match(prefix, @"\b(?:khong|chua|dung|do not|don't)\s*$");
            if (!negation.Success)
            {
                if (assignee != null || leaveUnassigned)
                    error = "Yêu cầu có nhiều cách giao việc. Hãy chỉ rõ một người cho cả batch, hoặc chọn từng người trên card review.";
                assignee = name.Length > 0 ? name : string.Empty;
            }
            var removeStart = negation.Success ? negation.Index : instruction.Index;
            var connector = Regex.Match(text[..removeStart], @"\b(?:va|roi|sau do|and|then)\s*$");
            if (connector.Success) removeStart = connector.Index;
            content = content.Remove(removeStart, start + end - removeStart);
        }

        var foldedContent = Fold(Regex.Replace(content, Quotes, match => new string(' ', match.Length)));
        var create = Regex.Match(foldedContent,
            @"\b(?:tao|them|soan|create|draft|add)\b[^;:\r\n!?]{0,48}?\b(?:tasks?|nhiem vu|cong viec)\b");
        if (create.Success) content = content[(create.Index + create.Length)..].Trim();
        // Explicit metadata and review instructions must not become a business deliverable.
        var control = Regex.Match(Fold(Regex.Replace(content, Quotes, match => new string(' ', match.Length))),
            @"(?:^|[;.\r\n]|\s+)(?:mo ban nhap|mo card|chua ghi|khong ghi|chi luu|cho xac nhan|moi task|moi nhiem vu|han chot|deadline|de chua giao|giu chua giao|unassigned|show a draft|do not save|wait for confirmation)\b");
        if (control.Success) content = content[..control.Index];
        content = content.Trim(' ', ',', ';', ':', '.', '-');
        var heading = Regex.Match(Fold(content), @"^(?:(?:cho|trong|for|in)\s+sprint\s+\d+\s*[:,\-]?\s*|(?:ten|tieu de|noi dung|title|content)\s*:?\s*)");
        if (heading.Success) content = content[heading.Length..].Trim();
        if (Regex.IsMatch(Fold(content), @"^(?:(?:moi|new)\s*)?(?:(?:cho|trong|for|in)\s+(?:sprint\s+\d+|project|du an)(?:\s+(?:dang chon|hien tai|nay))?)?\s*$"))
            content = string.Empty;
        return new(content, assignee, leaveUnassigned, error);
    }

    public string? ValidateAssignmentContent()
        => Error ?? (Assignee != null && string.IsNullOrWhiteSpace(Content)
            ? "Bạn muốn tạo task về công việc gì? Hãy gửi lại yêu cầu đầy đủ theo mẫu: Tạo 3 task: [việc 1], [việc 2], [việc 3]; giao cho [họ tên]. Số lượng và người nhận không phải nội dung task."
            : null);

    public static (Guid? Id, string? Error) ResolveAssignee(string? query, IEnumerable<(Guid Id, string Name)> members)
    {
        if (query == null) return (null, null);
        var normalized = AiPromptLanguage.Normalize(query).Trim(' ', '@', '"', '\'', '“', '”');
        var candidates = members.DistinctBy(member => member.Id).ToArray();
        var exact = candidates.Where(member => AiPromptLanguage.Normalize(member.Name) == normalized).ToArray();
        var matched = exact.Length > 0 ? exact : candidates.Where(member => normalized.Length > 0 &&
            AiPromptLanguage.ContainsAny(AiPromptLanguage.Normalize(member.Name), normalized)).ToArray();
        return matched.Length switch
        {
            1 => (matched[0].Id, null),
            > 1 => (null, "Tên người nhận khớp nhiều thành viên trong Project. Hãy ghi họ tên đầy đủ hoặc chọn người trên card; Qaly chưa tự chọn."),
            _ => (null, "Không tìm thấy người nhận trong danh sách thành viên được phép giao việc của Project. Hãy kiểm tra họ tên hoặc thêm thành viên vào Project trước.")
        };
    }

    public IReadOnlyList<string> TaskTitles(int? count)
    {
        var quoted = Regex.Match(Content, "^(?:\"(?<title>[^\"]+)\"|“(?<title>[^”]+)”|`(?<title>[^`]+)`|'(?<title>[^']+)')$");
        if (quoted.Success) return count is null or 1 ? [quoted.Groups["title"].Value] : [];
        var titles = Regex.Split(Content, @"[,;\r\n]+")
            .Select(value => Regex.Replace(value.Trim(), @"^\d+[.)]\s*", ""))
            .Where(value => value.Length > 0).ToArray();
        return ((count.HasValue && titles.Length == count.Value) || (count == null && Assignee != null && titles.Length == 1)) &&
            titles.All(value => value.Length <= 200)
            ? titles : [];
    }

    // Accent folding with unchanged offsets into the NFC original, unlike routing normalization.
    private static string Fold(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsSurrogate(character)) { result.Append(character); continue; }
            if (character is 'đ' or 'Đ') { result.Append('d'); continue; }
            var decomposed = character.ToString().Normalize(NormalizationForm.FormD);
            result.Append(char.ToLowerInvariant(decomposed[0]));
        }
        return result.ToString();
    }
}
