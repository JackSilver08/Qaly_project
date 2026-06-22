# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: browser-console.spec.ts >> public authentication pages have no browser errors
- Location: tests\e2e\browser-console.spec.ts:57:1

# Error details

```
Error: page.goto: net::ERR_CONNECTION_REFUSED at http://127.0.0.1:5000/Account/Login
Call log:
  - navigating to "http://127.0.0.1:5000/Account/Login", waiting until "networkidle"

```

# Test source

```ts
  1  | import { expect, test, type ConsoleMessage, type Page } from "@playwright/test";
  2  | 
  3  | const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@qaly.dev";
  4  | const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "Qaly@E2E2026!";
  5  | 
  6  | type BrowserProblem = {
  7  |     source: "console" | "pageerror" | "requestfailed";
  8  |     message: string;
  9  | };
  10 | 
  11 | function collectBrowserProblems(page: Page) {
  12 |     const problems: BrowserProblem[] = [];
  13 | 
  14 |     page.on("console", (message: ConsoleMessage) => {
  15 |         if (message.type() !== "error") return;
  16 |         problems.push({
  17 |             source: "console",
  18 |             message: `${message.text()} (${message.location().url || page.url()})`,
  19 |         });
  20 |     });
  21 | 
  22 |     page.on("pageerror", (error) => {
  23 |         problems.push({ source: "pageerror", message: error.stack ?? error.message });
  24 |     });
  25 | 
  26 |     page.on("requestfailed", (request) => {
  27 |         const failure = request.failure()?.errorText ?? "unknown network error";
  28 |         if (failure === "net::ERR_ABORTED") return;
  29 |         problems.push({
  30 |             source: "requestfailed",
  31 |             message: `${request.method()} ${request.url()}: ${failure}`,
  32 |         });
  33 |     });
  34 | 
  35 |     return problems;
  36 | }
  37 | 
  38 | async function login(page: Page) {
  39 |     await page.goto("/Account/Login", { waitUntil: "networkidle" });
  40 |     await page.locator('input[name="Email"]').fill(adminEmail);
  41 |     await page.locator('input[name="Password"]').fill(adminPassword);
  42 |     await page.locator("#loginForm button[type='submit']").click();
  43 |     await page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
  44 |         timeout: 20_000,
  45 |         waitUntil: "domcontentloaded",
  46 |     });
  47 |     await expect(page.locator(".shell-header")).toBeVisible();
  48 | }
  49 | 
  50 | function expectNoBrowserProblems(problems: BrowserProblem[]) {
  51 |     expect(
  52 |         problems,
  53 |         problems.map((problem) => `[${problem.source}] ${problem.message}`).join("\n"),
  54 |     ).toEqual([]);
  55 | }
  56 | 
  57 | test("public authentication pages have no browser errors", async ({ page }) => {
  58 |     const problems = collectBrowserProblems(page);
  59 | 
> 60 |     await page.goto("/Account/Login", { waitUntil: "networkidle" });
     |                ^ Error: page.goto: net::ERR_CONNECTION_REFUSED at http://127.0.0.1:5000/Account/Login
  61 |     await expect(page.locator("#loginForm")).toBeVisible();
  62 | 
  63 |     await page.goto("/Account/Register", { waitUntil: "networkidle" });
  64 |     await expect(page.locator("#registerForm")).toBeVisible();
  65 | 
  66 |     expectNoBrowserProblems(problems);
  67 | });
  68 | 
  69 | test("main authenticated routes have no browser errors", async ({ page }) => {
  70 |     await login(page);
  71 | 
  72 |     for (const route of ["/dashboard", "/projects", "/tasks", "/teams", "/analytics"]) {
  73 |         const routePage = await page.context().newPage();
  74 |         const problems = collectBrowserProblems(routePage);
  75 | 
  76 |         await routePage.goto(route, { waitUntil: "networkidle" });
  77 |         await expect(routePage.locator(".shell-header")).toBeVisible();
  78 |         expectNoBrowserProblems(problems);
  79 | 
  80 |         await routePage.close();
  81 |     }
  82 | });
  83 | 
```