# Rollback Plan - T1-TR-01 & T1-TR-02

## Document Information

**Title**: Rollback Procedure for T1-TR-01 & T1-TR-02 QA Tests  
**Date**: 2026-07-18  
**Author**: Trung (QA Automation)  
**Branches**:

- `codex/trung-t1-runtime-regression`
- `codex/trung-t1-evidence-notification-tests`

**Severity Levels**: 🟢 Low | 🟡 Medium | 🔴 High | 🟣 Critical

---

## Overview

This document defines the rollback procedures for the T1-TR-01 and T1-TR-02 test implementations. Since these are **test-only changes** (no production code modifications), rollback is straightforward and low-risk.

### When to Rollback

| Scenario                                     | Severity    | Action                                             |
| -------------------------------------------- | ----------- | -------------------------------------------------- |
| Test file syntax errors preventing execution | 🟡 Medium   | Fix in-place or rollback                           |
| Tests timeout consistently on CI/CD          | 🟡 Medium   | Adjust timings and retry; rollback if config issue |
| Tests interfere with other test suites       | 🟡 Medium   | Isolate tests; rollback if needed                  |
| Tests corrupt test database state            | 🔴 High     | Rollback immediately                               |
| Tests leak into production environment       | 🟣 Critical | **IMMEDIATE ROLLBACK**                             |
| Tests reveal blocking production bugs        | 🟡 Medium   | Keep tests, fix blocking bugs separately           |
| Reviewer requests major test restructuring   | 🟡 Medium   | Rollback and redesign vs. iterate                  |

---

## Part 1: Simple Rollback (No Code Merge Yet)

### 1.1 Scenario: Branch Not Yet Merged to Main

**Status**: Tests in PR, not yet merged to `main`

**Rollback Steps**:

```bash
# Step 1: Switch to feature branch
git checkout codex/trung-t1-runtime-regression

# Step 2: Option A - Discard all changes
git reset --hard main
git push -f origin codex/trung-t1-runtime-regression

# Step 2: Option B - Delete branch entirely
git branch -D codex/trung-t1-runtime-regression
git push origin --delete codex/trung-t1-runtime-regression

# Step 3: Close PR on GitHub
# Navigate to GitHub PR → "Close pull request" button
```

**Time to Recover**: < 5 minutes  
**Data Loss**: None (changes still in git history)  
**Team Notification**: Post in Slack #qa-automation: "T1-TR-01 rollback: PR closed due to [reason]"

---

## Part 2: Rollback After Merge to Main

### 2.1 Scenario: Tests Merged but Found to Be Broken

**Status**: Code merged to `main`, now causing issues

**Rollback Steps**:

#### Option A: Complete Revert (Recommended)

```bash
# Step 1: Create revert branch
git checkout main
git pull origin main

# Step 2: Identify problematic commit(s)
git log --oneline | head -20
# Find commit hash for "T1-TR-01 & T1-TR-02 tests added"

# Step 3: Revert the commit(s)
git revert -m 1 <commit-hash>
# This creates a NEW commit that undoes the changes

# Step 4: Resolve conflicts if any (unlikely for test files)
git status
# If conflicts: manually resolve, then `git add .` and `git revert --continue`

# Step 5: Push to main
git push origin main

# Step 6: Verify
git log --oneline | head -5
# Should show the revert commit at the top
```

**Example**:

```bash
git log --oneline | head -5
# Output:
# abc1234 Revert "T1-TR-01 & T1-TR-02 tests added"
# def5678 T1-TR-01 & T1-TR-02 tests added
# ghi9012 Previous commit
# ...

git revert -m 1 def5678
git push origin main
```

**Time to Recover**: 5-10 minutes  
**Team Impact**: Minimal (tests removed from pipeline)  
**Commit History**: Preserved (revert commit shows in history)

---

#### Option B: Force Reset (Only if Immediate Critical)

⚠️ **WARNING**: Only use if revert fails or in critical situations. Can disrupt team workflow.

```bash
# Step 1: Get last known good commit
git log --oneline | grep "before T1-TR tests"
# Note the commit hash (e.g., "zzz0000 Previous release")

# Step 2: Reset to that commit
git reset --hard zzz0000

# Step 3: Force push
git push -f origin main

# Step 4: Notify entire team immediately
# Slack, Email, GitHub issue with explanation
```

