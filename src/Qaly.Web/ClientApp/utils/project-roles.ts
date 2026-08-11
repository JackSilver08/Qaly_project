/**
 * Project role catalogue shown in the UI.
 *
 * Must stay aligned with `ProjectRoleRules.AssignableRoles` on the server. The server is the
 * enforcement boundary — this list only decides what a manager can pick, never what is allowed.
 */

import type { ProjectPermissionsDto } from "../types";

export interface ProjectRoleOption {
  /** Role key sent to the API. */
  value: string;
  /** Vietnamese label shown to the user. */
  label: string;
  /** One-line explanation of what the role may do. */
  hint: string;
  /** Groups roles in the picker. */
  group: "Quản lý" | "Chuyên môn" | "Cơ bản" | "Chỉ đọc";
}

export const PROJECT_ROLE_OPTIONS: readonly ProjectRoleOption[] = [
  {
    value: "Manager",
    label: "Quản lý dự án",
    hint: "Toàn quyền: thành viên, task, sprint, AI, tích hợp.",
    group: "Quản lý",
  },
  {
    value: "ScrumMaster",
    label: "Scrum Master",
    hint: "Điều phối sprint và quy trình, quản lý được thành viên.",
    group: "Quản lý",
  },
  {
    value: "Developer",
    label: "Lập trình viên",
    hint: "Nhận và xử lý task, cập nhật tiến độ, dùng AI hỗ trợ code.",
    group: "Chuyên môn",
  },
  {
    value: "Tester",
    label: "Kiểm thử viên",
    hint: "Kiểm thử, báo lỗi, xác nhận evidence của task.",
    group: "Chuyên môn",
  },
  {
    value: "Reviewer",
    label: "Người review",
    hint: "Review kết quả và duyệt evidence, không sửa cấu hình dự án.",
    group: "Chuyên môn",
  },
  {
    value: "Member",
    label: "Thành viên",
    hint: "Làm task được giao. AI chỉ xem tiến độ và tóm tắt.",
    group: "Cơ bản",
  },
  {
    value: "Viewer",
    label: "Người xem",
    hint: "Chỉ đọc. Không ghi bất kỳ dữ liệu nào.",
    group: "Chỉ đọc",
  },
  {
    value: "Customer",
    label: "Khách hàng",
    hint: "Chỉ đọc phần được chia sẻ, không thấy wiki nội bộ.",
    group: "Chỉ đọc",
  },
];

/** Owner is never assignable through membership updates; ownership transfer has its own flow. */
export const PROJECT_ROLE_OWNER = "Owner";

const ROLE_LOOKUP = new Map(
  PROJECT_ROLE_OPTIONS.map((option) => [option.value.toLowerCase(), option]),
);

export function projectRoleLabel(role: string | null | undefined): string {
  if (!role) return "Thành viên";
  const normalized = String(role).replace(/\s+/g, "").toLowerCase();
  if (normalized === "owner" || normalized === "projectowner") return "Chủ dự án";
  return ROLE_LOOKUP.get(normalized)?.label ?? role;
}

export function projectRoleHint(role: string | null | undefined): string {
  if (!role) return "";
  const normalized = String(role).replace(/\s+/g, "").toLowerCase();
  if (normalized === "owner" || normalized === "projectowner") {
    return "Người tạo dự án. Toàn quyền và không thể bị gỡ.";
  }
  return ROLE_LOOKUP.get(normalized)?.hint ?? "";
}

/**
 * Client-side reconstruction of `ProjectPermissionRules.Resolve`, used only when the server payload
 * predates the `permissions` field. Prefer the server's answer whenever it is present.
 */
export function fallbackProjectPermissions(input: {
  role: string | null | undefined;
  isOwner: boolean;
  isSystemAdmin: boolean;
}): ProjectPermissionsDto {
  const normalized = String(input.role ?? "").replace(/\s+/g, "").toLowerCase();
  const isMember = input.isOwner || input.isSystemAdmin || normalized.length > 0;
  const manages =
    input.isOwner ||
    input.isSystemAdmin ||
    ["owner", "manager", "admin", "pm", "projectowner", "projectmanager", "scrummaster"].includes(
      normalized,
    );
  const readOnly = !manages && (normalized === "viewer" || normalized === "customer");
  const canWrite = isMember && !readOnly;
  const specialist = manages || ["developer", "tester", "reviewer"].includes(normalized);
  const reviews = manages || ["reviewer", "tester"].includes(normalized);

  const aiTier: ProjectPermissionsDto["aiTier"] = manages
    ? "Full"
    : specialist
      ? "Specialist"
      : readOnly
        ? "ReadOnly"
        : isMember
          ? "Contributor"
          : "None";

  const effectiveRole = input.isOwner ? "Owner" : (input.role ?? "");

  return {
    role: isMember ? effectiveRole : "",
    roleLabel: isMember ? projectRoleLabel(effectiveRole) : "Không thuộc dự án",
    canManageProject: manages,
    canManageMembers: manages,
    canManageAllTasks: manages,
    canCreateTask: specialist,
    canUpdateOwnTasks: canWrite,
    canComment: canWrite,
    canTrackTime: canWrite,
    canReviewEvidence: reviews,
    canReadInternalWiki: isMember && normalized !== "customer",
    canWriteWiki: canWrite,
    canManageIntegrations: manages,
    aiTier,
    aiTierDescription: AI_TIER_DESCRIPTIONS[aiTier],
  };
}

export const AI_TIER_DESCRIPTIONS: Record<ProjectPermissionsDto["aiTier"], string> = {
  Full: "Toàn quyền AI: phân tích, lập kế hoạch, đề xuất phân công, tạo và sửa task.",
  Specialist: "AI chuyên môn: phân tích khối lượng nhóm, tạo và định hình task, hỏi đáp dự án.",
  Contributor: "AI cơ bản: xem tiến độ, tóm tắt, và thao tác trên công việc của chính bạn.",
  ReadOnly: "AI chỉ đọc: xem tiến độ và tóm tắt dự án.",
  None: "Không có quyền dùng AI trong dự án này.",
};

/** Roles grouped for a `<optgroup>` picker. */
export function groupedProjectRoles(): { group: string; options: ProjectRoleOption[] }[] {
  const groups: { group: string; options: ProjectRoleOption[] }[] = [];
  for (const option of PROJECT_ROLE_OPTIONS) {
    const existing = groups.find((item) => item.group === option.group);
    if (existing) existing.options.push(option);
    else groups.push({ group: option.group, options: [option] });
  }
  return groups;
}
