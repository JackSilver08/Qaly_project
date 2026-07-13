using Microsoft.AspNetCore.DataProtection;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.Privacy;

public sealed class PrivacyPayloadProtector : IPrivacyPayloadProtector
{
    private readonly IDataProtector _protector;

    public PrivacyPayloadProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Qaly.Privacy.DsarExport.v1");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedPayload) => _protector.Unprotect(protectedPayload);
}
