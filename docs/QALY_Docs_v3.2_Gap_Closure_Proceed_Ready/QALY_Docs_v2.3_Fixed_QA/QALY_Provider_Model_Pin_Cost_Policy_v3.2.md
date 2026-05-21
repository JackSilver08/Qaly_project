# QALY Provider/Model Pin and Cost Policy v3.2

## 1. Policy

Do not depend on one AI provider. Pin provider/model in configuration and re-check official pricing before buying credits.

## 2. Provider roles

| Role | Provider/model | Purpose | Notes |
|---|---|---|---|
| Primary quality | OpenAI `gpt-5.4-mini` | meeting extraction, assignee explanation, checklist | quality-first demo path |
| Low-cost mode | Gemini `gemini-2.5-flash-lite` | chat summary, simple extraction, batch/golden tests | very cheap per-token option |
| Local fallback | Ollama 7B/8B Q4 | offline/dev/sensitive fallback | laptop only, max 1 concurrent |
| CI/demo fallback | Mock JSON provider | deterministic tests/offline defense | zero cost |

## 3. Current reference pricing snapshot

Snapshot date: 20/05/2026. Update before purchasing API credits.

| Provider | Model | Input USD/1M | Output USD/1M | Source note |
|---|---|---:|---:|---|
| OpenAI | GPT-5.4 mini | 0.75 | 4.50 | standard processing under 270K context |
| OpenAI | GPT-5.4 | 2.50 | 15.00 | not default for P0 due cost |
| OpenAI | GPT-5.5 | 5.00 | 30.00 | do not use for P0 unless exceptional |
| Gemini | gemini-2.5-flash-lite | 0.10 | 0.40 | low-cost mode |
| Gemini | gemini-2.5-flash | 0.30 | 2.50 | mid-cost alternative |

## 4. Budget caps

| Environment | Daily cap | Monthly cap | Behavior |
|---|---:|---:|---|
| Local dev | 0.50 USD | 5 USD | prefer mock/cache |
| Team staging | 2 USD | 30 USD | hard stop |
| Final demo week | 5 USD | 50 USD | warm cache first |
| Worst-case reserve | 10 USD | 100 USD | PM approval required |

## 5. Cost reduction rules

1. Use mock provider for CI/unit tests.
2. Use prompt cache for repeated demo flows.
3. Send summaries/aggregated metrics instead of raw full transcript where possible.
4. Chunk long transcript; summarize-map-reduce only if needed.
5. Use Gemini Flash-Lite or mock for low-risk extraction.
6. Use OpenAI mini for advisor-facing demo where quality matters.
7. Never send sensitive transcript to cloud without override.
8. Batch async/golden tests when provider supports batch discount.

## 6. Provider failover order

```text
cache hit -> mock if environment=ci -> primary API -> low-cost API -> local Ollama -> manual fallback
```

For defense demo:

```text
warm cache -> primary API if network ok -> mock fallback
```

## 7. Purchase recommendation

Base recommended:

- 10-30 USD initial API credit.
- No paid GPU VPS.
- Use school VPS 8GB for app only.
- Use laptop RTX4060 for Meetily/local fallback.

Safe demo reserve:

- 50-100 USD maximum reserve if advisor expects repeated AI demos.
- Enforce monthly budget in app to avoid accidental overrun.
