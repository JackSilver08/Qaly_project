# Test Execution Commands - T1-TR-01 & T1-TR-02

## Overview

This document provides the exact commands to run T1-TR-01 and T1-TR-02 tests in clean environments, with proper error handling and evidence collection.

**Date**: 2026-07-18  
**Author**: Trung (QA Automation)  
**Branch**: codex/trung-t1-runtime-regression & codex/trung-t1-evidence-notification-tests

---

## PART 1: Environment Setup

### 1.1 Pre-requisites

```bash
# Verify Node.js version (12+)
node --version
npm --version

# Verify .NET version (8.0+)
dotnet --version

# Verify test runners installed
npm list -g playwright
dotnet tool list
```

### 1.2 Clean Installation

```bash
# Clone latest code
git clone <repo-url> QALY-test
cd QALY-test
git checkout main
git pull origin main

# Install dependencies
npm ci
dotnet restore

# Build solution
dotnet build -c Release

# Build Frontend
npm run build
```

### 1.3 Database Setup (for Integration Tests)

```bash
# Apply migrations (uses in-memory DB for integration tests)
dotnet ef database update --project src/Qaly.Infrastructure --context QalyDbContext

# Seed test data (optional for manual verification)
npm run seed:test-data
```

---

## PART 2: T1-TR-01 - E2E Test Execution

### Command 2.1: Run T1-TR-01 Full Suite (Chrome)

```bash
cd tests/e2e

# Run with default Chrome browser
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --project=chromium \
  --headed \
  --workers=1 \
  --retries=2

# Expected output:
# ✓ TC-TR-01-001: Task opens with canonical URL...
# ✓ TC-TR-01-002: Refresh page preserves canonical task URL...
# ✓ TC-TR-01-003: Share/Copy task URL creates valid canonical link...
# ... (10 tests total)
```

### Command 2.2: Run T1-TR-01 with Custom Server

```bash
E2E_BASE_URL="https://qaly-staging.azurewebsites.net" \
E2E_ADMIN_EMAIL="test-admin@qaly.dev" \
E2E_ADMIN_PASSWORD="SecurePassword123!" \
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --project=chromium
```

### Command 2.3: Run Single Test Case

```bash
# Run only canonical URL test
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  -g "TC-TR-01-001"

# Run only privacy policy tests
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  -g "privacy"
```

### Command 2.4: Run with Detailed Tracing

```bash
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --trace=on \
  --screenshot=only-on-failure \
  --video=retain-on-failure

# View trace
npx playwright show-trace test-results/trace.zip
```

### Command 2.5: Run in Debug Mode (Interactive)

```bash
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --debug \
  --headed

# This opens Inspector; use 'step' to debug line-by-line
```

### Command 2.6: Generate Test Report

```bash
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --reporter=html=test-results/e2e-report.html

# Open report
open test-results/e2e-report.html
# or on Windows
start test-results/e2e-report.html
```

---

## PART 3: T1-TR-02 - Integration Test Execution

### Command 3.1: Run T1-TR-02 Full Suite

```bash
cd tests/Qaly.IntegrationTests

# Run all tests in T1-TR-02 file
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  -c Release \
  --logger "console;verbosity=detailed" \
  --no-build

# Expected output:
# Passed  T1TR02CommentEvidenceNotificationTests.CommentCreate_WithAuthorizedUser_Returns201AndPersistsToDb [123ms]
# Passed  T1TR02CommentEvidenceNotificationTests.CommentCreate_WithUnauthorizedUser_Returns403Forbidden [45ms]
# ... (11 tests total)
# Test Run Successful.
# Total tests: 11. Passed: 11. Failed: 0. Skipped: 0.
```

### Command 3.2: Run Specific Test Class

```bash
# Run only Comment tests
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  --filter "FullyQualifiedName~Comment" \
  -c Release

# Run only Evidence tests
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  --filter "FullyQualifiedName~Evidence" \
  -c Release

# Run only Notification tests
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  --filter "FullyQualifiedName~Notification" \
  -c Release
```

