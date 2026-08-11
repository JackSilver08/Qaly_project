import { expect, test, type Page } from "@playwright/test";
import { browserApiRequest, type BrowserApiResponse } from "./support/browser-api";
import {
    adminEmail,
    adminPassword,
    memberEmail,
    memberPassword,
} from "./support/credentials";

type ApiResult<T> = {
    isSuccess: boolean;
    data: T | null;
    error: string | null;
};

type Entity = { id: string; name: string };
type User = { id: string; email: string };

async function login(page: Page, email: string, password: string) {
    await page.goto("/Account/Login", { waitUntil: "domcontentloaded" });
    await page.locator('input[name="Email"]').fill(email);
    await page.locator('input[name="Password"]').fill(password);
    await page.getByRole("button", { name: /Đăng nhập|Login/i }).click();
    await page.waitForURL((url) => !url.pathname.startsWith("/Account/Login"), {
        timeout: 20_000,
    });
    await expect(page.locator(".shell-header")).toBeVisible();
}

async function apiResult<T>(response: BrowserApiResponse): Promise<T> {
    const body = (await response.json()) as ApiResult<T> | T;
    expect(response.ok(), `${response.url()} returned ${response.status()}`).toBeTruthy();
    if (body && typeof body === "object" && "isSuccess" in body) {
        const result = body as ApiResult<T>;
        expect(result.isSuccess, result.error ?? "API result failed").toBeTruthy();
        expect(result.data).not.toBeNull();
        return result.data as T;
    }
    return body as T;
}

async function apiCommand(response: BrowserApiResponse) {
    const body = (await response.json()) as ApiResult<unknown>;
    expect(response.ok(), `${response.url()} returned ${response.status()}`).toBeTruthy();
    if (body && typeof body === "object" && "isSuccess" in body) {
        expect(body.isSuccess, body.error ?? "API command failed").toBeTruthy();
    }
}

function unique(prefix: string) {
    return `${prefix} ${Date.now()} ${Math.random().toString(36).slice(2, 8)}`;
}

test("organization revoke closes project, task, group, meeting, Wiki, AI and analytics deep-links", async ({
    browser,
    page,
}) => {
    let organizationId = "";
    let projectId = "";
    let groupId = "";
    let memberContext: Awaited<ReturnType<typeof browser.newContext>> | undefined;

    await login(page, adminEmail, adminPassword);

    try {
        const users = await apiResult<User[]>(
            await browserApiRequest(page, "GET", "/api/users"),
        );
        const member = users.find(
            (user) => user.email.toLowerCase() === memberEmail.toLowerCase(),
        );
        expect(member, `Seed user ${memberEmail} must exist`).toBeTruthy();

        const organization = await apiResult<Entity>(
            await browserApiRequest(page, "POST", "/api/organizations", {
                data: {
                    name: unique("RC tenant"),
                    code: null,
                    description: "E2E tenant revocation boundary",
                    ownerId: null,
                },
            }),
        );
        organizationId = organization.id;
        await apiCommand(
            await browserApiRequest(
                page,
                "POST",
                `/api/organizations/${organizationId}/members`,
                { data: { userId: member!.id, role: "Member" } },
            ),
        );

        const project = await apiResult<Entity>(
            await browserApiRequest(page, "POST", "/api/projects", {
                data: {
                    name: unique("RC project"),
                    code: null,
                    description: "E2E project read-back",
                    logoUrl: null,
                    startDate: null,
                    endDate: null,
                    organizationId,
                    sourceGroupId: null,
                },
            }),
        );
        projectId = project.id;
        await apiCommand(
            await browserApiRequest(
                page,
                "POST",
                `/api/projects/${projectId}/members`,
                { data: { userId: member!.id, role: "Member" } },
            ),
        );

        const task = await apiResult<{ id: string }>(
            await browserApiRequest(page, "POST", "/api/tasks", {
                data: {
                    title: unique("RC task"),
                    description: "Persisted task for tenant revocation",
                    priority: "High",
                    dueDate: null,
                    estimatedHours: 2,
                    projectId,
                    assigneeId: member!.id,
                    assigneeIds: [member!.id],
                    labelIds: [],
                    isPrivate: false,
                    isPinned: false,
                    contributesToProgress: true,
                },
            }),
        );

        const group = await apiResult<Entity>(
            await browserApiRequest(page, "POST", "/api/groups", {
                data: {
                    name: unique("RC group"),
                    organizationId,
                    color: "#2563eb",
                },
            }),
        );
        groupId = group.id;
        await apiCommand(
            await browserApiRequest(page, "POST", `/api/groups/${groupId}/members`, {
                data: { userId: member!.id, role: "Member" },
            }),
        );

        memberContext = await browser.newContext({
            baseURL: process.env.E2E_BASE_URL ?? "http://127.0.0.1:5000",
        });
        const memberPage = await memberContext.newPage();
        await login(memberPage, memberEmail, memberPassword);

        expect(
            (await browserApiRequest(memberPage, "GET", `/api/projects/${projectId}`)).status(),
        ).toBe(200);
        expect(
            (await browserApiRequest(memberPage, "GET", `/api/tasks/${task.id}`)).status(),
        ).toBe(200);
        expect(
            (await browserApiRequest(memberPage, "GET", `/api/groups/${groupId}`)).status(),
        ).toBe(200);

        await memberPage.goto(`/projects/${projectId}`);
        await expect(memberPage.getByRole("heading", { name: project.name })).toBeVisible();
        await memberPage
            .locator(".project-tabs")
            .getByRole("button", { name: /Lộ Trình Dự Án/i })
            .click();
        await expect(memberPage).toHaveURL(new RegExp(`projects/${projectId}\\?tab=roadmap`));
        await memberPage.reload();
        await expect(
            memberPage.locator(".project-tabs .is-active"),
        ).toContainText(/Lộ Trình Dự Án/i);
        await memberPage
            .locator(".project-tabs")
            .getByRole("button", { name: /Nhiệm vụ/i })
            .click();
        await memberPage.goBack();
        await expect(
            memberPage.locator(".project-tabs .is-active"),
        ).toContainText(/Lộ Trình Dự Án/i);

        await apiCommand(
            await browserApiRequest(
                page,
                "DELETE",
                `/api/organizations/${organizationId}/members/${member!.id}`,
            ),
        );

        const protectedRoutes = [
            `/api/projects/${projectId}`,
            `/api/tasks/${task.id}`,
            `/api/projects/${projectId}/wiki`,
            `/api/groups/${groupId}`,
            `/api/groups/${groupId}/meetings/active`,
            `/api/ai/projects/${projectId}/summary`,
            `/api/analytics/projects/${projectId}`,
        ];
        for (const route of protectedRoutes) {
            const response = await browserApiRequest(memberPage, "GET", route);
            expect(response.status(), `${route} must close after tenant revoke`).toBe(403);
        }

        await memberPage.goto(`/projects/${projectId}`);
        await expect(memberPage.locator(".route-entity-error")).toBeVisible();
        await expect(memberPage.locator("body")).not.toContainText(task.id);
    } finally {
        await memberContext?.close();
        if (groupId) {
            await browserApiRequest(page, "DELETE", `/api/groups/${groupId}`);
        }
        if (projectId) {
            await browserApiRequest(page, "DELETE", `/api/projects/${projectId}`);
        }
        if (organizationId) {
            await browserApiRequest(page, "DELETE", `/api/organizations/${organizationId}`);
        }
    }
});
