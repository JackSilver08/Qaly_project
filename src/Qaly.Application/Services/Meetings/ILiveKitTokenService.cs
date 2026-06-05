using Qaly.Application.Common.Models;

namespace Qaly.Application.Services.Meetings;

public interface ILiveKitTokenService
{
    LiveKitTokenResult CreateJoinToken(LiveKitTokenRequest request);
}

public sealed record LiveKitTokenRequest(
    string RoomName,
    string ParticipantIdentity,
    string ParticipantName,
    string? ParticipantMetadata = null);

public sealed record LiveKitTokenResult(
    string ServerUrl,
    string Token,
    DateTimeOffset ExpiresAt);
