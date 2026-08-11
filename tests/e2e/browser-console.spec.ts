import { expect, test, type ConsoleMessage, type Page } from "@playwright/test";
import { adminEmail, adminPassword } from "./support/credentials";

test.setTimeout(150_000);

type BrowserProblem = {
    source: "console" | "pageerror" | "requestfailed";
    message: string;
};

function collectBrowserProblems(page: Page) {
    const problems: BrowserProblem[] = [];

    page.on("console", (message: ConsoleMessage) => {
        if (message.type() !== "error") return;
        problems.push({
            source: "console",
            message: `${message.text()} (${message.location().url || page.url()})`,
        });
    });

    page.on("pageerror", (error) => {
        problems.push({ source: "pageerror", message: error.stack ?? error.message });
    });

    page.on("requestfailed", (request) => {
        const failure = request.failure()?.errorText ?? "unknown network error";
        if (failure === "net::ERR_ABORTED") return;
        problems.push({
            source: "requestfailed",
            message: `${request.method()} ${request.url()}: ${failure}`,
        });
    });

    return problems;
}

async function login(page: Page) {
    await page.goto("/Account/Login", { waitUntil: "networkidle" });
    await page.locator('input[name="Email"]').fill(adminEmail);
    await page.locator('input[name="Password"]').fill(adminPassword);
    await Promise.all([
        page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
            timeout: 30_000,
            waitUntil: "domcontentloaded",
        }),
        page.locator("#loginForm button[type='submit']").click(),
    ]);
    await expect(page.locator(".shell-header")).toBeVisible();
}

function expectNoBrowserProblems(problems: BrowserProblem[]) {
    expect(
        problems,
        problems.map((problem) => `[${problem.source}] ${problem.message}`).join("\n"),
    ).toEqual([]);
}

test("public authentication pages have no browser errors", async ({ page }) => {
    const problems = collectBrowserProblems(page);

    await page.goto("/Account/Login", { waitUntil: "networkidle" });
    await expect(page.locator("#loginForm")).toBeVisible();

    await page.goto("/Account/Register", { waitUntil: "networkidle" });
    await expect(page.locator("#registerForm")).toBeVisible();

    expectNoBrowserProblems(problems);
});

test("main authenticated routes have no browser errors", async ({ page }) => {
    await login(page);

    for (const route of ["/dashboard", "/projects", "/tasks", "/teams", "/analytics"]) {
        const routePage = await page.context().newPage();
        const problems = collectBrowserProblems(routePage);

        await routePage.goto(route, { waitUntil: "domcontentloaded" });
        await expect(routePage.locator(".shell-header")).toBeVisible();
        await routePage.waitForTimeout(1_000);
        expectNoBrowserProblems(problems);

        await routePage.close();
    }
});

test("group detail route renders without a blank screen", async ({ page }) => {
    await login(page);
    const groupId =
        process.env.E2E_GROUP_ID ?? "d9a15354-b812-4431-93bd-8424770aad2e";

    await page.goto(`/groups/${groupId}`, { waitUntil: "domcontentloaded" });
    await expect(page.locator(".shell-header")).toBeVisible();
    await expect(page.locator(".groups-workspace")).toBeVisible();
});