**Time to Recover**: 2-3 minutes  
**Team Impact**: HIGH (others' work potentially affected)  
**Risk**: Use only if critical (e.g., tests prevent CI from running)

---

### 2.2 Recovery Checklist After Merge Rollback

- [ ] Rollback commit pushed to main
- [ ] CI/CD pipeline green with rollback
- [ ] Tests no longer running in CI
- [ ] Team notified via Slack
- [ ] Root cause documented in GitHub issue
- [ ] New branch created for fixes (if applicable)

---

## Part 3: Partial Rollback (Fix Specific Issues)

### 3.1 Scenario: One Test File Is Good, One Is Broken

**Status**: T1-TR-01 tests passing, T1-TR-02 tests failing

**Rollback Steps**:

```bash
# Step 1: Create new branch from main
git checkout main
git pull origin main
git checkout -b codex/trung-t1-fix-integration-tests

# Step 2: Remove only the broken test file
git rm tests/Qaly.IntegrationTests/T1TR02CommentEvidenceNotificationTests.cs

# Step 3: Keep the good file
# tests/e2e/t1-tr-01-task-url-regression.spec.ts remains

# Step 4: Commit and push
git add .
git commit -m "Remove broken T1-TR-02 tests; keep T1-TR-01"
git push origin codex/trung-t1-fix-integration-tests

# Step 5: Create new PR with just T1-TR-01
# Let T1-TR-02 be redesigned in separate PR
```

**Time to Recover**: 15-20 minutes  
**Partial Impact**: T1-TR-01 available; T1-TR-02 deferred

---

## Part 4: Environment-Specific Rollback

### 4.1 Scenario: Tests Pass in Dev, Fail in Staging

**Status**: Tests work locally, break in CI/CD pipeline

**Rollback Steps**:

````bash
# Step 1: Skip tests in CI only (don't remove from main)
# Edit: .github/workflows/test.yml

```yaml
- name: Run T1-TR-01 tests
  if: github.event.pull_request.draft == false  # Disable temporarily
  run: npx playwright test t1-tr-01-task-url-regression.spec.ts
````

# Step 2: Commit updated workflow

git add .github/workflows/test.yml
git commit -m "Temporarily skip T1-TR-01/02 in CI while debugging"
git push origin main

# Step 3: Create separate debugging branch

git checkout -b codex/debug-t1-tr-ci-failures

# Step 4: Debug (increase timeouts, add logging, etc.)

# Make changes, push, test manually

# Step 5: Once fixed, re-enable in workflow

# git push, PR, merge when green

````

**Time to Recover**: 30-60 minutes (includes debugging)

---

## Part 5: Test Database Corruption Recovery

### 5.1 Scenario: Integration Tests Leave DB in Bad State

**Status**: Tests fail to clean up; subsequent tests inherit corrupted state

**Immediate Actions**:

```bash
# Step 1: Kill running tests immediately
pkill -f "playwright|dotnet test"

# Step 2: Clean database
# For in-memory DB (development):
rm -rf bin/obj
dotnet clean

# For SQL Server (if running):
sqlcmd -S localhost -U sa -P YourPassword -Q "DROP DATABASE QalyIntegrationTests;"
````

**Prevention for Future**:

```csharp
// Add in T1TR02CommentEvidenceNotificationTests.cs

public async Task IAsyncLifetime.InitializeAsync()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
    await db.Database.EnsureDeletedAsync();  // Clean slate
    await db.Database.EnsureCreatedAsync();
    // Then seed test data
}

public async Task IAsyncLifetime.DisposeAsync()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
    await db.Database.EnsureDeletedAsync();  // Clean up after
}
```

**Time to Recover**: 10-15 minutes  
**Permanent Fix**: Update test teardown logic

---

## Part 6: Contact & Escalation

### 6.1 Rollback Decision Tree

```
Is this a test-only change?
├─ YES: Proceed with rollback (low risk)
└─ NO: This shouldn't happen (design issue)
       Contact Trung immediately

Has it been merged to main?
├─ NO: Simple revert (Part 1)
└─ YES:
    Is it breaking CI/CD?
    ├─ YES (🔴 High): Use force reset (Part 2B)
    │                  Contact: Trung + Duy + Team Lead
    └─ NO (🟡 Medium): Use git revert (Part 2A)
                       Contact: Trung + Duy

Is it environment-specific?
├─ YES: Skip in workflow, debug separately (Part 4)
└─ NO: Full rollback
```

### 6.2 Contact Procedures

**For Support** (order of escalation):

1. **Immediate**: Slack message in #qa-automation
    - Tag: `@Trung` (code owner)
    - Message: "T1-TR tests failing. Reason: [brief description]"

2. **Within 5 min**: GitHub issue with label `urgent`
    - Title: "ROLLBACK NEEDED: T1-TR-01/02 [specific issue]"
    - Include: Error logs, attempted rollback step, impact

3. **Within 15 min**: Zoom/Teams call if not resolved
    - Participants: Trung, Duy, Minh, Khang
    - Decision: Fix vs. Full rollback

**For Production Issues** (🟣 Critical):

```
1. IMMEDIATE: pkill all tests + freeze pipeline
2. IMMEDIATE: Slack urgent message to @Trung @Duy
3. WITHIN 2 min: Execute force reset (Part 2B)
4. WITHIN 5 min: Post-incident review
```

---

## Part 7: Rollback Verification Checklist

After executing rollback, verify:

- [ ] Tests no longer run in CI/CD pipeline
- [ ] Production code unchanged
- [ ] No orphaned resources (temp files, DB, etc.)
- [ ] Git history shows revert commit
- [ ] Slack message posted explaining reason
- [ ] GitHub PR closed with reason noted
- [ ] Team confirms pipeline back to green

**Verification Commands**:

```bash
# Verify tests removed
ls -la tests/e2e/t1-tr-01-*.spec.ts 2>&1 | grep "No such file"
ls -la tests/Qaly.IntegrationTests/T1TR02* 2>&1 | grep "No such file"

# Verify production code intact
git diff main~1 src/ | grep -i "modified" || echo "No production changes"

# Verify git history
git log --oneline | head -3
# Should show revert commit at top
```

---

## Part 8: Post-Rollback Fix Strategy

### After Rollback, Choose Path:

**Path A: Redesign Tests** (for major issues)

```
1. Create new branch: codex/trung-t1-redesign
2. Redesign affected test file
3. Add new test cases to address root cause
4. Re-submit as new PR
5. Time estimate: 2-4 hours
```

**Path B: Incremental Fix** (for small issues)

```
1. Create new branch: codex/trung-t1-fix
2. Cherry-pick working tests (if partial rollback)
3. Fix specific failures
4. Re-submit focused PR
5. Time estimate: 30-60 minutes
```

**Path C: Defer** (for complex issues)

```
1. Create GitHub issue: "T1-TR tests - deferred to Week 2"
2. Add to backlog
3. Document blocking factors
4. Re-estimate effort
5. Time estimate: Next sprint
```

---

## Part 9: Documentation & Lessons Learned

After rollback, create post-mortem:

```markdown
# Post-Rollback Analysis - T1-TR-01/02

## Summary

- **Date**: 2026-07-18
- **Duration**: X minutes from issue detection to rollback
- **Impact**: None (tests only)

## Root Cause

[One sentence explaining why rollback was needed]

## What Went Wrong

1. [First issue]
2. [Second issue]
3. [Third issue if applicable]

## Preventive Measures

1. Add pre-commit hook to validate test file syntax
2. Increase timeout values in E2E config
3. Add test cleanup hooks in integration tests

## Follow-up

- [ ] Create new branch for fix
- [ ] Schedule review with Duy/Minh
- [ ] Target re-merge date: [Date]

## Lessons Learned

- [What we learned]
- [How we'll improve next time]
```

---

## Part 10: Recovery Time Objectives (RTO)

| Rollback Type                       | RTO       | RPO   | Notes                            |
| ----------------------------------- | --------- | ----- | -------------------------------- |
| Pre-merge (Part 1)                  | < 5 min   | N/A   | Instant, no production impact    |
| Post-merge, simple revert (Part 2A) | 5-10 min  | 0 min | Tests removed; production intact |
| Post-merge, force reset (Part 2B)   | 2-3 min   | 0 min | Critical only; notify team       |
| Partial rollback (Part 3)           | 15-20 min | 0 min | Keep good tests; fix bad ones    |
| Environment-specific (Part 4)       | 30-60 min | 0 min | Includes debugging               |
| DB corruption (Part 5)              | 10-15 min | 0 min | DB reset; re-seed data           |

---

## Part 11: Test Environment Resilience

### Prevent Future Rollbacks

**Add to Test Setup**:

```csharp
[SetUp]
public async Task TestSetup()
{
    // Ensure clean state
    await _db.Database.EnsureDeletedAsync();
    await _db.Database.EnsureCreatedAsync();
    await SeedTestDataAsync();
}

[TearDown]
public async Task TestTeardown()
{
    // Cleanup
    await _db.Database.EnsureDeletedAsync();

    // Validate no side effects
    AssertNoLeakedResources();
}
```

**Add to E2E Tests**:

```typescript
test.afterEach(async ({ page }, testInfo) => {
    // Capture failure evidence
    if (testInfo.status !== "passed") {
        await page.screenshot({
            path: `test-results/failure-${testInfo.testId}.png`,
        });
    }

    // Clear auth state
    await page.context().clearCookies();
});
```

---

## Part 12: Final Checklist

Before declaring rollback complete:

✅ **Git Level**:

- [ ] Rollback commit(s) in git log
- [ ] `main` branch clean
- [ ] No uncommitted changes

✅ **CI/CD Level**:

- [ ] Pipeline no longer runs rolled-back tests
- [ ] Pipeline status green
- [ ] No blocking errors

✅ **Documentation Level**:

- [ ] Rollback reason documented
- [ ] GitHub issue closed with explanation
- [ ] Slack message posted
- [ ] Post-mortem scheduled (if needed)

✅ **Team Level**:

- [ ] All reviewers notified
- [ ] Fix plan (if applicable) created
- [ ] Next sprint estimated

---

## Appendix: Command Reference

```bash
# View rollback options
git log --oneline --all | head -20

# Revert specific commit
git revert -m 1 <commit-hash>

# Force reset (use with care)
git reset --hard <commit-hash>
git push -f origin main

# Stash changes (temporary hold)
git stash
git stash pop

# Cherry-pick specific fixes
git cherry-pick <commit-hash>

# View diff before rollback
git diff HEAD~1
git diff <branch1> <branch2>

# Abort in-progress operations
git revert --abort
git merge --abort
git rebase --abort
```

---

**Document Version**: 1.0  
**Last Updated**: 2026-07-18  
**Next Review**: After first rollback (if needed) or 2026-08-01
