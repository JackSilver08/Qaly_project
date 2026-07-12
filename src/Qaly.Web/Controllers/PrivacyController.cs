using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaly.Application.DTOs.Privacy;
using Qaly.Application.Services;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/privacy")]
public sealed class PrivacyController : BaseApiController
{
    private readonly IPrivacyService _privacyService;
    private readonly IPrivacyOperationsService _operationsService;

    public PrivacyController(IPrivacyService privacyService, IPrivacyOperationsService operationsService)
    {
        _privacyService = privacyService;
        _operationsService = operationsService;
    }

    [HttpGet("policies")]
    public async Task<IActionResult> ListPolicies(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        var result = await _privacyService.ListPoliciesAsync(tenantId, projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("policies")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePolicy(RetentionPolicyUpsertRequest request, CancellationToken ct)
    {
        var result = await _privacyService.CreatePolicyAsync(request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("policies/{policyId:guid}/supersede")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SupersedePolicy(
        Guid policyId,
        [FromQuery] string rowVersion,
        RetentionPolicyUpsertRequest request,
        CancellationToken ct)
    {
        var result = await _privacyService.SupersedePolicyAsync(policyId, request, rowVersion, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("policies/{policyId:guid}/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisablePolicy(
        Guid policyId,
        RetentionPolicyDisableRequest request,
        CancellationToken ct)
    {
        var result = await _privacyService.DisablePolicyAsync(policyId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("consents")]
    public async Task<IActionResult> ListConsents([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var result = await _privacyService.ListConsentsAsync(projectId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("consents")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantConsent(PrivacyConsentGrantRequest request, CancellationToken ct)
    {
        var result = await _privacyService.GrantConsentAsync(request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("consents/{consentId:guid}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeConsent(
        Guid consentId,
        PrivacyConsentRevokeRequest request,
        CancellationToken ct)
    {
        var result = await _privacyService.RevokeConsentAsync(consentId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("decisions/evaluate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Evaluate(PrivacyDecisionRequest request, CancellationToken ct)
    {
        var result = await _privacyService.EvaluateAsync(request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("data-subject-requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDataSubjectRequest(
        DataSubjectRequestSubmitRequest request,
        CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request = request with { IdempotencyKey = idempotencyKey };
        }

        var result = await _privacyService.SubmitDataSubjectRequestAsync(request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("data-subject-requests")]
    public async Task<IActionResult> ListDataSubjectRequests([FromQuery] Guid tenantId, CancellationToken ct)
    {
        var result = await _privacyService.ListDataSubjectRequestsAsync(tenantId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("data-subject-requests/{requestId:guid}")]
    public async Task<IActionResult> GetDataSubjectRequest(Guid requestId, CancellationToken ct)
    {
        var result = await _privacyService.GetDataSubjectRequestAsync(requestId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("data-subject-requests/{requestId:guid}/accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptDataSubjectRequest(
        Guid requestId,
        DataSubjectRequestDecisionRequest request,
        CancellationToken ct)
    {
        var result = await _privacyService.AcceptDataSubjectRequestAsync(requestId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("data-subject-requests/{requestId:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDataSubjectRequest(
        Guid requestId,
        DataSubjectRequestDecisionRequest request,
        CancellationToken ct)
    {
        var result = await _privacyService.RejectDataSubjectRequestAsync(requestId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("data-subject-requests/{requestId:guid}/download")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DownloadDataSubjectExport(Guid requestId, CancellationToken ct)
    {
        var result = await _privacyService.DownloadDataSubjectExportAsync(requestId, ct);
        if (!result.IsSuccess || result.Data == null)
        {
            return StatusCode(result.StatusCode, result);
        }

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    [HttpGet("legal-holds")]
    public async Task<IActionResult> ListLegalHolds([FromQuery] Guid tenantId, CancellationToken ct)
    {
        var result = await _privacyService.ListLegalHoldsAsync(tenantId, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("legal-holds")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLegalHold(PrivacyLegalHoldCreateRequest request, CancellationToken ct)
    {
        var result = await _privacyService.CreateLegalHoldAsync(request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("legal-holds/{holdId:guid}/release")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseLegalHold(
        Guid holdId,
        PrivacyLegalHoldReleaseRequest request,
        CancellationToken ct)
    {
        var result = await _privacyService.ReleaseLegalHoldAsync(holdId, request, ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var result = await _privacyService.GetHealthAsync(ct);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("worker/run-once")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunWorkerOnce(CancellationToken ct)
    {
        var result = await _operationsService.RunNextAsync(ct);
        return StatusCode(result.StatusCode, result);
    }
}
