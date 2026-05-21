namespace Qaly.Application.DTOs.Wiki;

public record WikiPageDto(
    Guid Id,
    string Title,
    string Content,
    string AuthorName,
    DateTimeOffset UpdatedAt
);

public record CreateWikiPageDto(
    string Title,
    string? Content
);

public record UpdateWikiPageDto(
    string Title,
    string? Content
);
