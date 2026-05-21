# QALY AI Test Plan v3.1 - Cost Control and Golden Dataset

## 1. Test modes

| Mode | Provider | Dùng khi nào |
|---|---|---|
| unit | mock | CI/local dev |
| integration | mock + cache | backend test |
| staging | AI API with low budget | final verify |
| demo | warmed cache + AI API + mock fallback | bảo vệ |

## 2. Golden dataset

Tạo tối thiểu 50 cases:

| Group | Cases |
|---|---:|
| meeting action item extraction | 10 |
| chat summary | 10 |
| task draft from message | 10 |
| task breakdown/checklist | 10 |
| assignee recommendation | 10 |

## 3. Acceptance thresholds

| Metric | Target |
|---|---:|
| JSON schema valid | >= 95% |
| action item correct enough | >= 85% |
| assignee top-3 contains expected | >= 90% |
| no direct official task creation without confirm | 100% |
| cache hit in repeated tests | >= 90% |
| API daily budget guard | 100% |
| sensitive meeting local-only route | 100% |

## 4. Cost tests

- Set `AI_DAILY_BUDGET_USD=0.01` and verify provider switches to mock.
- Repeat same prompt twice and verify second request cache hit.
- Submit huge transcript and verify truncation/chunking.
- Disable API key and verify fallback path.
- Submit invalid AI JSON and verify backend rejects/repairs once.

## 5. Demo failure drills

1. Turn off network: app still shows cached demo result.
2. Remove API key: mock provider works.
3. Stop ngrok: manual Meetily import works.
4. Stop Qdrant: SQL keyword fallback works.
5. High RAM on VPS: Qdrant profile disabled, P0 still runs.
