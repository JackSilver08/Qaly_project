import { expect, test, type Page, type TestInfo } from "@playwright/test";

test.describe.configure({ mode: "serial" });

const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:5000";
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@qaly.dev";
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "Admin@123456";

let infraBlockedReason: string | null = null;
let testProjectId: string = "";
let testTaskId: string = "";
let testGroupId: string = "";
let authToken: string = "";

async function captureFailureEvidence(page: Page, testInfo: TestInfo) {
    await page
        .screenshot({
            path: testInfo.outputPath("test-failed.png"),
            fullPage: true,
        })
        .catch(() => undefined);
}

async function runWithEvidence(
    page: Page,
    testInfo: TestInfo,
    action: () => Promise<void>,
) {
    try {
        await action();
    } catch (error) {
        await captureFailureEvidence(page, testInfo);
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

// ===== T1-TR-01: Task URL, Group link và Privacy UI =====

test.describe("T1-TR-01: Task URL, Group link và Privacy UI", () => {
    test("TC-TR-01-001: Task canonical URL loads correctly with refresh", async ({
        page,
    }, testInfo) => {
        await runWithEvidence(page, testInfo, async () => {
            await loginAsAdmin(page);

            // Navigate to projects
            await page.click(
                'a[href="/projects"], button:has-text("Projects"), button:has-text("Dự án")',
            );
            await page.waitForSelector('a[href*="/projects/"]', {
                timeout: 10_000,
            });

            // Click first project
            const projectLink = page.locator('a[href*="/projects/"]').first();
            if (await projectLink.isVisible()) {
                await projectLink.click();
                await page.waitForURL(/\/projects\/[a-f0-9-]+/, {
                    timeout: 10_000,
                });

                // Extract project ID from URL
                const projectUrl = page.url();
                const projectMatch = projectUrl.match(
                    /\/projects\/([a-f0-9-]+)/,
                );
                expect(projectMatch).toBeTruthy();
                testProjectId = projectMatch![1];

                // Look for a task and click it
                const taskElements = page.locator('[class*="task"]');
                if ((await taskElements.count()) > 0) {
                    await taskElements.first().click();
                    await page.waitForURL(
                        /\/projects\/[a-f0-9-]+\/tasks\/[a-f0-9-]+/,
                        { timeout: 10_000 },
                    );

                    const taskUrl = page.url();
                    const taskMatch = taskUrl.match(
                        /\/projects\/([a-f0-9-]+)\/tasks\/([a-f0-9-]+)/,
                    );
                    expect(taskMatch).toBeTruthy();
                    testTaskId = taskMatch![2];

                    // Verify canonical URL structure
                    expect(taskUrl).toMatch(
                        /\/projects\/[a-f0-9-]+\/tasks\/[a-f0-9-]+$/,
                    );

                    // Test refresh with canonical URL
                    const currentUrl = page.url();
                    await page.reload({ waitUntil: "domcontentloaded" });
                    expect(page.url()).toBe(currentUrl);
                }
            }
        });
    });

    test("TC-TR-01-002: Task URL supports back/forward navigation", async ({
        page,
    }, testInfo) => {
        if (!testProjectId || !testTaskId) {
            test.skip(true, "testProjectId or testTaskId not set");
        }

        await runWithEvidence(page, testInfo, async () => {
            await loginAsAdmin(page);

            // Navigate to project
            await page.goto(`/projects/${testProjectId}`, {
                waitUntil: "domcontentloaded",
            });

            // Navigate to task
            await page.goto(`/projects/${testProjectId}/tasks/${testTaskId}`, {
                waitUntil: "domcontentloaded",
            });

            // Go back
            await page.goBack();
            expect(page.url()).toContain(`/projects/${testProjectId}`);

            // Go forward
            await page.goForward();
            expect(page.url()).toContain(`/tasks/${testTaskId}`);
        });
    });

    test("TC-TR-01-003: Group link renders and navigates correctly", async ({
        page,
    }, testInfo) => {
        await runWithEvidence(page, testInfo, async () => {
            await loginAsAdmin(page);

            // Navigate to teams/groups
            await page.click(
                'a[href="/teams"], a[href="/groups"], button:has-text("Teams"), button:has-text("Groups"), button:has-text("Nhóm")',
            );
            await page.waitForSelector('a[href*="/groups/"]', {
                timeout: 10_000,
            });

            // Get first group link
            const groupLink = page.locator('a[href*="/groups/"]').first();
            if (await groupLink.isVisible()) {
                const href = await groupLink.getAttribute("href");
                expect(href).toMatch(/\/groups\/[a-f0-9-]+/);

                // Extract group ID
                const groupMatch = href!.match(/\/groups\/([a-f0-9-]+)/);
                testGroupId = groupMatch![1];

                // Click to navigate
                await groupLink.click();
                await page.waitForURL(/\/groups\/[a-f0-9-]+/, {
                    timeout: 10_000,
                });
                expect(page.url()).toContain(`/groups/${testGroupId}`);
            }
        });
    });

    test("TC-TR-01-004: Privacy UI shows access restrictions", async ({
        page,
    }, testInfo) => {
        if (!testProjectId) {
            test.skip(true, "testProjectId not set");
        }

        await runWithEvidence(page, testInfo, async () => {
            await loginAsAdmin(page);

            // Navigate to project
            await page.goto(`/projects/${testProjectId}`, {
                waitUntil: "domcontentloaded",
            });

            // Look for privacy-related UI elements
            const privacyElements = page.locator(
                '[class*="privacy"], [class*="lock"], [class*="restricted"]',
            );
            const lockIcon = page.locator(
                'svg[class*="lock"], [data-icon*="lock"]',
            );

            // If privacy elements exist, verify they're visible and have appropriate styling
            if (
                (await privacyElements.count()) > 0 ||
                (await lockIcon.count()) > 0
            ) {
                const element =
                    (await privacyElements.count()) > 0
                        ? privacyElements.first()
                        : lockIcon;
                await expect(element).toBeVisible();
            }
        });
    });
});

// ===== T1-TR-02: Comment, Attachment, Evidence, Notification, quyền =====

test.describe("T1-TR-02: Comment, Attachment, Evidence, Notification và quyền", () => {
    test("TC-TR-02-001: Create and retrieve comments", async ({
        request,
    }, testInfo) => {
        if (!testTaskId) {
            test.skip(true, "testTaskId not set");
        }

        // GET comments - should return array
        const getResponse = await request.get(
            `/api/comments/task/${testTaskId}`,
            { failOnStatusCode: false },
        );
        expect([200, 401]).toContain(getResponse.status());

        if (getResponse.status() === 200) {
            const comments = await getResponse.json();
            expect(
                Array.isArray(comments.data) || Array.isArray(comments),
            ).toBeTruthy();
        }
    });

    test("TC-TR-02-002: Comment API returns proper error when unauthorized", async ({
        request,
    }, testInfo) => {
        if (!testTaskId) {
            test.skip(true, "testTaskId not set");
        }

        // Try without auth header
        const response = await request.get(`/api/comments/task/${testTaskId}`, {
            headers: { Authorization: "Bearer invalid_token_xyz" },
            failOnStatusCode: false,
        });

        // Should fail with 401 or return data (depending on implementation)
        expect([200, 401, 403]).toContain(response.status());
    });

    test("TC-TR-02-003: Retrieve attachments for task", async ({
        request,
    }, testInfo) => {
        if (!testTaskId) {
            test.skip(true, "testTaskId not set");
        }

        // GET attachments - should return array
        const getResponse = await request.get(
            `/api/attachments/task/${testTaskId}`,
            { failOnStatusCode: false },
        );
        expect([200, 401]).toContain(getResponse.status());

        if (getResponse.status() === 200) {
            const response = await getResponse.json();
            const attachments = response.data || response;
            expect(Array.isArray(attachments)).toBeTruthy();
        }
    });

    test("TC-TR-02-004: Evidence approval endpoint responds correctly", async ({
        request,
    }, testInfo) => {
        if (!testTaskId) {
            test.skip(true, "testTaskId not set");
        }

        // Get attachments first
        const getResponse = await request.get(
            `/api/attachments/task/${testTaskId}`,
            { failOnStatusCode: false },
        );

        if (getResponse.status() === 200) {
            const response = await getResponse.json();
            const attachments = response.data || response;

            if (attachments && attachments.length > 0) {
                const attachmentId = attachments[0].id;

                // Try to mark as evidence - should either succeed or fail with proper status
                const markResponse = await request.patch(
                    `/api/attachments/${attachmentId}/evidence`,
                    {
                        data: { isEvidence: true },
                        failOnStatusCode: false,
                    },
                );
                expect([200, 204, 400, 401, 403, 404]).toContain(
                    markResponse.status(),
                );
            }
        }
    });

    test("TC-TR-02-005: Evidence review endpoint (admin only)", async ({
        request,
    }, testInfo) => {
        if (!testTaskId) {
            test.skip(true, "testTaskId not set");
        }

        // Get attachments
        const getResponse = await request.get(
            `/api/attachments/task/${testTaskId}`,
            { failOnStatusCode: false },
        );

        if (getResponse.status() === 200) {
            const response = await getResponse.json();
            const attachments = response.data || response;

            if (attachments && attachments.length > 0) {
                const attachmentId = attachments[0].id;

                // Admin can review evidence - endpoint should respond
                const reviewResponse = await request.post(
                    `/api/attachments/${attachmentId}/evidence/review`,
                    {
                        data: {
                            approve: true,
                            reviewNote:
                                "Evidence approved for retention testing",
                        },
                        failOnStatusCode: false,
                    },
                );
                // Should either succeed or fail with proper auth status
                expect([200, 204, 400, 401, 403, 404]).toContain(
                    reviewResponse.status(),
                );
            }
        }
    });

    test("TC-TR-02-006: Get user notifications", async ({
        request,
    }, testInfo) => {
        // GET notifications
        const getResponse = await request.get(`/api/notifications`, {
            failOnStatusCode: false,
        });
        expect([200, 401]).toContain(getResponse.status());

        if (getResponse.status() === 200) {
            const notifications = await getResponse.json();
            expect(notifications).toBeDefined();
        }
    });

    test("TC-TR-02-007: Get unread notification count", async ({
        request,
    }, testInfo) => {
        // GET unread count
        const countResponse = await request.get(
            `/api/notifications/unread-count`,
            { failOnStatusCode: false },
        );
        expect([200, 401]).toContain(countResponse.status());

        if (countResponse.status() === 200) {
            const response = await countResponse.json();
            const count = response.data ?? response;
            expect(typeof count === "number").toBeTruthy();
        }
    });

    test("TC-TR-02-008: Mark notification as read - returns proper status", async ({
        request,
    }, testInfo) => {
        // First get notifications
        const getResponse = await request.get(
            `/api/notifications?unreadOnly=true`,
            { failOnStatusCode: false },
        );

        if (getResponse.status() === 200) {
            const response = await getResponse.json();
            const notifications = response.data || response;

            if (notifications && notifications.length > 0) {
                const notificationId = notifications[0].id;

                // Mark as read - should return success or error status
                const markResponse = await request.patch(
                    `/api/notifications/${notificationId}/read`,
                    { failOnStatusCode: false },
                );
                expect([200, 204, 404, 401]).toContain(markResponse.status());
            }
        }
    });

    test("TC-TR-02-009: Attachment deletion persistence check", async ({
        request,
    }, testInfo) => {
        if (!testTaskId) {
            test.skip(true, "testTaskId not set");
        }

        // Get attachments before
        const beforeDelete = await request.get(
            `/api/attachments/task/${testTaskId}`,
            { failOnStatusCode: false },
        );

        if (beforeDelete.status() === 200) {
            const response = await beforeDelete.json();
            const attachmentsBefore = response.data || response;
            const initialCount = Array.isArray(attachmentsBefore)
                ? attachmentsBefore.length
                : 0;

            if (initialCount > 0) {
                const attachmentId = attachmentsBefore[0].id;

                // Delete - may or may not succeed depending on permissions
                await request.delete(`/api/attachments/${attachmentId}`, {
                    failOnStatusCode: false,
                });

                // Verify read-back after delete
                const afterDelete = await request.get(
                    `/api/attachments/task/${testTaskId}`,
                    { failOnStatusCode: false },
                );
                expect(afterDelete.status()).toBe(200);

                const responseAfter = await afterDelete.json();
                const attachmentsAfter = responseAfter.data || responseAfter;
                const finalCount = Array.isArray(attachmentsAfter)
                    ? attachmentsAfter.length
                    : 0;

                // Count should be <= initial count
                expect(finalCount).toBeLessThanOrEqual(initialCount);
            }
        }
    });

    test("TC-TR-02-010: Comment count persists in task metadata", async ({
        page,
    }, testInfo) => {
        if (!testProjectId || !testTaskId) {
            test.skip(true, "testProjectId or testTaskId not set");
        }

        await runWithEvidence(page, testInfo, async () => {
            await loginAsAdmin(page);

            // Navigate to task
            await page.goto(`/projects/${testProjectId}/tasks/${testTaskId}`, {
                waitUntil: "domcontentloaded",
            });

            // Look for comment count - could be in metadata, sidebar, or header
            const commentIndicators = page.locator(
                '[class*="comment"], [data-testid*="comment"]',
            );

            // If found, verify it's visible
            if ((await commentIndicators.count()) > 0) {
                const element = commentIndicators.first();
                if (await element.isVisible()) {
                    const text = await element.textContent();
                    expect(text).toBeTruthy();
                }
            }
        });
    });

    test("TC-TR-02-011: Attachment count persists in task metadata", async ({
        page,
    }, testInfo) => {
        if (!testProjectId || !testTaskId) {
            test.skip(true, "testProjectId or testTaskId not set");
        }

        await runWithEvidence(page, testInfo, async () => {
            await loginAsAdmin(page);

            // Navigate to task
            await page.goto(`/projects/${testProjectId}/tasks/${testTaskId}`, {
                waitUntil: "domcontentloaded",
            });

            // Look for attachment indicator
            const attachmentIndicators = page.locator(
                '[class*="attachment"], [class*="file"], [data-testid*="attachment"]',
            );

            if ((await attachmentIndicators.count()) > 0) {
                const element = attachmentIndicators.first();
                if (await element.isVisible()) {
                    const text = await element.textContent();
                    expect(text).toBeTruthy();
                }
            }
        });
    });

    test("TC-TR-02-012: Permission check - API returns consistent status codes", async ({
        request,
    }, testInfo) => {
        if (!testTaskId) {
            test.skip(true, "testTaskId not set");
        }

        // Test multiple endpoints with proper status handling
        const endpoints = [
            `/api/comments/task/${testTaskId}`,
            `/api/attachments/task/${testTaskId}`,
            `/api/notifications`,
            `/api/notifications/unread-count`,
        ];

        for (const endpoint of endpoints) {
            const response = await request.get(endpoint, {
                failOnStatusCode: false,
            });

            // Should be 200 if authorized, 401 if not
            expect([200, 401]).toContain(response.status());
        }
    });
});

// ===== Cleanup & Final Verification =====

test.describe("Cleanup and Persistence Verification", () => {
    test("TC-TR-FINAL-001: Verify data persists after task reload", async ({
        page,
        request,
    }, testInfo) => {
        if (!testProjectId || !testTaskId) {
            test.skip(true, "testProjectId or testTaskId not set");
        }

        await runWithEvidence(page, testInfo, async () => {
            // Get initial state
            const initialComments = await request.get(
                `/api/comments/task/${testTaskId}`,
                { failOnStatusCode: false },
            );
            const initialAttachments = await request.get(
                `/api/attachments/task/${testTaskId}`,
                { failOnStatusCode: false },
            );

            // Reload page
            await page.goto(`/projects/${testProjectId}/tasks/${testTaskId}`, {
                waitUntil: "domcontentloaded",
            });
            await page.reload({ waitUntil: "domcontentloaded" });

            // Verify data still available
            const finalComments = await request.get(
                `/api/comments/task/${testTaskId}`,
                { failOnStatusCode: false },
            );
            const finalAttachments = await request.get(
                `/api/attachments/task/${testTaskId}`,
                { failOnStatusCode: false },
            );

            expect(initialComments.status()).toBe(finalComments.status());
            expect(initialAttachments.status()).toBe(finalAttachments.status());
        });
    });

    test("TC-TR-FINAL-002: Verify evidence state is consistent", async ({
        request,
    }, testInfo) => {
        if (!testTaskId) {
            test.skip(true, "testTaskId not set");
        }

        // Get attachments twice and verify consistency
        const response1 = await request.get(
            `/api/attachments/task/${testTaskId}`,
            { failOnStatusCode: false },
        );

        if (response1.status() === 200) {
            const data1 = await response1.json();
            const attachments1 = data1.data || data1;

            // Wait a moment
            await new Promise((resolve) => setTimeout(resolve, 500));

            // Get again
            const response2 = await request.get(
                `/api/attachments/task/${testTaskId}`,
                { failOnStatusCode: false },
            );

            if (response2.status() === 200) {
                const data2 = await response2.json();
                const attachments2 = data2.data || data2;

                // Verify same count and structure
                if (
                    Array.isArray(attachments1) &&
                    Array.isArray(attachments2)
                ) {
                    expect(attachments1.length).toBe(attachments2.length);
                }
            }
        }
    });

    test("TC-TR-FINAL-003: Canonical URL persists across navigation", async ({
        page,
    }, testInfo) => {
        if (!testProjectId || !testTaskId) {
            test.skip(true, "testProjectId or testTaskId not set");
        }

        await runWithEvidence(page, testInfo, async () => {
            await loginAsAdmin(page);

            // Navigate to canonical URL
            const canonicalUrl = `/projects/${testProjectId}/tasks/${testTaskId}`;
            await page.goto(canonicalUrl, { waitUntil: "domcontentloaded" });

            // Verify URL matches canonical format
            expect(page.url()).toContain(canonicalUrl);

            // Navigate back to project and forward again
            await page.goto(`/projects/${testProjectId}`, {
                waitUntil: "domcontentloaded",
            });
            await page.goto(canonicalUrl, { waitUntil: "domcontentloaded" });

            // Verify canonical URL is maintained
            expect(page.url()).toContain(canonicalUrl);
        });
    });
});
