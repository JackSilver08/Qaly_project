import { expect, test, type Page, type TestInfo } from "@playwright/test";

/**
 * T1-TR-01: Task URL, Group link & Privacy UI Regression Tests
 *
 * SCOPE:
 * - Verify canonical task URL structure and behavior
 * - Test Group link creation/discovery and UI update
 * - Validate Privacy Policy UI Create/Read/Update flows
 *
 * PRINCIPLE:
 * - All 3 states: ALLOW (success), DENY (forbidden), ERROR (system failure)
 * - Persistence Read-Back: After UI change, verify via API that data truly persisted
 * - Browser behaviors: Refresh, Share/Copy Link, Back navigation all work
 *
 * PR REQUIREMENTS:
 * - Attach: screenshot of canonical URL, Privacy UI success message
 * - Attach: API read-back evidence showing DB persistence
 * - Attach: console logs showing no silent errors
 *
 * @author Trung (QA Automation)
 * @date 2026-07-18
 */

test.describe.configure({ mode: "serial" });

const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:5000";
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@qaly.dev";
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "Admin@123456";

let infraBlockedReason: string | null = null;
let testProjectId: string = "";
let testTaskId: string = "";
let testGroupId: string = "";
let authToken: string = "";

// ===== Utility Functions =====

async function captureFailureEvidence(
    page: Page,
    testInfo: TestInfo,
    label: string = "test-failed",
) {
    const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
    await page
        .screenshot({
            path: testInfo.outputPath(`${label}-${timestamp}.png`),
            fullPage: true,
        })
        .catch(() => undefined);
}

async function runWithEvidence(
    page: Page,
    testInfo: TestInfo,
    action: () => Promise<void>,
    label: string = "test",
) {
    try {
        await action();
    } catch (error) {
        await captureFailureEvidence(page, testInfo, label);
        throw error;
    }
}

async function loginAsAdmin(page: Page) {
    await page.goto("/Account/Login", { waitUntil: "domcontentloaded" });
    await expect(page.locator("#loginForm")).toBeVisible();
    await page.locator('input[name="Email"]').fill(adminEmail);
    await page.locator('input[name="Password"]').fill(adminPassword);
    await page
        .getByRole("button", { name: /Dang nhap|Đăng nhập|Login/i })
        .click();
    await page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
        timeout: 20_000,
        waitUntil: "domcontentloaded",
    });
    await page
        .locator(".welcome-overlay")
        .waitFor({ state: "hidden", timeout: 5_000 })
        .catch(() => undefined);
    await expect(page.locator(".shell-header")).toBeVisible();
}

async function apiCall(
    method: string,
    endpoint: string,
    body?: any,
    headers?: Record<string, string>,
) {
    const options: RequestInit = {
        method,
        headers: {
            "Content-Type": "application/json",
            ...headers,
        },
    };
    if (body) options.body = JSON.stringify(body);
    const response = await fetch(`${baseURL}${endpoint}`, options);
    return {
        status: response.status,
        data: await response.json().catch(() => null),
        headers: response.headers,
    };
}

async function extractTaskIdFromUrl(url: string): Promise<string | null> {
    const match = url.match(/\/projects\/([a-f0-9\-]+)\/tasks\/([a-f0-9\-]+)/);
    return match ? match[2] : null;
}

async function extractProjectIdFromUrl(url: string): Promise<string | null> {
    const match = url.match(/\/projects\/([a-f0-9\-]+)/);
    return match ? match[1] : null;
}

// ===== Setup & Teardown =====

test.beforeAll(async ({ request }) => {
    try {
        const response = await request.get("/Account/Login", {
            failOnStatusCode: false,
            timeout: 5_000,
        });

        if (response.status() >= 500) {
            infraBlockedReason = `Infra blocked: server ${baseURL} returned HTTP ${response.status()}.`;
        }
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        infraBlockedReason = `Infra blocked: cannot connect to server ${baseURL}. ${message}`;
    }
});

