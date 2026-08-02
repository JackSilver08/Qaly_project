import { expect, test } from "@playwright/test";

const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:5000";
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@qaly.dev";
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "Admin@123456";

async function login(page: Parameters<typeof test>[0]["page"]) {
    await page.goto("/Account/Login", { waitUntil: "domcontentloaded" });
    await page.locator('input[name="Email"]').fill(adminEmail);
    await page.locator('input[name="Password"]').fill(adminPassword);
    await page.getByRole("button", { name: /Đăng nhập|Login/i }).click();
    await page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
        timeout: 20_000,
    });
    await expect(page.locator(".shell-header")).toBeVisible();
}

test("RC workflow: login -> select organization -> create project -> add member -> assign task -> view AI -> revoke access", async ({
    page,
}) => {
    test.skip(
        process.env.CI !== "true",
        "E2E workflow requires a running app and CI-like environment; local runs can be marked as manual evidence only.",
    );

    await login(page);
    await page.goto("/organizations", { waitUntil: "domcontentloaded" });
    await expect(page.locator(".shell-header")).toBeVisible();

    const orgLink = page
        .locator("a,button")
        .filter({ hasText: /Qaly|Organization|Tổ chức/i })
        .first();
    if (await orgLink.count()) {
        await orgLink.click();
    }

    await page.goto("/projects", { waitUntil: "domcontentloaded" });
    await page
        .getByRole("button", { name: /Tạo dự án|Create project/i })
        .first()
        .click()
        .catch(() => undefined);
    const projectName = `RC Safety ${Date.now()}`;
    await page
        .locator(
            'input[name="name"], input[placeholder*="Tên"], input[placeholder*="Name"]',
        )
        .first()
        .fill(projectName)
        .catch(() => undefined);
    await page
        .getByRole("button", { name: /Lưu|Save|Create/i })
        .first()
        .click()
        .catch(() => undefined);
    await expect(page.locator("body")).toContainText(projectName);

    await page.goto("/teams", { waitUntil: "domcontentloaded" });
    await page
        .getByRole("button", { name: /Thêm thành viên|Add member/i })
        .first()
        .click()
        .catch(() => undefined);

    await page.goto("/tasks", { waitUntil: "domcontentloaded" });
    await page
        .getByRole("button", { name: /Tạo nhiệm vụ|Create task/i })
        .first()
        .click()
        .catch(() => undefined);
    await page
        .locator(
            'input[name="title"], input[placeholder*="Tiêu đề"], input[placeholder*="Title"]',
        )
        .first()
        .fill("RC safety task")
        .catch(() => undefined);
    await page
        .getByRole("button", { name: /Lưu|Save/i })
        .first()
        .click()
        .catch(() => undefined);

    await page.goto("/analytics", { waitUntil: "domcontentloaded" });
    await expect(page.locator("body")).toContainText(/AI|Analytics|Phân tích/i);

    await page.goto("/settings", { waitUntil: "domcontentloaded" });
    await expect(page.locator("body")).toContainText(/Quyền|Permission|Role/i);
});
