using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Import;
using Qaly.Application.DTOs.Wiki;

namespace Qaly.Application.Services;

public partial class FileImportService : IFileImportService
{
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".html", ".htm", ".docx"
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
                "Dinh dang document nay chua duoc ho tro. Hien co: .md, .markdown, .txt, .html, .htm, .docx.",
                400);
        }

        var raw = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : await ReadTextAsync(fileStream, ct);

        if (!extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(raw))
            return Result.Failure<ParsedDocument>("File khong co noi dung de import.", 400);

        return extension.ToLowerInvariant() switch
        {
            ".md" or ".markdown" => Result.Success(ParseMarkdown(raw, fileName)),
            ".docx" => ParseDocx(fileStream, fileName),
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

    private static Result<ParsedDocument> ParseDocx(Stream stream, string fileName)
    {
        try
        {
            if (stream.CanSeek)
                stream.Position = 0;

            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null)
                return Result.Failure<ParsedDocument>("File DOCX khong co noi dung de import.", 400);

            var title = NormalizeWhitespace(document.PackageProperties.Title);
            var blocks = new List<string>();
            var warnings = new List<string>();
            var hasVisibleText = false;

            foreach (var paragraph in body.Elements<Paragraph>())
            {
                var text = NormalizeWhitespace(GetParagraphText(paragraph));
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                hasVisibleText = true;
                var block = ConvertParagraphToMarkdown(paragraph, text);
                if (!string.IsNullOrWhiteSpace(block))
                    blocks.Add(block);

                if (string.IsNullOrWhiteSpace(title) && TryGetHeadingLevel(GetParagraphStyleId(paragraph), out _))
                    title = text;
            }

            if (!hasVisibleText)
                return Result.Failure<ParsedDocument>("File DOCX khong co noi dung de import.", 400);

            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileNameWithoutExtension(fileName);

            if (body.Descendants<Table>().Any())
                warnings.Add("Da bo qua bang trong DOCX; hien tai parser toi thieu chi chuyen heading va paragraph.");
            if (body.Descendants<Drawing>().Any())
                warnings.Add("Hinh anh trong DOCX da duoc bo qua trong parser toi thieu.");

            var markdown = string.Join("\n\n", blocks).Trim();
            if (string.IsNullOrWhiteSpace(markdown))
                markdown = textFallback(body);

            var parsedBlocks = SplitBlocks(markdown);
            return Result.Success(new ParsedDocument(
                "DOCX",
                title,
                ExtractDescription(parsedBlocks, title),
                markdown,
                CountBlocks(markdown),
                parsedBlocks.Take(8).ToList(),
                warnings));
        }
        catch (Exception ex)
        {
            return Result.Failure<ParsedDocument>($"Khong the doc DOCX: {ex.Message}", 400);
        }
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

    private static string ConvertParagraphToMarkdown(Paragraph paragraph, string text)
    {
        var styleId = GetParagraphStyleId(paragraph);
        if (TryGetHeadingLevel(styleId, out var level))
            return $"{new string('#', level)} {text}";

        if (IsListParagraph(paragraph))
            return $"- {text}";

        return text;
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        foreach (var node in paragraph.Descendants())
        {
            switch (node)
            {
                case Text text:
                    builder.Append(text.Text);
                    break;
                case TabChar:
                    builder.Append('\t');
                    break;
                case Break:
                    builder.Append('\n');
                    break;
                case CarriageReturn:
                    builder.Append('\n');
                    break;
            }
        }

        return builder.ToString();
    }

    private static string? GetParagraphStyleId(Paragraph paragraph)
        => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;

    private static bool TryGetHeadingLevel(string? styleId, out int level)
    {
        level = 0;
        if (string.IsNullOrWhiteSpace(styleId))
            return false;

        var match = Regex.Match(styleId, @"^Heading\s*([1-6])$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        level = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return true;
    }

    private static bool IsListParagraph(Paragraph paragraph)
        => paragraph.ParagraphProperties?.NumberingProperties != null ||
           (GetParagraphStyleId(paragraph)?.Contains("List", StringComparison.OrdinalIgnoreCase) ?? false);

    private static string NormalizeWhitespace(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value, @"\s+", " ").Trim();

    private static string DropTagWithContent(string html, string tagName)
        => Regex.Replace(html, $@"<\s*{tagName}[^>]*>.*?<\s*/\s*{tagName}\s*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static string textFallback(Body body)
        => string.Join("\n\n", body.Descendants<Paragraph>()
            .Select(paragraph => NormalizeWhitespace(GetParagraphText(paragraph)))
            .Where(text => !string.IsNullOrWhiteSpace(text)));

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
