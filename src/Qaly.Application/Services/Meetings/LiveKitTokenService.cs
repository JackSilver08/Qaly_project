using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;

namespace Qaly.Application.Services.Meetings;

public sealed class LiveKitTokenService : ILiveKitTokenService
{
    private readonly LiveKitOptions _options;

    public LiveKitTokenService(IOptions<LiveKitOptions> options)
    {
        _options = options.Value;
    }

    public LiveKitTokenResult CreateJoinToken(LiveKitTokenRequest request)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("LiveKit is not configured.");
        }

        var ttl = TimeSpan.FromMinutes(Math.Clamp(_options.TokenTtlMinutes, 5, 24 * 60));
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        var token = new AccessToken(_options.ApiKey.Trim(), _options.ApiSecret.Trim())
            .WithIdentity(request.ParticipantIdentity)
            .WithName(request.ParticipantName)
            .WithGrants(new VideoGrants
            {
                Room = request.RoomName,
                RoomJoin = true,
                CanPublish = true,
                CanSubscribe = true,
                CanPublishData = true
            })
            .WithTtl(ttl);

        if (!string.IsNullOrWhiteSpace(request.ParticipantMetadata))
        {
            token = token.WithMetadata(request.ParticipantMetadata);
        }

        return new LiveKitTokenResult(
            _options.ServerUrl.Trim(),
            token.ToJwt(),
            expiresAt);
    }
}
