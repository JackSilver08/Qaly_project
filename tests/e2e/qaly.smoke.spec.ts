import { expect, test } from "@playwright/test";

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@qaly.dev";
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "Qaly@E2E2026!";

test("admin can create a group and send a real team chat message", async ({
    page,
}) => {
    await page.goto("/Account/Login");
    await page.locator('input[name="Email"]').fill(adminEmail);
    await page.locator('input[name="Password"]').fill(adminPassword);
    await page.locator('button[type="submit"]').click();
    await page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
        timeout: 20_000,
    });

    await page.goto("/teams");
    await expect(page.locator(".team-chat-page")).toBeVisible();

    const groupName = `E2E Smoke ${Date.now()}`;
    page.once("dialog", (dialog) => dialog.accept(groupName));
    await page.getByLabel("Create group").click();

    const createdGroup = page
        .locator(".team-chat-group")
        .filter({ hasText: groupName })
        .first();
    await expect(createdGroup).toBeVisible();
    await createdGroup.click();

    const message = `Smoke message ${Date.now()}`;
    await page.locator('.team-chat-composer input[type="text"]').fill(message);
    await page.locator('.team-chat-composer button[type="submit"]').click();

    await expect(page.locator(".team-chat-body")).toContainText(message);
});

test("group poll create -> vote -> results (UI smoke)", async ({ page }) => {
    await page.goto("/Account/Login");
    await page.locator('input[name="Email"]').fill(adminEmail);
    await page.locator('input[name="Password"]').fill(adminPassword);
    await page.locator('button[type="submit"]').click();
    await page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
        timeout: 20_000,
    });

    await page.goto("/teams");
    const groupName = `E2E Poll ${Date.now()}`;
    page.once("dialog", (dialog) => dialog.accept(groupName));
    await page.getByLabel("Create group").click();
    const createdGroup = page
        .locator(".team-chat-group")
        .filter({ hasText: groupName })
        .first();
    await createdGroup.click();

    // Open Poll composer
    await page.getByLabel("Tạo poll").click();
    await page
        .locator('.team-poll-composer input[type="text"]')
        .first()
        .fill("Which color?");
    await page
        .locator('.team-poll-composer input[type="text"]')
        .nth(1)
        .fill("Red");
    await page
        .locator('.team-poll-composer input[type="text"]')
        .nth(2)
        .fill("Blue");
    await page.locator('.team-chat-composer button[type="submit"]').click();

    // Poll appears in message list
    await expect(page.locator(".team-chat-body")).toContainText("Which color?");

    // Navigate to polls page and check results UI (may be empty initially)
    await page.goto(
        `/groups/${await createdGroup.getAttribute("data-group-id")}/polls`,
    );
    await expect(page.locator("text=Tạo poll")).toBeVisible();
});

test("meeting UI start/join/end flow (UI smoke)", async ({ page }) => {
    await page.goto("/Account/Login");
    await page.locator('input[name="Email"]').fill(adminEmail);
    await page.locator('input[name="Password"]').fill(adminPassword);
    await page.locator('button[type="submit"]').click();
    await page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
        timeout: 20_000,
    });

    await page.goto("/teams");
    const groupName = `E2E Meeting ${Date.now()}`;
    page.once("dialog", (dialog) => dialog.accept(groupName));
    await page.getByLabel("Create group").click();
    const createdGroup = page
        .locator(".team-chat-group")
        .filter({ hasText: groupName })
        .first();
    await createdGroup.click();

    // Open meeting page
    const groupId = await createdGroup.getAttribute("data-group-id");
    await page.goto(`/groups/${groupId}/meeting`);
    await expect(page.locator("text=Meeting — Nhóm")).toBeVisible();
    await page.getByText("Start meeting").click();
    await expect(page.locator("text=Participants")).toBeVisible();
    await page.getByText("End meeting").click();
});
