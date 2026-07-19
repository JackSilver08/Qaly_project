# Qaly Release Governance

## Purpose

This document defines the minimum governance gate for Qaly releases and supports task `T1-LG-02`.

Release governance is not a feature screen. It is the rule set that keeps `main`, production config, database migrations, and release evidence consistent before a demo or deployment.

## Branch And Review Rules

- Work on short-lived branches.
- Open a pull request into `main`.
- Keep `main` protected and avoid direct pushes.
- CODEOWNERS must request review for changed areas.
- At least one owner review is required before merging.
- Migration, privacy, security, AI provider, and recovery changes require explicit owner attention.

## Required CI Gates

The release candidate must pass:

- NuGet restore;
- `npm ci`;
- frontend typecheck;
- frontend build;
- committed frontend bundle check;
- configuration safety check;
- configuration parity check;
- Docker Compose validation;
- backend build;
- unit tests with coverage;
- integration tests with coverage;
- required Playwright smoke tests;
- dependency vulnerability check;
- production container build.

## Configuration Parity

When a new key is added to `src/Qaly.Web/appsettings.json`, the production example must be updated in `src/Qaly.Web/appsettings.Production.example.json`.

The script below enforces that rule:

```powershell
.\scripts\check-config-parity.ps1
```

Production secrets must stay as placeholders or environment variables. Never commit real passwords, API keys, tokens, private keys, or production connection strings.

## Release Evidence

Attach the following evidence to the release record:

- CI run link and commit SHA;
- production image tag and image digest;
- applied migration list;
- clean restore evidence for risky or destructive migrations;
- smoke test result;
- known issues and rollback plan;
- feature flags enabled for the release;
- owner approval record.

## Release Checklist

1. Branch is up to date with `main`.
2. Pull request has owner review through CODEOWNERS.
3. Required CI gates are green.
4. `appsettings.Production.example.json` includes all production-visible config keys.
5. No tracked secret file is present.
6. New migrations have rollback or recovery notes.
7. Clean restore evidence exists when schema/data risk is high.
8. Release notes mention user-facing changes and operational risks.
9. Release tag points to the exact merged commit.
10. Rollback target is known before deployment starts.

## Rollback Rule

Rollback must be selected before deployment:

- feature flag off;
- application image rollback;
- compensating migration;
- database restore from verified backup.

Database restore is the last recovery path. Do not treat it as the normal rollback for every bug.