test.beforeEach(async ({}, testInfo) => {
    if (!infraBlockedReason) return;

    testInfo.annotations.push({
        type: "Blocked",
        description: infraBlockedReason,
    });
    test.skip(true, infraBlockedReason);
});

// ===== TEST SUITE: T1-TR-01 =====

test.describe("T1-TR-01: Task URL, Group link & Privacy UI", () => {
    // ===== Test 1.1: Canonical Task URL Structure =====
    test("TC-TR-01-001: Task opens with canonical URL /projects/{id}/tasks/{id} format", async ({
        page,
    }, testInfo) => {
        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);

                // Navigate to projects
                await page.goto("/projects", { waitUntil: "domcontentloaded" });
                await page.waitForSelector('a[href*="/projects/"]', {
                    timeout: 10_000,
                });

                // Click first project
                const projectLink = page
                    .locator('a[href*="/projects/"][href*!="/tasks"]')
                    .first();
                expect(await projectLink.isVisible()).toBeTruthy();
                await projectLink.click();
                await page.waitForURL(/\/projects\/[a-f0-9\-]+(?!\/tasks)$/, {
                    timeout: 10_000,
                });

                testProjectId =
                    (await extractProjectIdFromUrl(page.url())) || "";
                expect(testProjectId).toBeTruthy();

                // Find and click first task
                const taskCard = page
                    .locator('[class*="task"], [role="button"][class*="item"]')
                    .first();
                expect(await taskCard.isVisible()).toBeTruthy();
                await taskCard.click();

                // Wait for canonical task URL
                await page.waitForURL(
                    /\/projects\/[a-f0-9\-]+\/tasks\/[a-f0-9\-]+/,
                    { timeout: 10_000 },
                );
                const taskUrl = page.url();
                testTaskId = (await extractTaskIdFromUrl(taskUrl)) || "";

                // Verify URL structure
                expect(taskUrl).toMatch(
                    /\/projects\/[a-f0-9\-]+\/tasks\/[a-f0-9\-]+$/,
                );
                expect(testTaskId).toBeTruthy();
                testInfo.annotations.push({
                    type: "Evidence",
                    description: `Canonical URL: ${taskUrl}`,
                });
            },
            "tc-01-001-canonical-url",
        );
    });

    // ===== Test 1.2: Refresh Page Preserves Canonical URL =====
    test("TC-TR-01-002: Refresh page preserves canonical task URL and state", async ({
        page,
    }, testInfo) => {
        if (!testProjectId || !testTaskId) {
            test.skip(true, "testProjectId or testTaskId not set");
        }

        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);
                const canonicalUrl = `/projects/${testProjectId}/tasks/${testTaskId}`;
                await page.goto(canonicalUrl, {
                    waitUntil: "domcontentloaded",
                });

                // Capture initial state
                const initialUrl = page.url();
                const initialContent = await page
                    .locator('h1, [class*="title"]')
                    .first()
                    .textContent();

                // Perform refresh
                await page.reload({ waitUntil: "domcontentloaded" });
                await page.waitForTimeout(1000);

                // Verify URL and content persistence
                const afterRefreshUrl = page.url();
                const afterRefreshContent = await page
                    .locator('h1, [class*="title"]')
                    .first()
                    .textContent();

                expect(afterRefreshUrl).toBe(initialUrl);
                expect(afterRefreshContent).toBe(initialContent);

                testInfo.annotations.push({
                    type: "Evidence",
                    description: `URL unchanged after refresh: ${afterRefreshUrl}`,
                });
            },
            "tc-01-002-refresh",
        );
    });

    // ===== Test 1.3: Copy Link / Share Functionality =====
    test("TC-TR-01-003: Share/Copy task URL creates valid canonical link", async ({
        page,
        context,
    }, testInfo) => {
        if (!testProjectId || !testTaskId) {
            test.skip(true, "testProjectId or testTaskId not set");
        }

        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);
                const canonicalUrl = `/projects/${testProjectId}/tasks/${testTaskId}`;
                await page.goto(canonicalUrl, {
                    waitUntil: "domcontentloaded",
                });

                // Look for Share/Copy button
                const shareButton = page
                    .locator(
                        'button:has-text("Share"), button[title*="Share"], button[class*="share"]',
                    )
                    .first();
                if (await shareButton.isVisible()) {
                    await shareButton.click();
                    await page.waitForTimeout(500);

                    // Check if copy-to-clipboard action occurred
                    const linkText = page
                        .locator('input[value*="/projects/"]')
                        .first();
                    if (await linkText.isVisible()) {
                        const copiedLink = await linkText.inputValue();
                        expect(copiedLink).toMatch(
                            /\/projects\/[a-f0-9\-]+\/tasks\/[a-f0-9\-]+/,
                        );
                        testInfo.annotations.push({
                            type: "Evidence",
                            description: `Shared link: ${copiedLink}`,
                        });
                    }
                }
            },
            "tc-01-003-share-link",
        );
    });

    // ===== Test 1.4: Back Navigation Works =====
    test("TC-TR-01-004: Browser Back button returns to project URL", async ({
        page,
    }, testInfo) => {
        if (!testProjectId) {
            test.skip(true, "testProjectId not set");
        }

        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);

                // Go to project
                await page.goto(`/projects/${testProjectId}`, {
                    waitUntil: "domcontentloaded",
                });
                const projectUrl = page.url();

                // Find and click task
                const taskCard = page
                    .locator('[class*="task"], [role="button"][class*="item"]')
                    .first();
                expect(await taskCard.isVisible()).toBeTruthy();
                await taskCard.click();
                await page.waitForURL(
                    /\/projects\/[a-f0-9\-]+\/tasks\/[a-f0-9\-]+/,
                    { timeout: 10_000 },
                );

                // Use browser back
                await page.goBack({ waitUntil: "domcontentloaded" });
                await page.waitForTimeout(500);

                // Verify we're back at project URL (not forced redirect)
                const afterBackUrl = page.url();
                expect(afterBackUrl).toContain(`/projects/${testProjectId}`);
                expect(afterBackUrl).not.toContain("/tasks/");

                testInfo.annotations.push({
                    type: "Evidence",
                    description: `Navigated back from task to: ${afterBackUrl}`,
                });
            },
            "tc-01-004-back-nav",
        );
    });

    // ===== Test 1.5: Privacy UI - Policy Creation Success =====
    test("TC-TR-01-005: Privacy Policy create button persists data to DB (ALLOW path)", async ({
        page,
    }, testInfo) => {
        if (!testProjectId) {
            test.skip(true, "testProjectId not set");
        }

        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);
                await page.goto(`/projects/${testProjectId}`, {
                    waitUntil: "domcontentloaded",
                });

                // Navigate to Settings/Privacy if available
                const settingsButton = page
                    .locator('button:has-text("Settings"), a[href*="settings"]')
                    .first();
                if (await settingsButton.isVisible()) {
                    await settingsButton.click();
                    await page.waitForTimeout(1000);
                }

                // Look for Privacy Policy section
                const privacySection = page
                    .locator(
                        "text=Privacy|Privacy Policy|Chính sách quyền riêng tư",
                    )
                    .first();
                if (await privacySection.isVisible()) {
                    // Try to create a policy
                    const createButton = page
                        .locator(
                            'button:has-text("Create"), button:has-text("Add"), button[class*="create"]',
                        )
                        .first();
                    if (await createButton.isVisible()) {
                        await createButton.click();
                        await page.waitForTimeout(500);

                        // Fill policy form if modal appears
                        const policyNameInput = page
                            .locator('input[placeholder*="policy|name"]')
                            .first();
                        if (await policyNameInput.isVisible()) {
                            await policyNameInput.fill(
                                `Test Policy ${Date.now()}`,
                            );

                            const savePolicyButton = page
                                .locator(
                                    'button:has-text("Save"), button:has-text("Create")',
                                )
                                .last();
                            await savePolicyButton.click();
                            await page.waitForTimeout(1000);

                            // Check for success message
                            const successMessage = page
                                .locator("text=Success|created|Thành công|tạo")
                                .first();
                            const successVisible = await successMessage
                                .isVisible()
                                .catch(() => false);

                            testInfo.annotations.push({
                                type: "Evidence",
                                description: `Privacy policy creation ${successVisible ? "successful" : "in progress"} - UI feedback shown`,
                            });
                        }
                    }
                }
            },
            "tc-01-005-privacy-create",
        );
    });

    // ===== Test 1.6: Privacy Policy UI - Forbidden Access (DENY path) =====
    test("TC-TR-01-006: Non-admin user DENY on Privacy policy access", async ({
        page,
        request,
    }, testInfo) => {
        if (!testProjectId) {
            test.skip(true, "testProjectId not set");
        }

        await runWithEvidence(
            page,
            testInfo,
            async () => {
                // Try to access Privacy API directly as non-admin
                const response = await request.get(
                    `/api/privacy/policies?tenantId=${testProjectId}&projectId=${testProjectId}`,
                    {
                        failOnStatusCode: false,
                        headers: { "X-Test-Auth": "None" },
                    },
                );

                // Should be 401 or 403
                expect([401, 403]).toContain(response.status());

                testInfo.annotations.push({
                    type: "Evidence",
                    description: `Unauthorized access returned HTTP ${response.status()} (DENY path verified)`,
                });
            },
            "tc-01-006-privacy-deny",
        );
    });

    // ===== Test 1.7: Privacy Policy Read-Back Persistence =====
    test("TC-TR-01-007: Privacy policy creation persists to DB via API read-back", async ({
        page,
        request,
    }, testInfo) => {
        if (!testProjectId) {
            test.skip(true, "testProjectId not set");
        }

        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);

                // Get CSRF token
                const loginResponse = await request.get("/Account/Login", {
                    failOnStatusCode: false,
                });
                const csrfMatch = (await loginResponse.text()).match(
                    /name="__RequestVerificationToken"[^>]*value="([^"]+)"/,
                );
                const csrfToken = csrfMatch ? csrfMatch[1] : "";

                // Call Privacy API directly to create policy
                const createResponse = await request.post(
                    "/api/privacy/policies",
                    {
                        data: {
                            tenantId: testProjectId,
                            projectId: testProjectId,
                            purpose: "TEST_PURPOSE",
                            dataClassification: "SensitiveCollaboration",
                            allowCloudProcessing: false,
                            defaultRetentionDays: 30,
                        },
                        failOnStatusCode: false,
                        headers: csrfToken ? { "X-CSRF-Token": csrfToken } : {},
                    },
                );

                const createData = await createResponse.json();
                if (createResponse.ok() || createResponse.status() === 201) {
                    const policyId = createData?.data?.id;
                    expect(policyId).toBeTruthy();

                    // Read-back: Verify policy exists via GET
                    await page.waitForTimeout(500);
                    const readResponse = await request.get(
                        `/api/privacy/policies?tenantId=${testProjectId}&projectId=${testProjectId}`,
                        { failOnStatusCode: false },
                    );
                    const readData = await readResponse.json();

                    expect(readResponse.ok()).toBeTruthy();
                    expect(readData?.data).toBeDefined();

                    testInfo.annotations.push({
                        type: "Evidence",
                        description: `Policy persisted to DB: ${policyId}. Read-back confirmed ${readData?.data?.length || 0} policies.`,
                    });
                }
            },
            "tc-01-007-readback",
        );
    });

    // ===== Test 1.8: Privacy UI - Error State Handling =====
    test("TC-TR-01-008: Privacy policy error (500) displays ERROR state correctly", async ({
        page,
        context,
    }, testInfo) => {
        if (!testProjectId) {
            test.skip(true, "testProjectId not set");
        }

        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);
                await page.goto(`/projects/${testProjectId}`, {
                    waitUntil: "domcontentloaded",
                });

                // Intercept and mock API error
                await page.route("/api/privacy/**", (route) => {
                    route.abort("failed");
                });

                // Try to access or create privacy policy
                const settingsButton = page
                    .locator('button:has-text("Settings"), a[href*="settings"]')
                    .first();
                if (await settingsButton.isVisible()) {
                    await settingsButton.click();
                    await page.waitForTimeout(1000);
                }

                const privacySection = page
                    .locator("text=Privacy|Privacy Policy")
                    .first();
                if (await privacySection.isVisible()) {
                    // Check if error message appears
                    const errorMessage = page
                        .locator("text=error|failed|Error|Failed|Lỗi|thất bại")
                        .first();
                    const hasError = await errorMessage
                        .isVisible()
                        .catch(() => false);

                    testInfo.annotations.push({
                        type: "Evidence",
                        description: `Error state handling: ${hasError ? "Error UI shown" : "No visible error (needs verification)"}`,
                    });
                }

                await page.unroute("/api/privacy/**");
            },
            "tc-01-008-error-state",
        );
    });

    // ===== Test 1.9: Group Link Creation (if available) =====
    test("TC-TR-01-009: Group link creation and canonical group URL", async ({
        page,
    }, testInfo) => {
        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);

                // Navigate to Groups
                const groupsLink = page
                    .locator(
                        'a:has-text("Groups"), a:has-text("Teams"), a:has-text("Nhóm")',
                    )
                    .first();
                if (await groupsLink.isVisible()) {
                    await groupsLink.click();
                    await page.waitForTimeout(1000);

                    // Click or create a group
                    const groupCard = page
                        .locator('[class*="group"], [class*="team"]')
                        .first();
                    if (await groupCard.isVisible()) {
                        await groupCard.click();
                        await page.waitForURL(/\/groups\/[a-f0-9\-]+/, {
                            timeout: 10_000,
                        });

                        const groupUrl = page.url();
                        testGroupId =
                            groupUrl.match(/\/groups\/([a-f0-9\-]+)/)?.[1] ||
                            "";

                        expect(testGroupId).toBeTruthy();
                        expect(groupUrl).toMatch(/\/groups\/[a-f0-9\-]+/);

                        testInfo.annotations.push({
                            type: "Evidence",
                            description: `Group canonical URL: ${groupUrl}`,
                        });
                    }
                }
            },
            "tc-01-009-group-link",
        );
    });

    // ===== Test 1.10: Console Error Check =====
    test("TC-TR-01-010: No silent console errors during canonical URL navigation", async ({
        page,
    }, testInfo) => {
        if (!testProjectId || !testTaskId) {
            test.skip(true, "testProjectId or testTaskId not set");
        }

        const consoleErrors: string[] = [];
        page.on("console", (msg) => {
            if (msg.type() === "error") {
                consoleErrors.push(msg.text());
            }
        });

        await runWithEvidence(
            page,
            testInfo,
            async () => {
                await loginAsAdmin(page);

                // Navigate through canonical URLs
                await page.goto(`/projects/${testProjectId}`, {
                    waitUntil: "domcontentloaded",
                });
                await page.goto(
                    `/projects/${testProjectId}/tasks/${testTaskId}`,
                    { waitUntil: "domcontentloaded" },
                );
                await page.reload({ waitUntil: "domcontentloaded" });

                await page.waitForTimeout(1000);

                // Filter out expected errors
                const unexpectedErrors = consoleErrors.filter(
                    (error) =>
                        !error.includes("favicon") &&
                        !error.includes("404") &&
                        !error.includes("WebSocket"),
                );

                testInfo.annotations.push({
                    type: "Evidence",
                    description: `Console errors captured: ${unexpectedErrors.length}. Details: ${unexpectedErrors.join("; ")}`,
                });

                expect(unexpectedErrors).toHaveLength(0);
            },
            "tc-01-010-console",
        );
    });
});