### Command 3.3: Run with Code Coverage

```bash
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  -c Release \
  /p:CollectCoverage=true \
  /p:CoverageFormat=cobertura \
  /p:Exclude="[*Tests]*" \
  --logger trx

# Generate coverage report
reportgenerator -reports:"**/*coverage.cobertura.xml" \
  -targetdir:"test-results/coverage" \
  -reporttypes:"Html"

open test-results/coverage/index.html
```

### Command 3.4: Run with Detailed Output

```bash
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  -c Release \
  --logger "console;verbosity=detailed" \
  --diag test-results/diag.log
```

### Command 3.5: Run in Isolation Mode (Safety)

```bash
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  -c Release \
  --settings coverlet.runsettings \
  --logger trx \
  --testadapterpath:"packages" \
  --blame-hang-timeout=60000
```

---

## PART 4: Combined E2E + Integration Test Run

### Command 4.1: Full QA Regression Suite

```bash
#!/bin/bash
# Run both E2E and Integration tests in sequence

echo "=== Starting T1-TR-01 E2E Tests ==="
cd tests/e2e
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --project=chromium \
  --workers=1 \
  || { echo "E2E tests failed"; exit 1; }

echo "=== E2E Tests Complete. Starting Integration Tests ==="
cd ../Qaly.IntegrationTests
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  -c Release \
  --logger "console;verbosity=normal" \
  || { echo "Integration tests failed"; exit 1; }

echo "=== All Tests Passed Successfully ==="
```

### Command 4.2: Generate Combined Report

```bash
# E2E HTML Report
cd tests/e2e
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --reporter=html=e2e-report.html

# Integration TRX Report
cd ../Qaly.IntegrationTests
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  -c Release \
  --logger "trx;LogFileName=integration-report.trx"

# Combine results in CI/CD pipeline (use TRX to HTML converter)
echo "Reports:"
echo "- E2E: tests/e2e/e2e-report.html"
echo "- Integration: tests/Qaly.IntegrationTests/integration-report.html"
```

---

## PART 5: Docker-Based Test Execution (Recommended for CI/CD)

### Command 5.1: Build Test Container

```bash
docker build -f Dockerfile -t qaly-test:latest .

# Or use docker-compose
docker-compose -f docker-compose.test.yml build
```

### Command 5.2: Run Tests in Container

```bash
# Run E2E tests
docker-compose -f docker-compose.test.yml run \
  --rm e2e-tests \
  npm run test:e2e

# Run Integration tests
docker-compose -f docker-compose.test.yml run \
  --rm integration-tests \
  npm run test:integration

# Run both
docker-compose -f docker-compose.test.yml up \
  --abort-on-container-exit \
  --exit-code-from integration-tests
```

### Command 5.3: Extract Test Results from Container

```bash
docker cp <container-id>:/app/test-results ./local-test-results
docker cp <container-id>:/app/playwright-report ./local-playwright-report
```

---

## PART 6: Parallel Execution (Performance)

### Command 6.1: Run E2E Tests in Parallel

```bash
# Run with 4 workers (chromium + firefox + webkit + edge)
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --workers=4 \
  --project=chromium,firefox,webkit
```

### Command 6.2: Run Integration Tests in Parallel

```bash
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  -c Release \
  --logger "console;verbosity=minimal" \
  -- RunConfiguration.MaxCpuCount=4
```

---

## PART 7: Evidence Collection & Logging

### Command 7.1: Collect All Evidence Artifacts

```bash
#!/bin/bash

EVIDENCE_DIR="test-results/evidence-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$EVIDENCE_DIR"

# E2E Evidence
cp tests/e2e/test-results/*.png "$EVIDENCE_DIR/" 2>/dev/null || true
cp tests/e2e/playwright-report/* "$EVIDENCE_DIR/" 2>/dev/null || true

# Integration Evidence
cp tests/Qaly.IntegrationTests/test-results/*.trx "$EVIDENCE_DIR/" 2>/dev/null || true
cp tests/Qaly.IntegrationTests/*.log "$EVIDENCE_DIR/" 2>/dev/null || true

# API Logs (if available)
cp logs/api-*.log "$EVIDENCE_DIR/" 2>/dev/null || true

echo "Evidence collected in: $EVIDENCE_DIR"
zip -r "$EVIDENCE_DIR.zip" "$EVIDENCE_DIR"
```

