namespace Qaly.Domain.Entities;

public static class AiJobStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Retrying = "retrying";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Canceled = "canceled";

    public static bool IsTerminal(string status)
        => status is Succeeded or Failed or Canceled;

    public static bool CanTransition(string from, string to)
        => (from, to) switch
        {
            (Queued, Running or Canceled) => true,
            (Running, Succeeded or Retrying or Failed or Canceled) => true,
            (Retrying, Queued or Running or Failed or Canceled) => true,
            (Failed, Retrying) => true,
            _ => string.Equals(from, to, StringComparison.Ordinal)
        };
}

public static class AiDraftStatuses
{
    public const string PendingReview = "pending_review";
    public const string Confirmed = "confirmed";
    public const string Rejected = "rejected";
    public const string Expired = "expired";

    public static bool IsTerminal(string status)
        => status is Confirmed or Rejected or Expired;
}

public static class AiAttemptStatuses
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Canceled = "canceled";
}
