# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: qaly.smoke.spec.ts >> should login successfully
- Location: tests\e2e\qaly.smoke.spec.ts:231:1

# Error details

```
Error: page.goto: net::ERR_CONNECTION_REFUSED at http://127.0.0.1:5000/Account/Login
Call log:
  - navigating to "http://127.0.0.1:5000/Account/Login", waiting until "domcontentloaded"

```

# Test source

```ts
  1   | import {
  2   |     expect,
  3   |     test,
  4   |     type Browser,
  5   |     type BrowserContext,
  6   |     type Page,
  7   | } from "@playwright/test";
  8   | 
  9   | test.describe.configure({ mode: "serial" });
  10  | 
  11  | const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@qaly.dev";
  12  | const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "Qaly@E2E2026!";
  13  | const secondaryEmail = process.env.E2E_MEMBER_EMAIL ?? adminEmail;
  14  | const secondaryPassword = process.env.E2E_MEMBER_PASSWORD ?? adminPassword;
  15  | const wikiFixturePath = "tests/e2e/fixtures/wiki-smoke.md";
  16  | 
  17  | type ApiResult<T> = {
  18  |     isSuccess: boolean;
  19  |     data: T | null;
  20  |     error: string | null;
  21  | };
  22  | 
  23  | type GroupDto = {
  24  |     id: string;
  25  |     name: string;
  26  | };
  27  | 
  28  | type ProjectDto = {
  29  |     id: string;
  30  |     name: string;
  31  | };
  32  | 
  33  | type UserDto = {
  34  |     id: string;
  35  |     email: string;
  36  | };
  37  | 
  38  | type ApiResponseLike = {
  39  |     ok(): boolean;
  40  |     status(): number;
  41  |     url(): string;
  42  |     json?(): Promise<unknown>;
  43  |     text(): Promise<string>;
  44  | };
  45  | 
  46  | function uniqueName(prefix: string) {
  47  |     const suffix = Math.random().toString(36).slice(2, 8);
  48  |     return `${prefix} ${Date.now()} ${suffix}`;
  49  | }
  50  | 
  51  | async function login(
  52  |     page: Page,
  53  |     email = adminEmail,
  54  |     password = adminPassword,
  55  | ) {
> 56  |     await page.goto("/Account/Login", { waitUntil: "domcontentloaded" });
      |                ^ Error: page.goto: net::ERR_CONNECTION_REFUSED at http://127.0.0.1:5000/Account/Login
  57  |     await expect(page.locator("#loginForm")).toBeVisible();
  58  |     await page.locator('input[name="Email"]').fill(email);
  59  |     await page.locator('input[name="Password"]').fill(password);
  60  |     await page.getByRole("button", { name: /Đăng nhập|Login/i }).click();
  61  |     await page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
  62  |         timeout: 20_000,
  63  |         waitUntil: "domcontentloaded",
  64  |     });
  65  |     await page
  66  |         .locator(".welcome-overlay")
  67  |         .waitFor({ state: "hidden", timeout: 5_000 })
  68  |         .catch(() => undefined);
  69  |     await expect(page.locator(".shell-header")).toBeVisible();
  70  | }
  71  | 
  72  | async function responseBody(response: ApiResponseLike) {
  73  |     try {
  74  |         return await response.json?.();
  75  |     } catch {
  76  |         const text = await response.text();
  77  |         if (!text.trim()) return null;
  78  | 
  79  |         try {
  80  |             return JSON.parse(text) as unknown;
  81  |         } catch {
  82  |             return text;
  83  |         }
  84  |     }
  85  | }
  86  | 
  87  | function responseErrorMessage(response: ApiResponseLike, body: unknown) {
  88  |     let bodyMessage = "";
  89  |     if (body && typeof body === "object" && "error" in body) {
  90  |         bodyMessage = `: ${String(body.error)}`;
  91  |     } else if (typeof body === "string" && body.trim()) {
  92  |         bodyMessage = `: ${body.slice(0, 300)}`;
  93  |     }
  94  | 
  95  |     return `API ${response.url()} trả về ${response.status()}${bodyMessage}`;
  96  | }
  97  | 
  98  | async function apiResult<T>(response: ApiResponseLike): Promise<T> {
  99  |     const body = await responseBody(response);
  100 | 
  101 |     expect(
  102 |         response.ok(),
  103 |         responseErrorMessage(response, body),
  104 |     ).toBeTruthy();
  105 | 
  106 |     if (body && typeof body === "object" && "isSuccess" in body) {
  107 |         const result = body as ApiResult<T>;
  108 |         expect(result.isSuccess, result.error ?? "API result failed").toBeTruthy();
  109 |         expect(body.data, "API result must include data").not.toBeNull();
  110 |         return result.data as T;
  111 |     }
  112 | 
  113 |     return body as T;
  114 | }
  115 | 
  116 | async function apiCommand(response: ApiResponseLike) {
  117 |     const body = await responseBody(response);
  118 | 
  119 |     expect(
  120 |         response.ok(),
  121 |         responseErrorMessage(response, body),
  122 |     ).toBeTruthy();
  123 | 
  124 |     if (body && typeof body === "object" && "isSuccess" in body) {
  125 |         const result = body as ApiResult<unknown>;
  126 |         expect(result.isSuccess, result.error ?? "API command failed").toBeTruthy();
  127 |     }
  128 | }
  129 | 
  130 | async function createGroupViaApi(page: Page, name: string) {
  131 |     return apiResult<GroupDto>(
  132 |         await page.request.post("/api/groups", {
  133 |             data: {
  134 |                 name,
  135 |                 color: "#2563eb",
  136 |             },
  137 |         }),
  138 |     );
  139 | }
  140 | 
  141 | async function createProjectViaApi(page: Page, name: string) {
  142 |     return apiResult<ProjectDto>(
  143 |         await page.request.post("/api/projects", {
  144 |             data: {
  145 |                 name,
  146 |                 code: null,
  147 |                 description: "DH-02 E2E smoke project",
  148 |                 logoUrl: null,
  149 |                 startDate: null,
  150 |                 endDate: null,
  151 |                 organizationId: null,
  152 |                 sourceGroupId: null,
  153 |             },
  154 |         }),
  155 |     );
  156 | }
```