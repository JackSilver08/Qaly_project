import {
    expect,
    test,
    type Browser,
    type BrowserContext,
    type Page,
} from "@playwright/test";
import { browserApiRequest } from "./support/browser-api";
import {
    adminEmail,
    adminPassword,
    memberEmail,
    memberPassword,
} from "./support/credentials";

test.describe.configure({ mode: "serial" });

const secondaryEmail = process.env.E2E_MEMBER_EMAIL ? memberEmail : adminEmail;
const secondaryPassword = process.env.E2E_MEMBER_PASSWORD ? memberPassword : adminPassword;
const wikiFixturePath = "tests/e2e/fixtures/wiki-smoke.md";

type ApiResult<T> = {
    isSuccess: boolean;
    data: T | null;
    error: string | null;
};

type GroupDto = {
    id: string;
    name: string;
};

type ProjectDto = {
    id: string;
    name: string;
};

type UserDto = {
    id: string;
    email: string;
};

type WebhookDto = {
    id: string;
    projectId: string;
    payloadUrl: string;
    events: string[];
    hasSecret: boolean;
    isActive: boolean;
    createdAt: string;
};

type GanttTaskDto = {
    id: string;
    title: string;
    status: string;
    startDate: string | null;
    endDate: string | null;
    progress: number;
    isCriticalPath: boolean;
    dependencies: string[];
};

type ApiResponseLike = {
    ok(): boolean;
    status(): number;
    url(): string;
    json?(): Promise<unknown>;
    text(): Promise<string>;
};

function uniqueName(prefix: string) {
    const suffix = Math.random().toString(36).slice(2, 8);
    return `${prefix} ${Date.now()} ${suffix}`;
}