### Command 7.2: Extract Console & Error Logs

```bash
# For E2E
npx playwright test t1-tr-01-task-url-regression.spec.ts \
  --reporter=list \
  2>&1 | tee test-results/e2e-console.log

# For Integration
dotnet test T1TR02CommentEvidenceNotificationTests.cs \
  -c Release \
  2>&1 | tee test-results/integration-console.log
```

---

## PART 8: CI/CD Pipeline Integration

### Command 8.1: GitHub Actions (Full Suite)

```yaml
name: T1-TR-01 & T1-TR-02 QA Tests

on: [push, pull_request]

jobs:
    e2e-tests:
        runs-on: ubuntu-latest
        steps:
            - uses: actions/checkout@v3
            - uses: actions/setup-node@v3
              with:
                  node-version: "18"
            - run: npm ci
            - run: npm run build
            - run: npm run test:e2e -- tests/e2e/t1-tr-01-task-url-regression.spec.ts
            - uses: actions/upload-artifact@v3
              if: failure()
              with:
                  name: e2e-report
                  path: tests/e2e/test-results/

    integration-tests:
        runs-on: ubuntu-latest
        steps:
            - uses: actions/checkout@v3
            - uses: actions/setup-dotnet@v3
              with:
                  dotnet-version: "8.0"
            - run: dotnet build -c Release
            - run: dotnet test tests/Qaly.IntegrationTests/T1TR02CommentEvidenceNotificationTests.cs -c Release
            - uses: actions/upload-artifact@v3
              if: failure()
              with:
                  name: integration-report
                  path: tests/Qaly.IntegrationTests/test-results/
```

---

## PART 9: Troubleshooting & Debug Commands

### Command 9.1: Health Check

```bash
# Check server is up
curl -v http://localhost:5000/Account/Login

# Check DB connectivity
dotnet ef dbcontext info --project src/Qaly.Infrastructure

# Check test environment
npm run test:env-check
```

### Command 9.2: Clean Build for Tests

```bash
# Complete clean
rm -rf node_modules bin obj .next dist
npm ci
dotnet clean -c Release
dotnet build -c Release

# Re-run tests
npm run test:e2e -- t1-tr-01-task-url-regression.spec.ts
dotnet test T1TR02CommentEvidenceNotificationTests.cs -c Release
```

### Command 9.3: Validate Test Structure

```bash
# Check E2E test file syntax
npx playwright test --list tests/e2e/t1-tr-01-task-url-regression.spec.ts

# Check Integration test file builds
dotnet build tests/Qaly.IntegrationTests/T1TR02CommentEvidenceNotificationTests.cs -v d
```

---

## PART 10: Success Criteria

A test run is **SUCCESSFUL** when:

✓ **E2E Tests**:

- All 10 tests in T1-TR-01 pass
- No console errors (except expected 404s)
- Screenshots captured for failed tests
- HTTP status codes correct (200, 201, 403, etc.)
- Canonical URLs verified

✓ **Integration Tests**:

- All 11 tests in T1-TR-02 pass
- DB persistence verified via read-back
- Authorization checks enforce ALLOW/DENY/ERROR states
- No silent failures
- Code coverage ≥ 80%

✓ **Combined**:

- No flaky tests (0 retries needed on second run)
- Evidence artifacts collected and archived
- PR comments updated with results

---

## PART 11: Rollback Procedure (If Tests Fail)

See [T1-TR-Rollback-Plan.md](./T1-TR-Rollback-Plan.md)

For immediate rollback:

```bash
git revert HEAD
git push origin codex/trung-t1-runtime-regression

# Notify reviewers in Slack/GitHub
```
