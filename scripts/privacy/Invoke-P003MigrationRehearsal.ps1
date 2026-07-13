[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_]+$')]
    [string]$DatabaseName,

    [switch]$KeepDatabase
)

$ErrorActionPreference = 'Stop'
$previousMigration = '20260712064020_AddGitHubIntegrationSchema'
$targetMigration = '20260712185807_P003PrivacyRetentionDsar'
$server = '(localdb)\MSSQLLocalDB'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path

if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
    $suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $DatabaseName = "QalyP003Rehearsal_$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))_$suffix"
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($ArgumentList -join ' ')"
    }
}

function Invoke-SqlQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$Query
    )

    Invoke-Checked 'sqlcmd' @(
        '-S', $server,
        '-E',
        '-b',
        '-r', '1',
        '-W',
        '-s', '|',
        '-d', $Database,
        '-Q', $Query
    )
}

function Invoke-SqlFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Invoke-Checked 'sqlcmd' @(
        '-S', $server,
        '-E',
        '-b',
        '-r', '1',
        '-W',
        '-s', '|',
        '-d', $Database,
        '-i', $Path
    )
}

$oldConnectionString = [Environment]::GetEnvironmentVariable(
    'ConnectionStrings__DefaultConnection',
    [EnvironmentVariableTarget]::Process)
$databaseCreated = $false

Push-Location $repoRoot
try {
    Write-Output "P0-03 rehearsal database: $DatabaseName"
    Invoke-SqlQuery 'master' "CREATE DATABASE [$DatabaseName];"
    $databaseCreated = $true

    $connectionString = "Server=$server;Database=$DatabaseName;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true"
    [Environment]::SetEnvironmentVariable(
        'ConnectionStrings__DefaultConnection',
        $connectionString,
        [EnvironmentVariableTarget]::Process)

    Write-Output "`n=== MIGRATE TO P0-03 PREDECESSOR ==="
    Invoke-Checked 'dotnet' @(
        'ef', 'database', 'update', $previousMigration,
        '--project', 'src/Qaly.Infrastructure/Qaly.Infrastructure.csproj',
        '--startup-project', 'src/Qaly.Web/Qaly.Web.csproj',
        '--context', 'QalyDbContext',
        '--configuration', 'Release',
        '--no-build'
    )

    $seedSql = @'
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @UserId uniqueidentifier = 'D3000000-0000-0000-0000-000000000001';
DECLARE @ProjectId uniqueidentifier = 'D3000000-0000-0000-0000-000000000002';
DECLARE @MeetingId uniqueidentifier = 'D3000000-0000-0000-0000-000000000003';
DECLARE @ConsentId uniqueidentifier = 'D3000000-0000-0000-0000-000000000004';
DECLARE @RequestId uniqueidentifier = 'D3000000-0000-0000-0000-000000000005';
DECLARE @Now datetimeoffset = SYSDATETIMEOFFSET();

INSERT INTO dbo.Users (
    Id, FullName, Email, PasswordHash, Role, IsActive, CreatedAt
) VALUES (
    @UserId, N'P0-03 Rehearsal Subject', N'p003-rehearsal@qaly.test', N'not-used', N'Admin', 1, @Now
);

INSERT INTO dbo.Projects (
    Id, Name, Code, Status, OwnerId, EnableOnHold, EnableInReview,
    RequireEvidenceToDone, RestrictTransitionsToAdmin, IsDeleted, CreatedAt
) VALUES (
    @ProjectId, N'P0-03 Rehearsal Project', N'P003-REHEARSAL', N'Active', @UserId,
    1, 1, 0, 0, 0, @Now
);

INSERT INTO dbo.MeetingImports (
    Id, ProjectId, ImportedById, SourceProvider, SourceId, SourceHash, Title,
    MeetingStartedAt, Summary, TranscriptText, ParticipantsJson, RawPayloadJson,
    AiJobId, AiDraftId, CreatedAt, UpdatedAt
) VALUES (
    @MeetingId, @ProjectId, @UserId, N'meetily', N'legacy-meeting',
    REPLICATE(N'a', 64), N'Legacy sensitive meeting', @Now, N'legacy summary',
    N'legacy transcript', N'[]', N'{}', NULL, NULL, @Now, NULL
);

INSERT INTO dbo.PrivacyConsents (
    Id, TenantId, ProjectId, UserId, ConsentType, Purpose, ScopeJson, Status,
    GrantedAt, RevokedAt, IpAddress, UserAgent, CreatedAt, UpdatedAt
) VALUES (
    @ConsentId, NULL, @ProjectId, @UserId, N'ai_cloud_processing',
    N'meeting_action_extraction', NULL, N'granted', @Now, NULL, NULL, NULL, @Now, NULL
);

INSERT INTO dbo.DataSubjectRequests (
    Id, TenantId, ProjectId, RequesterUserId, RequestType, ScopeJson, Status,
    RequestedAt, ApprovedBy, CompletedAt, RejectionReason, EvidenceUri, CreatedAt, UpdatedAt
) VALUES (
    @RequestId, @ProjectId, @ProjectId, @UserId, N'export', N'{}',
    N'pending', @Now, NULL, NULL, NULL, NULL, @Now, NULL
);
'@

    Write-Output "`n=== SEED LEGACY DATA ==="
    Invoke-SqlQuery $DatabaseName $seedSql

    Write-Output "`n=== P0-03 PREFLIGHT ==="
    Invoke-SqlFile $DatabaseName (Join-Path $scriptRoot 'p003-preflight.sql')

    Write-Output "`n=== APPLY P0-03 MIGRATION ==="
    Invoke-Checked 'dotnet' @(
        'ef', 'database', 'update', $targetMigration,
        '--project', 'src/Qaly.Infrastructure/Qaly.Infrastructure.csproj',
        '--startup-project', 'src/Qaly.Web/Qaly.Web.csproj',
        '--context', 'QalyDbContext',
        '--configuration', 'Release',
        '--no-build'
    )

    Write-Output "`n=== P0-03 POSTFLIGHT ==="
    Invoke-SqlFile $DatabaseName (Join-Path $scriptRoot 'p003-postflight.sql')

    Write-Output "`n=== P0-03 RECONCILIATION ==="
    Invoke-SqlFile $DatabaseName (Join-Path $scriptRoot 'p003-reconciliation.sql')

    Write-Output "`nP0-03 migration rehearsal completed successfully."
}
finally {
    [Environment]::SetEnvironmentVariable(
        'ConnectionStrings__DefaultConnection',
        $oldConnectionString,
        [EnvironmentVariableTarget]::Process)

    if ($databaseCreated -and -not $KeepDatabase) {
        Write-Output "`n=== CLEANUP ==="
        Invoke-SqlQuery 'master' "ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DatabaseName];"
        Write-Output "Dropped rehearsal database $DatabaseName."
    }
    elseif ($databaseCreated) {
        Write-Output "Kept rehearsal database $DatabaseName."
    }

    Pop-Location
}