async function login(
    page: Page,
    email = adminEmail,
    password = adminPassword,
) {
    await page.goto("/Account/Login", { waitUntil: "domcontentloaded" });
    await expect(page.locator("#loginForm")).toBeVisible();
    await page.locator('input[name="Email"]').fill(email);
    await page.locator('input[name="Password"]').fill(password);
    await page.getByRole("button", { name: /Đăng nhập|Login/i }).click();
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

async function responseBody(response: ApiResponseLike) {
    try {
        return await response.json?.();
    } catch {
        const text = await response.text();
        if (!text.trim()) return null;

        try {
            return JSON.parse(text) as unknown;
        } catch {
            return text;
        }
    }
}

function responseErrorMessage(response: ApiResponseLike, body: unknown) {
    let bodyMessage = "";
    if (body && typeof body === "object" && "error" in body) {
        bodyMessage = `: ${String(body.error)}`;
    } else if (typeof body === "string" && body.trim()) {
        bodyMessage = `: ${body.slice(0, 300)}`;
    }

    return `API ${response.url()} trả về ${response.status()}${bodyMessage}`;
}

async function apiResult<T>(response: ApiResponseLike): Promise<T> {
    const body = await responseBody(response);

    expect(
        response.ok(),
        responseErrorMessage(response, body),
    ).toBeTruthy();

    if (body && typeof body === "object" && "isSuccess" in body) {
        const result = body as ApiResult<T>;
        expect(result.isSuccess, result.error ?? "API result failed").toBeTruthy();
        expect(result.data, "API result must include data").not.toBeNull();
        return result.data as T;
    }

    return body as T;
}

async function apiCommand(response: ApiResponseLike) {
    const body = await responseBody(response);

    expect(
        response.ok(),
        responseErrorMessage(response, body),
    ).toBeTruthy();

    if (body && typeof body === "object" && "isSuccess" in body) {
        const result = body as ApiResult<unknown>;
        expect(result.isSuccess, result.error ?? "API command failed").toBeTruthy();
    }
}

async function createGroupViaApi(page: Page, name: string) {
    return apiResult<GroupDto>(
        await browserApiRequest(page, "POST", "/api/groups", {
            data: {
                name,
                color: "#2563eb",
            },
        }),
    );
}

async function createProjectViaApi(page: Page, name: string) {
    return apiResult<ProjectDto>(
        await browserApiRequest(page, "POST", "/api/projects", {
            data: {
                name,
                code: null,
                description: "DH-02 E2E smoke project",
                logoUrl: null,
                startDate: null,
                endDate: null,
                organizationId: null,
                sourceGroupId: null,
            },
        }),
    );
}

async function createTaskViaApi(page: Page, projectId: string, title: string) {
    return apiResult<{ id: string; title: string }>(
        await browserApiRequest(page, "POST", "/api/tasks", {
            data: {
                title,
                description: "Real Gantt contract E2E task",
                priority: "High",
                dueDate: null,
                estimatedHours: null,
                projectId,
                assigneeId: null,
                isPrivate: false,
                isPinned: false,
                contributesToProgress: true,
                assigneeIds: [],
                labelIds: [],
            },
        }),
    );
}

async function cleanupGroup(page: Page, groupId?: string) {
    if (!groupId) return;
    await browserApiRequest(page, "DELETE", `/api/groups/${groupId}`).catch(() => undefined);
}

async function cleanupProject(page: Page, projectId?: string) {
    if (!projectId) return;
    await browserApiRequest(page, "DELETE", `/api/projects/${projectId}`).catch(() => undefined);
}

async function addSecondaryUserToGroupIfNeeded(page: Page, groupId: string) {
    if (secondaryEmail.toLowerCase() === adminEmail.toLowerCase()) return;

    const users = await apiResult<UserDto[]>(await browserApiRequest(page, "GET", "/api/users"));
    const secondaryUser = users.find(
        (user) => user.email.toLowerCase() === secondaryEmail.toLowerCase(),
    );

    expect(
        secondaryUser,
        `Không tìm thấy E2E_MEMBER_EMAIL=${secondaryEmail} trong /api/users`,
    ).toBeTruthy();

    await apiCommand(
        await browserApiRequest(page, "POST", `/api/groups/${groupId}/members`, {
            data: {
                userId: secondaryUser!.id,
                role: "Member",
            },
        }),
    );
}

async function createGroupViaUi(page: Page, groupName: string) {
    await page.goto("/teams");
    await expect(page.locator(".team-chat-page")).toBeVisible();

    await page.getByRole("button", { name: /Tạo nhóm|Create group/i }).first().click();
    const modal = page.locator(".group-modal");
    await expect(modal).toBeVisible();
    await modal.locator('input[placeholder="Tên nhóm"]').fill(groupName);
    await modal.locator('button[type="submit"]').click();

    const createdGroup = page
        .locator(".team-chat-group")
        .filter({ hasText: groupName })
        .first();
    await expect(createdGroup).toBeVisible();
    await createdGroup.click();

    const groupId = await createdGroup.getAttribute("data-group-id");
    expect(groupId, "Nhóm mới phải có data-group-id").toBeTruthy();
    await expect(page.locator(".team-chat-window__header")).toContainText(groupName);

    return { id: groupId!, name: groupName };
}

async function openGroupPage(page: Page, group: GroupDto) {
    await page.goto(`/groups/${group.id}`);
    await expect(page.locator(".team-chat-page")).toBeVisible();
    await expect(
        page.locator(".team-chat-group").filter({ hasText: group.name }).first(),
    ).toBeVisible();
    await expect(page.locator(".team-chat-window__header")).toContainText(group.name);
}

async function newSecondaryPage(browser: Browser) {
    const context = await browser.newContext();
    const page = await context.newPage();
    await login(page, secondaryEmail, secondaryPassword);
    return { context, page };
}

test("should login successfully", async ({ page }) => {
    await login(page);

    await page.locator(".shell-user-menu").click();
    await expect(page.getByRole("menuitem", { name: /Đăng xuất|Logout/i })).toBeVisible();
    await page.getByRole("menuitem", { name: /Đăng xuất|Logout/i }).click();
    await page.waitForURL((url) => url.pathname.startsWith("/Account/Login"), {
        timeout: 20_000,
    });
    await expect(page.locator("#loginForm")).toBeVisible();

    await page.goBack();
    await expect(page.locator("#loginForm")).toBeVisible();
    await expect(page).toHaveURL(/\/Account\/Login/i);
});

test("should reject an incorrect password with a safe Vietnamese error", async ({ page }) => {
    await page.goto("/Account/Login", { waitUntil: "domcontentloaded" });
    await page.locator('input[name="Email"]').fill(adminEmail);
    await page.locator('input[name="Password"]').fill("definitely-not-the-password");
    await page.getByRole("button", { name: /Đăng nhập|Login/i }).click();

    await expect(page.locator(".auth-error")).toHaveText("Email hoặc mật khẩu không đúng.");
    await expect(page.locator("#loginForm")).toBeVisible();
    await expect(page).toHaveURL(/\/Account\/Login/i);
});

test("should show an honest empty state when dashboard data fails", async ({ page }) => {
    await page.route("**/api/dashboard/overview", (route) => route.fulfill({
        status: 503,
        contentType: "application/json",
        body: JSON.stringify({ error: "dashboard unavailable" }),
    }));

    await login(page);

    await expect(page.getByRole("alert")).toContainText("không phải dữ liệu mẫu");
    await expect(page.getByText("Qaly MVP", { exact: true })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Thử lại" })).toBeVisible();
});

test("should report webhook secret presence without exposing the secret", async ({ page }) => {
    let projectId: string | undefined;
    let webhookId: string | undefined;

    try {
        await login(page);
        const project = await createProjectViaApi(page, uniqueName("Webhook contract project"));
        projectId = project.id;

        const webhook = await apiResult<WebhookDto>(await browserApiRequest(
            page,
            "POST",
            `/api/projects/${project.id}/webhooks`,
            {
                data: {
                    payloadUrl: "https://8.8.8.8/qaly-webhook",
                    secret: "e2e-webhook-secret",
                    events: ["task.created"],
                },
            },
        ));
        webhookId = webhook.id;

        expect(webhook.hasSecret).toBe(true);
        expect(webhook).not.toHaveProperty("secret");

        const webhooks = await apiResult<WebhookDto[]>(await browserApiRequest(
            page,
            "GET",
            `/api/projects/${project.id}/webhooks`,
        ));
        expect(webhooks).toContainEqual(expect.objectContaining({
            id: webhook.id,
            hasSecret: true,
        }));
        expect(webhooks[0]).not.toHaveProperty("secret");
    } finally {
        if (projectId && webhookId) {
            await browserApiRequest(page, "DELETE", `/api/projects/${projectId}/webhooks/${webhookId}`)
                .catch(() => undefined);
        }
        await cleanupProject(page, projectId);
    }
});

test("should render database-backed Gantt dates on the project timeline", async ({ page }) => {
    let projectId: string | undefined;
    let taskId: string | undefined;
    const taskTitle = uniqueName("Real Gantt task");

    try {
        await login(page);
        const project = await createProjectViaApi(page, uniqueName("Real Gantt project"));
        projectId = project.id;
        const task = await createTaskViaApi(page, project.id, taskTitle);
        taskId = task.id;

        await apiCommand(await browserApiRequest(page, "PATCH", `/api/tasks/${task.id}/dates`, {
            data: {
                startDate: "2026-08-10T00:00:00Z",
                endDate: "2026-08-14T00:00:00Z",
            },
        }));

        const gantt = await apiResult<GanttTaskDto[]>(await browserApiRequest(
            page,
            "GET",
            `/api/tasks/project/${project.id}/gantt`,
        ));
        expect(gantt).toContainEqual(expect.objectContaining({
            id: task.id,
            title: taskTitle,
            startDate: "2026-08-10T00:00:00+00:00",
            endDate: "2026-08-14T00:00:00+00:00",
        }));

        await page.goto(`/projects/${project.id}`);
        await page.getByRole("button", { name: "Lộ Trình Dự Án" }).click();
        await page.getByRole("button", { name: /Dòng thời gian|Timeline View/i }).click();
        await expect(page.locator(".timeline-task-label")).toContainText(taskTitle);
        await expect(page.locator(".timeline-bar")).toBeVisible();
    } finally {
        if (taskId) {
            await browserApiRequest(page, "DELETE", `/api/tasks/${taskId}`).catch(() => undefined);
        }
        await cleanupProject(page, projectId);
    }
});

test("should create a group and open group page", async ({ page }) => {
    let groupId: string | undefined;

    try {
        await login(page);
        const group = await createGroupViaUi(page, uniqueName("DH02 E2E Group"));
        groupId = group.id;

        await expect(page).toHaveURL(new RegExp(`/groups/${group.id}$`));
        await expect(page.locator(".group-detail-header")).toContainText(group.name);
    } finally {
        await cleanupGroup(page, groupId);
    }
});

test("should send a chat message and receive it in a second context", async ({
    page,
    browser,
}) => {
    let group: GroupDto | undefined;
    const secondary = { context: undefined as BrowserContext | undefined };

    try {
        await login(page);
        group = await createGroupViaApi(page, uniqueName("DH02 E2E Realtime"));
        await addSecondaryUserToGroupIfNeeded(page, group.id);
        await openGroupPage(page, group);

        const secondarySession = await newSecondaryPage(browser);
        secondary.context = secondarySession.context;
        await openGroupPage(secondarySession.page, group);

        const message = uniqueName("DH02 realtime message");
        await page
            .locator('.team-chat-composer textarea, .team-chat-composer input[type="text"]')
            .fill(message);
        await page.locator('.team-chat-composer button[type="submit"]').click();

        await expect(page.locator(".team-chat-body")).toContainText(message);
        await expect(secondarySession.page.locator(".team-chat-body")).toContainText(message, {
            timeout: 15_000,
        });
    } finally {
        await secondary.context?.close();
        await cleanupGroup(page, group?.id);
    }
});

test("should vote in a group poll", async ({ page }) => {
    let groupId: string | undefined;

    try {
        await login(page);
        const group = await createGroupViaUi(page, uniqueName("DH02 E2E Poll"));
        groupId = group.id;

        await page.getByRole("button", { name: /Bình chọn/i }).click();
        const pollComposer = page.locator(".group-poll-composer");
        await expect(pollComposer).toBeVisible();

        const question = uniqueName("DH02 poll question");
        await pollComposer
            .locator('input[placeholder="Hỏi mọi người một câu..."]')
            .fill(question);
        await pollComposer.locator(".group-poll-options input").nth(0).fill("Đỏ");
        await pollComposer.locator(".group-poll-options input").nth(1).fill("Xanh");
        await pollComposer.getByRole("button", { name: /Gửi bình chọn/i }).click();

        const pollCard = page.locator(".poll-card").filter({ hasText: question }).first();
        await expect(pollCard).toBeVisible();

        const redOption = pollCard.locator(".poll-option").filter({ hasText: "Đỏ" }).first();
        await expect(redOption).toBeEnabled();
        await redOption.click();

        await expect(redOption).toContainText(/1 chọn|1 votes/i);
        await expect(pollCard).toContainText(/1 người tham gia|1 voter/i);
    } finally {
        await cleanupGroup(page, groupId);
    }
});

test("should open analytics page", async ({ page }) => {
    await login(page);

    await page.locator('a[href="/analytics"]').click();
    await expect(page).toHaveURL(/\/analytics$/);
    await expect(page.locator(".analytics-chat-portal")).toBeVisible();
    await expect(page.getByRole("textbox", { name: /Nhập yêu cầu (tiếp theo )?cho Trợ lý AI/i })).toBeVisible();
});

test("should render meeting page in two authenticated contexts", async ({
    page,
    browser,
}) => {
    let group: GroupDto | undefined;
    let meetingId: string | undefined;
    const secondary = { context: undefined as BrowserContext | undefined };

    try {
        await login(page);
        group = await createGroupViaApi(page, uniqueName("DH02 E2E Meeting"));
        await addSecondaryUserToGroupIfNeeded(page, group.id);

        await page.goto(`/groups/${group.id}/meeting`);
        await expect(page.getByText("QALY MEET", { exact: true })).toBeVisible();
        await expect(page.locator(".gm-prejoin__heading")).toContainText(/Sẵn sàng bắt đầu/i);

        const startResponsePromise = page.waitForResponse(
            (response) =>
                response.url().includes(`/api/groups/${group!.id}/meetings/start`) &&
                response.request().method() === "POST",
        );
        await page.getByRole("button", { name: /Bắt đầu cuộc họp|Start meeting/i }).first().click();
        const meeting = await apiResult<Record<string, string>>(await startResponsePromise);
        meetingId = meeting.id ?? meeting.Id;

        expect(meetingId, "Meeting start response phải có id").toBeTruthy();
        await expect(page.locator(".gm-status")).toHaveClass(/gm-status--active/);
        await expect(page.locator(".gm-status")).toContainText(/LiveKit|Cần kiểm tra kết nối/i);
        await expect(page.locator(".gm-tile--local")).toContainText("Bạn");

        const secondarySession = await newSecondaryPage(browser);
        secondary.context = secondarySession.context;
        await secondarySession.page.goto(`/groups/${group.id}/meeting?meetingId=${meetingId}`);
        await expect(secondarySession.page.getByText("QALY MEET", { exact: true })).toBeVisible();
        await expect(secondarySession.page.locator(".gm-status")).toHaveClass(/gm-status--active/);
        await expect(secondarySession.page.locator(".gm-status")).toContainText(
            /LiveKit|Cần kiểm tra kết nối/i,
        );
        await expect(secondarySession.page.locator(".gm-tile--local")).toContainText("Bạn");
    } finally {
        if (group?.id && meetingId) {
            await browserApiRequest(page, "POST", `/api/groups/${group.id}/meetings/${meetingId}/end`)
                .catch(() => undefined);
        }
        await secondary.context?.close();
        await cleanupGroup(page, group?.id);
    }
});

test("should preview and import a Wiki document", async ({ page }) => {
    let project: ProjectDto | undefined;

    try {
        await login(page);
        project = await createProjectViaApi(page, uniqueName("DH02 E2E Import"));

        await page.goto(`/projects/${project.id}`);
        await expect(page.getByRole("heading", { name: project.name })).toBeVisible();
        await page
            .locator(".project-tabs")
            .getByRole("button", { name: "Nhiệm vụ" })
            .click();
        await expect(page.locator("#tasks")).toBeVisible();
        await page.getByRole("button", { name: /Nhập file|Import/i }).click();

        const modal = page.locator(".import-modal");
        await expect(modal).toBeVisible();
        await modal.locator('input[type="file"]').setInputFiles(wikiFixturePath);
        await expect(modal).toContainText("wiki-smoke.md");

        await modal.getByRole("button", { name: /Tiếp tục/i }).click();
        await expect(
            modal.getByRole("heading", { name: /Xem trước trang Wiki sẽ nhập/i }),
        ).toBeVisible();
        await expect(modal.locator(".document-preview .import-input")).toHaveValue(
            /DH02 Wiki Smoke/i,
        );

        await modal.getByRole("button", { name: /Tiếp tục/i }).click();
        await expect(
            modal.getByRole("heading", { name: /Xác nhận nhập tài liệu/i }),
        ).toBeVisible();
        await expect(modal).toContainText("DH02 Wiki Smoke");

        await modal.getByRole("button", { name: /Tạo trang Wiki/i }).click();
        await expect(
            modal.getByRole("heading", { name: /Nhập dữ liệu hoàn tất/i }),
        ).toBeVisible();
        await expect(modal).toContainText("DH02 Wiki Smoke");
    } finally {
        await cleanupProject(page, project?.id);
    }
});
