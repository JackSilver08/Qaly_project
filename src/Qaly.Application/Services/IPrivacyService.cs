using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Privacy;

namespace Qaly.Application.Services;

public interface IPrivacyService
{
    Task<Result<IReadOnlyList<RetentionPolicyDto>>> ListPoliciesAsync(Guid tenantId, Guid? projectId, CancellationToken ct = default);
    Task<Result<RetentionPolicyDto>> CreatePolicyAsync(RetentionPolicyUpsertRequest request, CancellationToken ct = default);
    Task<Result<RetentionPolicyDto>> SupersedePolicyAsync(Guid policyId, RetentionPolicyUpsertRequest request, string rowVersion, CancellationToken ct = default);
    Task<Result<RetentionPolicyDto>> DisablePolicyAsync(Guid policyId, RetentionPolicyDisableRequest request, CancellationToken ct = default);

    Task<Result<IReadOnlyList<PrivacyConsentDto>>> ListConsentsAsync(Guid? projectId, CancellationToken ct = default);
    Task<Result<PrivacyConsentDto>> GrantConsentAsync(PrivacyConsentGrantRequest request, CancellationToken ct = default);
    Task<Result<PrivacyConsentDto>> RevokeConsentAsync(Guid consentId, PrivacyConsentRevokeRequest request, CancellationToken ct = default);
    Task<Result<PrivacyDecisionDto>> EvaluateAsync(PrivacyDecisionRequest request, CancellationToken ct = default);

    Task<Result<DataSubjectRequestDto>> SubmitDataSubjectRequestAsync(DataSubjectRequestSubmitRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DataSubjectRequestDto>>> ListDataSubjectRequestsAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<DataSubjectRequestDto>> GetDataSubjectRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<Result<DataSubjectRequestDto>> AcceptDataSubjectRequestAsync(Guid requestId, DataSubjectRequestDecisionRequest request, CancellationToken ct = default);
    Task<Result<DataSubjectRequestDto>> RejectDataSubjectRequestAsync(Guid requestId, DataSubjectRequestDecisionRequest request, CancellationToken ct = default);
    Task<Result<PrivacyExportArtifact>> DownloadDataSubjectExportAsync(Guid requestId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<PrivacyLegalHoldDto>>> ListLegalHoldsAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<PrivacyLegalHoldDto>> CreateLegalHoldAsync(PrivacyLegalHoldCreateRequest request, CancellationToken ct = default);
    Task<Result<PrivacyLegalHoldDto>> ReleaseLegalHoldAsync(Guid holdId, PrivacyLegalHoldReleaseRequest request, CancellationToken ct = default);
    Task<Result<PrivacyHealthDto>> GetHealthAsync(CancellationToken ct = default);
}
