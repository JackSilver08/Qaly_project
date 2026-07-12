namespace Qaly.Application.Services;

public interface IPrivacyPayloadProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedPayload);
}
