# Task and Kanban API Contract

## Common Response

All Task/Kanban endpoints return the existing service envelope:

```json
{
  "isSuccess": true,
  "data": {},
  "error": null,
  "statusCode": 200
}
```

For write conflicts, the API returns `409` and, where available, the latest server copy in `data`.

## Task DTO

`TaskItemDto` includes a `rowVersion` field. Clients must keep this value and send it back when updating or moving a task.

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "title": "Design Kanban API",
  "status": "Todo",
  "priority": "High",
  "projectId": "22222222-2222-2222-2222-222222222222",
  "sortOrder": 1000,
  "rowVersion": "AAAAAAAAB9E="
}
```

Valid status values:

- `Todo`
- `InProgress`
- `OnHold`
- `InReview`
- `Done`
- `Cancelled`

Workflow transitions are enforced by `TaskStatusRules`.

## Get Kanban Board

```http
GET /api/tasks/project/{projectId}/kanban
```

Response:

```json
{
  "isSuccess": true,
  "data": {
    "projectId": "22222222-2222-2222-2222-222222222222",
    "columns": [
      {
        "status": "Todo",
        "tasks": []
      },
      {
        "status": "InProgress",
        "tasks": []
      }
    ]
  },
  "error": null,
  "statusCode": 200
}
```

## Move Task on Kanban

```http
PATCH /api/tasks/project/{projectId}/kanban/move
Content-Type: application/json
```

Request:

```json
{
  "taskId": "11111111-1111-1111-1111-111111111111",
  "fromStatus": "Todo",
  "toStatus": "InProgress",
  "beforeTaskId": null,
  "afterTaskId": "33333333-3333-3333-3333-333333333333",
  "rowVersion": "AAAAAAAAB9E="
}
```

Rules:

- Send either `beforeTaskId` or `afterTaskId`, not both.
- Omit both to append the task to the end of the target column.
- `fromStatus` must match the server-side current task status.
- `toStatus` must be allowed by `TaskStatusRules`.
- Moving to `Done` still requires approved evidence.
- `rowVersion` is checked to prevent stale drag-drop updates.
- The move updates status and sort order together in one save.

Success response:

```json
{
  "isSuccess": true,
  "data": {
    "task": {
      "id": "11111111-1111-1111-1111-111111111111",
      "status": "InProgress",
      "sortOrder": 2000,
      "rowVersion": "AAAAAAAAB9I="
    },
    "board": {
      "projectId": "22222222-2222-2222-2222-222222222222",
      "columns": []
    }
  },
  "error": null,
  "statusCode": 200
}
```

Conflict response:

```json
{
  "isSuccess": false,
  "data": {
    "task": {
      "id": "11111111-1111-1111-1111-111111111111",
      "status": "InReview",
      "rowVersion": "AAAAAAAAB9Q="
    },
    "board": {
      "projectId": "22222222-2222-2222-2222-222222222222",
      "columns": []
    }
  },
  "error": "Task was modified by another request. Refresh before moving.",
  "statusCode": 409
}
```

## Update Task

```http
PUT /api/tasks/{id}
Content-Type: application/json
```

`UpdateTaskDto` accepts optional `rowVersion`. If supplied and stale, the API returns `409` with the latest `TaskItemDto`.

```json
{
  "title": "Design Kanban API",
  "description": "Atomic move endpoint",
  "status": "InProgress",
  "priority": "High",
  "dueDate": "2026-05-30T00:00:00+07:00",
  "estimatedHours": 8,
  "actualHours": 2,
  "assigneeId": "44444444-4444-4444-4444-444444444444",
  "isPrivate": false,
  "isPinned": false,
  "contributesToProgress": true,
  "assigneeIds": [],
  "labelIds": [],
  "rowVersion": "AAAAAAAAB9E="
}
```
