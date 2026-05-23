namespace Qaly.Application.DTOs.Wiki;

public record WikiPageDto(
    Guid Id,
    string Title,
    string Content,
    string Visibility,
    string AuthorName,
    DateTimeOffset UpdatedAt
);

public record CreateWikiPageDto(
    string Title,
    string? Content,
    string Visibility = "internal"
);

public record UpdateWikiPageDto(
    string Title,
    string? Content,
    string Visibility = "internal"
);
