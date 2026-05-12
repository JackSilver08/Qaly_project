namespace Qaly.Application.DTOs.Vote;

public record VoteRequest(int Value);

public record VoteSummaryDto(
    string TargetType,
    Guid TargetId,
    int UpvoteCount,
    int DownvoteCount,
    int Score,
    int MyVote);
