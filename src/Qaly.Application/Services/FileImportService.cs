using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Import;
using Qaly.Application.DTOs.Wiki;

namespace Qaly.Application.Services;

public partial class FileImportService : IFileImportService
{
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".html", ".htm"
    };

    private readonly IWikiService _wikiService;

    public FileImportService(IWikiService wikiService)
    {
        _wikiService = wikiService;
    }

    public async Task<Result<DocumentImportPreviewResult>> PreviewDocumentAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        var parsed = await ParseDocumentAsync(fileStream, fileName, ct);
        if (!parsed.IsSuccess)
            return Result.Failure<DocumentImportPreviewResult>(parsed.Error ?? "Khong the doc file.", parsed.StatusCode);

        var document = parsed.Data!;
        return Result.Success(new DocumentImportPreviewResult(
            fileName,
            document.FileType,
            document.Title,
            document.Description,
            document.BlockCount,
            document.PreviewBlocks,
            document.Warnings));
    }

    public async Task<Result<DocumentImportResult>> ImportDocumentAsync(
        Guid projectId,
        Stream fileStream,
        string fileName,
        string? title = null,
        CancellationToken ct = default)
    {
        var parsed = await ParseDocumentAsync(fileStream, fileName, ct);
        if (!parsed.IsSuccess)
            return Result.Failure<DocumentImportResult>(parsed.Error ?? "Khong the doc file.", parsed.StatusCode);

        var document = parsed.Data!;
        var pageTitle = string.IsNullOrWhiteSpace(title) ? document.Title : title.Trim();
        var created = await _wikiService.CreateAsync(projectId, new CreateWikiPageDto(
            pageTitle,
            document.Content,
            "internal"), ct);

        if (!created.IsSuccess)
            return Result.Failure<DocumentImportResult>(created.Error ?? "Khong the tao wiki page.", created.StatusCode);

        return Result.Success(new DocumentImportResult(
            created.Data!.Id,
            projectId,
            created.Data.Title,
            document.BlockCount,
            document.Warnings));
    }

    private static async Task<Result<ParsedDocument>> ParseDocumentAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName);
        if (!DocumentExtensions.Contains(extension))
        {
            return Result.Failure<ParsedDocument>(
                "Dinh dang document nay chua duoc ho tro o phase dau. Hien co: .md, .markdown, .txt, .html, .htm.",
                400);
        }

        var raw = await ReadTextAsync(fileStream, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return Result.Failure<ParsedDocument>("File khong co noi dung de import.", 400);

        return extension.ToLowerInvariant() switch
        {
            ".md" or ".markdown" => Result.Success(ParseMarkdown(raw, fileName)),
            ".txt" => Result.Success(ParsePlainText(raw, fileName)),
            ".html" or ".htm" => Result.Success(ParseHtml(raw, fileName)),
            _ => Result.Failure<ParsedDocument>("Dinh dang document khong ho tro.", 400)
        };
    }

    private static async Task<string> ReadTextAsync(Stream stream, CancellationToken ct)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static ParsedDocument ParseMarkdown(string raw, string fileName)
    {
        var normalized = raw.Replace("\r\n", "\n").Trim();
        var title = ExtractMarkdownTitle(normalized) ?? Path.GetFileNameWithoutExtension(fileName);
        var blocks = SplitBlocks(normalized);
        var warnings = new List<string>();

        if (ContainsMermaidBlock(normalized))
            warnings.Add("Da phat hien Mermaid block; hien tai se luu dang markdown de renderer xu ly.");
        if (ContainsMath(normalized))
            warnings.Add("Da phat hien cong thuc LaTeX; frontend can KaTeX de render dep.");

        return new ParsedDocument(
            "Markdown",
            title,
            ExtractDescription(blocks, title),
            normalized,
            CountBlocks(normalized),
            blocks.Take(8).ToList(),
            warnings);
    }

    private static ParsedDocument ParsePlainText(string raw, string fileName)
    {
        var normalized = raw.Replace("\r\n", "\n").Trim();
        var warnings = LooksLikeMarkdown(normalized)
            ? new List<string> { "File .txt co dau hieu Markdown; co the doi duoi .md de giu formatting tot hon." }
            : [];

        return new ParsedDocument(
            "Plain Text",
            Path.GetFileNameWithoutExtension(fileName),
            ExtractDescription(SplitBlocks(normalized), null),
            normalized,
            CountBlocks(normalized),
            SplitBlocks(normalized).Take(8).ToList(),
            warnings);
    }

    private static ParsedDocument ParseHtml(string raw, string fileName)
    {
        var title = ExtractTag(raw, "title") ?? Path.GetFileNameWithoutExtension(fileName);
        var description = ExtractMetaDescription(raw) ?? string.Empty;
        var body = ExtractTag(raw, "body") ?? raw;

        body = DropTagWithContent(body, "script");
        body = DropTagWithContent(body, "style");
        body = DropTagWithContent(body, "iframe");
        body = DropTagWithContent(body, "form");

        var markdown = ConvertBasicHtmlToMarkdown(body).Trim();
        var blocks = SplitBlocks(markdown);

        return new ParsedDocument(
            "HTML",
            WebUtility.HtmlDecode(title).Trim(),
            WebUtility.HtmlDecode(description).Trim(),
            markdown,
            CountBlocks(markdown),
            blocks.Take(8).ToList(),
            []);
    }

    private static string ConvertBasicHtmlToMarkdown(string html)
    {
        var text = html;
        text = Regex.Replace(text, @"<\s*h1[^>]*>(.*?)<\s*/\s*h1\s*>", "\n# $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\s*h2[^>]*>(.*?)<\s*/\s*h2\s*>", "\n## $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\s*h3[^>]*>(.*?)<\s*/\s*h3\s*>", "\n### $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\s*p[^>]*>(.*?)<\s*/\s*p\s*>", "\n$1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*li[^>]*>(.*?)<\s*/\s*li\s*>", "\n- $1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\s*blockquote[^>]*>(.*?)<\s*/\s*blockquote\s*>", "\n> $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\s*pre[^>]*>\s*<\s*code[^>]*>(.*?)<\s*/\s*code\s*>\s*<\s*/\s*pre\s*>", "\n```\n$1\n```\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\s*a[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)<\s*/\s*a\s*>", "[$2]($1)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\s*img[^>]*alt\s*=\s*[""']([^""']*)[""'][^>]*src\s*=\s*[""']([^""']+)[""'][^>]*>", "![ $1 ]($2)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty, RegexOptions.Singleline);
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+\n", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text;
    }

    private static string? ExtractMarkdownTitle(string markdown)
    {
        var match = Regex.Match(markdown, @"^\s*#\s+(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractTag(string html, string tagName)
    {
        var match = Regex.Match(html, $@"<\s*{tagName}[^>]*>(.*?)<\s*/\s*{tagName}\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? StripTags(match.Groups[1].Value).Trim() : null;
    }

    private static string? ExtractMetaDescription(string html)
    {
        var match = Regex.Match(html, @"<\s*meta[^>]*name\s*=\s*[""']description[""'][^>]*content\s*=\s*[""']([^""']*)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string DropTagWithContent(string html, string tagName)
        => Regex.Replace(html, $@"<\s*{tagName}[^>]*>.*?<\s*/\s*{tagName}\s*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static string StripTags(string html)
        => WebUtility.HtmlDecode(Regex.Replace(html, @"<[^>]+>", string.Empty, RegexOptions.Singleline));

    private static List<string> SplitBlocks(string content)
        => content
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

    private static string ExtractDescription(List<string> blocks, string? title)
        => blocks.FirstOrDefault(block => !string.Equals(block.TrimStart('#', ' '), title, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

    private static int CountBlocks(string content)
        => Math.Max(1, SplitBlocks(content).Count);

    private static bool LooksLikeMarkdown(string text)
        => Regex.IsMatch(text, @"(^#\s)|(\*\*.+\*\*)|(^-\s)|(```)", RegexOptions.Multiline);

    private static bool ContainsMermaidBlock(string text)
        => Regex.IsMatch(text, @"```\s*mermaid", RegexOptions.IgnoreCase);

    private static bool ContainsMath(string text)
        => Regex.IsMatch(text, @"\$\$.+?\$\$|\$.+?\$", RegexOptions.Singleline);

    private sealed record ParsedDocument(
        string FileType,
        string Title,
        string Description,
        string Content,
        int BlockCount,
        List<string> PreviewBlocks,
        List<string> Warnings);
}
