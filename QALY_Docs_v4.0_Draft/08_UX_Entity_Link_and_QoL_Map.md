# Qaly UX Entity Link and Quality-of-Life Map v4.0 Draft

## 1. UX objective

Users should move from signal to source to action without reconstructing context. A risk summary opens its contributing tasks; a task created from a meeting opens the source action item; a notification opens the authorized target; a group exposes linked projects; back navigation returns to the prior filtered context.

Current strengths are project and task workspaces, saved task views, bulk actions, meeting-to-task mapping, import preview and undo, archive restore, group deep links, and contextual AI panels.

Current gaps include inconsistent task URLs, source labels without links, incomplete Group-to-Project reciprocity, limited hover preview, and inconsistent loading or success behavior.

## 2. Canonical entity routes

| Entity | Target canonical route | Minimum context |
|---|---|---|
| Organization | /organizations/{organizationId} | Organization name and role |
| Project | /projects/{projectId} | Project name, code, status |
| Project task | /projects/{projectId}/tasks/{taskId} | Project and task IDs |
| Primary collaboration | /projects/{projectId}/collaboration | Project and primary group IDs |
| Group | /groups/{groupId} | Group ID and current tab in query or route |
| Group message | /groups/{groupId}/messages/{messageId} | Group and message IDs |
| Meeting | /groups/{groupId}/meetings/{meetingId} | Group, meeting, and transcript permission |
| Meeting action item | /groups/{groupId}/meetings/{meetingId}/actions/{actionIndexOrId} | Stable action ID is preferred over index |
| Wiki page | /projects/{projectId}/wiki/{pageId} | Project and page IDs |
| Notification | Resolves to its source canonical route | Source type and ID |
| Audit event | /audit/{auditId} with authorized entity link | Audit ID and source entity |
| AI job or draft | /projects/{projectId}/ai/jobs/{jobId} or drafts/{draftId} | Project, job or draft, and source refs |

Legacy project-only task openings redirect or replace state with the canonical task route. Closing a drawer returns to the project URL without losing project tab, sort, or filter context.

## 3. Entity resolver contract

One backend and frontend entity-type registry maps canonical type to route, label, icon, permission policy, preview loader, and supported actions.

Unknown or deleted entities render a safe tombstone. Forbidden entities do not leak title, excerpt, assignee, or existence beyond policy. Resolver output is used by search, notifications, audit, AI sources, activity feeds, backlinks, and hover previews.

## 4. Permission-aware hover preview

Hover and keyboard focus may open a preview after a short stable delay. Touch uses explicit tap or info action. Preview never fetches content before authorization.

| Entity | Preview fields | Contextual actions |
|---|---|---|
| Project | Name, code, status, progress, owner, next milestone | Open, view board, view collaboration |
| Task | Title, status, priority, assignee, due date, blocker, source badge | Open, quick status where allowed, comment, copy link |
| Member | Name, project role, declared skills, capacity band | Open profile, view assigned work; no hidden performance data |
| Group | Name, member count, active meeting, linked projects | Open chat, join meeting, view linked projects |
| Message | Author, timestamp, short authorized excerpt, thread context | Open, reply, create draft task |
| Meeting | Title, time, participants count, consent state, summary availability | Open, review actions, view linked tasks |
| Wiki | Title, last editor, last update, version | Open, compare versions where allowed |
| AI source | Type, label, evidence, version, timestamp, confidence | Open source, ask follow-up, copy canonical link |

Previews use fixed dimensions, skeleton state, keyboard focus management, and escape or outside-click dismissal. They are not nested cards inside cards.

## 5. Cross-module map

| From | Signal | Target | Expected action |
|---|---|---|---|
| Dashboard | Overdue or blocked task | Canonical task | Review source, update, comment, or nudge |
| Project | Collaboration indicator | Primary group | Continue discussion or meeting |
| Group | Linked project badge | Project | Open board, timeline, or project AI context |
| Message | Action-language selection | AI task draft | Edit, reject, or confirm task |
| Meeting | Extracted action | Task draft or linked task | Confirm, link, or open existing task |
| Task | Meeting source badge | Meeting action | Review exact evidence and surrounding transcript |
| Analyst | Metric, risk, or recommendation | Source entity list | Open contributing tasks, members, meetings, or Wiki |
| Notification | Mention, assignment, status, evidence | Authorized source | Resolve action and return to notification context |
| Audit | Entity change | Current entity and optional diff | Inspect state without implying rollback |
| Version history | Historical snapshot | Diff and rollback preview | Confirm compensating update |

## 6. Breadcrumb and back behavior

- Breadcrumbs reflect entity hierarchy, not the sequence of modal openings.
- Browser Back restores the previous URL and local filter state where safe.
- A task opened from search carries a return-context token in history state, not in an authorization decision.
- Refreshing a canonical URL rehydrates the same entity or renders 403, 404, or tombstone.
- Closing a task, meeting action, AI source, or version diff never routes to an unrelated default page.

## 7. Contextual actions

Actions appear near the entity that provides their context. Permissions are returned by the backend or derived from an authoritative policy payload.

Examples include create task from message, link meeting action to task, ask Analyst about a source, nudge assignee, review evidence, compare Wiki versions, and open the primary group.

AI actions are labeled as Generate, Suggest, Explain, or Draft. Buttons must not imply that AI has already assigned, scheduled, approved, or completed work.

## 8. Source references

AI responses use structured source refs with type, ID, canonical URL, evidence, timestamp, source version, and confidence. Legacy label-only sources display Unresolved source and cannot show an active Open action.

Sources open in the same application by default. New tabs are reserved for external references or an explicit user action. Access is rechecked at open time.

## 9. Quality-of-life standards

| Pattern | Requirement |
|---|---|
| Search | Preserve query, scope, filters, and keyboard navigation; results use canonical links. |
| Saved view | Persist owner, filter schema version, sort, columns, and optional project scope; handle stale fields. |
| Bulk action | Preview count and impact; apply per-item authorization; return partial-failure detail. |
| Undo | Show bounded expiry, affected records, and result; do not call generic rollback. |
| Loading | Stable skeleton dimensions; no undefined labels or layout shifts. |
| Empty | Explain the current state and offer only a relevant next action. |
| Error | Preserve user input, state a safe cause, offer retry, and never show success afterward. |
| Offline or degraded | Label affected dependency and available read-only or fallback behavior. |
| Success | Display only after every required operation succeeds; partial success lists failures. |
| Accessibility | Keyboard operation, focus restoration, semantic labels, contrast, reduced motion, and screen-reader status. |
| Mobile | No overlapping controls; drawers become full-height sheets; canonical links remain shareable. |

Settings must not display global success after an organization or project save failed. Local-only preferences are labeled as device preferences and are not represented as server policy.

## 10. UX acceptance scenarios

1. Open a task from Dashboard, refresh, share URL, use Back, and preserve prior filter context.
2. Open a meeting-derived task, navigate to the exact action source, and return to task.
3. Open Analyst source references and receive authorized entity pages, not labels only.
4. Create a project from a group, return to the group, and find the linked project.
5. Attempt every preview as an unauthorized user and confirm no hidden data leaks.
6. Simulate slow, failed, and partial saves and confirm stable layout and truthful feedback.
7. Complete all flows by keyboard and at mobile viewport without overlap.

## 11. Telemetry

Measure canonical-link resolution failures, permission-denied previews, back-navigation abandonment, source-open rate, task-from-message confirmation, meeting-action linkage, save failures, undo success, and degraded dependency exposure. Telemetry excludes sensitive content.
