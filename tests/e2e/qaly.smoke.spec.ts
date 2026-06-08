import {
    expect,
    test,
    type Browser,
    type BrowserContext,
    type Page,
} from "@playwright/test";

test.describe.configure({ mode: "serial" });

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@qaly.dev";
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "Qaly@E2E2026!";
const secondaryEmail = process.env.E2E_MEMBER_EMAIL ?? adminEmail;
const secondaryPassword = process.env.E2E_MEMBER_PASSWORD ?? adminPassword;
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
        expect(body.data, "API result must include data").not.toBeNull();
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
        await page.request.post("/api/groups", {
            data: {
                name,
                color: "#2563eb",
            },
        }),
    );
}

async function createProjectViaApi(page: Page, name: string) {
    return apiResult<ProjectDto>(
        await page.request.post("/api/projects", {
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

async function cleanupGroup(page: Page, groupId?: string) {
    if (!groupId) return;
    await page.request.delete(`/api/groups/${groupId}`).catch(() => undefined);
}

async function cleanupProject(page: Page, projectId?: string) {
    if (!projectId) return;
    await page.request.delete(`/api/projects/${projectId}`).catch(() => undefined);
}

async function addSecondaryUserToGroupIfNeeded(page: Page, groupId: string) {
    if (secondaryEmail.toLowerCase() === adminEmail.toLowerCase()) return;

    const users = await apiResult<UserDto[]>(await page.request.get("/api/users"));
    const secondaryUser = users.find(
        (user) => user.email.toLowerCase() === secondaryEmail.toLowerCase(),
    );

    expect(
        secondaryUser,
        `Không tìm thấy E2E_MEMBER_EMAIL=${secondaryEmail} trong /api/users`,
    ).toBeTruthy();

    await apiCommand(
        await page.request.post(`/api/groups/${groupId}/members`, {
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
        await pollComposer.getByRole("button", { name: /Gửi poll/i }).click();

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
    await expect(page.getByRole("heading", { name: /Hôm nay Erumi/i })).toBeVisible();
    await expect(page.locator(".erumi-composer").first()).toBeVisible();
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
        await expect(page.getByRole("heading", { name: /Phòng họp nhóm/i })).toBeVisible();

        const startResponsePromise = page.waitForResponse(
            (response) =>
                response.url().includes(`/api/groups/${group!.id}/meetings/start`) &&
                response.request().method() === "POST",
        );
        await page.getByRole("button", { name: /Bắt đầu cuộc họp|Start meeting/i }).first().click();
        const meeting = await apiResult<Record<string, string>>(await startResponsePromise);
        meetingId = meeting.id ?? meeting.Id;

        expect(meetingId, "Meeting start response phải có id").toBeTruthy();
        await expect(page.locator(".meeting-status")).toHaveClass(/active/);
        await expect(page.locator(".meeting-status")).toContainText(
            /Đang họp|LiveKit chưa kết nối|Cần kiểm tra kết nối/i,
        );
        await expect(page.locator(".participants-card")).toContainText("Bạn");

        const secondarySession = await newSecondaryPage(browser);
        secondary.context = secondarySession.context;
        await secondarySession.page.goto(`/groups/${group.id}/meeting?meetingId=${meetingId}`);
        await expect(
            secondarySession.page.getByRole("heading", { name: /Phòng họp nhóm/i }),
        ).toBeVisible();
        await expect(secondarySession.page.locator(".meeting-status")).toHaveClass(/active/);
        await expect(secondarySession.page.locator(".meeting-status")).toContainText(
            /Đang họp|LiveKit chưa kết nối|Cần kiểm tra kết nối/i,
        );
        await expect(secondarySession.page.locator(".participants-card")).toContainText("Bạn");
    } finally {
        if (group?.id && meetingId) {
            await page.request
                .post(`/api/groups/${group.id}/meetings/${meetingId}/end`)
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

        await modal.getByRole("button", { name: /Continue/i }).click();
        await expect(
            modal.getByRole("heading", { name: /Preview Wiki page import/i }),
        ).toBeVisible();
        await expect(modal).toContainText("DH02 Wiki Smoke");

        await modal.getByRole("button", { name: /Continue/i }).click();
        await expect(
            modal.getByRole("heading", { name: /Confirm document import/i }),
        ).toBeVisible();

        await modal.getByRole("button", { name: /Create Wiki page/i }).click();
        await expect(modal.getByRole("heading", { name: /Import hoàn tất!/i })).toBeVisible();
        await expect(modal).toContainText("DH02 Wiki Smoke");
    } finally {
        await cleanupProject(page, project?.id);
    }
});
