export interface DashboardResponse {
    generatedAt: string;
    stats: DashboardStats;
    summary: string;
    riskDigest: string;
    projects: DashboardProject[];
    team: DashboardMember[];
    notifications: DashboardNotification[];
}

export interface DashboardStats {
    activeProjects: number;
    totalTasks: number;
    overdueTasks: number;
    teamMembers: number;
    completedTasks: number;
    completionRate: number;
    tasksAtRisk: number;
}

export interface DashboardProject {
    id: string;
    name: string;
    code: string;
    description: string | null;
    logoUrl: string | null;
    status: string;
    ownerId: string;
    ownerName: string;
    memberCount: number;
    taskCount: number;
    completedTaskCount: number;
    overdueTaskCount: number;
    progressPercentage: number;
    members: DashboardProjectMember[];
    tasks: DashboardTask[];
    createdAt: string;
    endDate: string | null;
}

export interface DashboardProjectMember {
    userId: string;
    fullName: string;
    role: string;
    email: string;
    canViewProjectTimeline: boolean;
    canViewTaskRisk: boolean;
    canNudgeAssignee: boolean;
    canViewUnseenTaskSignal: boolean;
}

export interface DashboardTask {
    id: string;
    title: string;
    status: string;
    priority: string;
    dueDate: string | null;
    assigneeId: string | null;
    assigneeName: string | null;
    reporterName: string;
    projectName: string;
    sortOrder: number;
    rowVersion: string;
    isPrivate: boolean;
    isRestricted: boolean;
    isPinned: boolean;
    contributesToProgress: boolean;
    upvoteCount: number;
    downvoteCount: number;
    commentCount: number;
    attachmentCount: number;
}

export interface DashboardMember {
    id: string;
    fullName: string;
    role: string;
    email: string;
    isActive: boolean;
    assignedTaskCount: number;
    completedTaskCount: number;
    inProgressTaskCount: number;
    overdueTaskCount: number;
    capacityPercent: number;
    focusArea: string;
}

export interface DashboardNotification {
    id: string;
    title: string;
    message: string;
    tone: "critical" | "warning" | "info";
    createdAt: string;
}

export interface ApiResult<T> {
    isSuccess: boolean;
    data: T | null;
    error: string | null;
    statusCode: number;
}

export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

export interface AuditLogDto {
    id: number;
    action: string;
    entityType: string;
    entityId: string;
    changesJson: string | null;
    userId: string | null;
    userName: string | null;
    ipAddress: string | null;
    timestamp: string;
}

export interface UserDto {
    id: string;
    fullName: string;
    email: string;
    role: string;
    isActive: boolean;
    avatarUrl: string | null;
    createdAt: string;
}

export interface ProjectDto {
    id: string;
    name: string;
    code: string;
    description: string | null;
    logoUrl: string | null;
    status: string;
    startDate: string | null;
    endDate: string | null;
    ownerId: string;
    ownerName: string;
    memberCount: number;
    taskCount: number;
    progressPercentage: number;
    labels: ProjectLabelDto[];
    createdAt: string;
}

export interface TaskItemDto {
    id: string;
    title: string;
    description: string | null;
    status: string;
    priority: string;
    dueDate: string | null;
    estimatedHours: number | null;
    actualHours: number | null;
    isPrivate: boolean;
    isRestricted: boolean;
    isPinned: boolean;
    contributesToProgress: boolean;
    upvoteCount: number;
    downvoteCount: number;
    projectId: string;
    projectName: string;
    assigneeId: string | null;
    assigneeName: string | null;
    assignees: TaskAssigneeDto[];
    labels: TaskLabelDto[];
    reporterId: string;
    reporterName: string;
    commentCount: number;
    attachmentCount: number;
    aiPrioritySuggestion: string | null;
    createdAt: string;
    sortOrder: number;
    rowVersion: string;
}

export interface KanbanBoardDto {
    projectId: string;
    columns: KanbanColumnDto[];
}

export interface KanbanColumnDto {
    status: string;
    tasks: TaskItemDto[];
}

export interface KanbanMoveRequest {
    taskId: string;
    fromStatus: string;
    toStatus: string;
    beforeTaskId?: string | null;
    afterTaskId?: string | null;
    rowVersion?: string | null;
}

export interface KanbanMoveResultDto {
    task: TaskItemDto;
    board: KanbanBoardDto;
}

export interface CommentDto {
    id: string;
    content: string;
    taskItemId: string;
    authorId: string;
    authorName: string;
    authorAvatarUrl: string | null;
    parentCommentId: string | null;
    upvoteCount: number;
    downvoteCount: number;
    attachmentCount: number;
    createdAt: string;
    updatedAt: string | null;
}

export interface NotificationDto {
    id: string;
    message: string;
    type: string;
    tone: string;
    isRead: boolean;
    relatedEntityId: string | null;
    relatedEntityType: string | null;
    createdAt: string;
}

export interface AttachmentDto {
    id: string;
    fileName: string;
    filePath: string;
    fileSize: number;
    contentType: string | null;
    scope: string;
    projectId: string | null;
    taskItemId: string | null;
    commentId: string | null;
    uploadedById: string;
    uploadedByName: string;
    uploadedAt: string;
    isEvidence: boolean;
    evidenceApprovalStatus: string;
    evidenceReviewedById: string | null;
    evidenceReviewedByName: string | null;
    evidenceReviewedAt: string | null;
    evidenceReviewNote: string | null;
}

export interface WikiPageDto {
    id: string;
    title: string;
    content: string;
    visibility: string;
    authorName: string;
    updatedAt: string;
}

export interface TimeEntryDto {
    id: string;
    taskId: string;
    taskTitle: string;
    userId: string;
    userName: string;
    startedAt: string;
    endedAt: string | null;
    manualMinutes: number | null;
    totalMinutes: number;
    note: string | null;
    createdAt: string;
}

export interface CreateTimeEntryDto {
    taskId: string;
    startedAt: string;
    endedAt?: string | null;
    manualMinutes?: number | null;
    note?: string | null;
}

export interface ProjectLabelDto {
    id: string;
    name: string;
    color: string;
    createdAt: string;
}

export interface ProjectTimelineDto {
    projectId: string;
    windowStart: string;
    windowEnd: string;
    sprintStart: string;
    sprintEnd: string;
    totalTasks: number;
    openTasks: number;
    doneTasks: number;
    overdueTasks: number;
    blockedTasks: number;
    buckets: SprintBucketDto[];
    blockedItems: TimelineDependencyDto[];
}

export interface ProjectWorkloadDto {
    projectId: string;
    membersWorkload: MemberWorkloadDto[];
}

export interface MemberWorkloadDto {
    userId: string;
    userName: string;
    avatarUrl: string | null;
    taskCount: number;
    estimatedHours: number;
    actualHours: number;
    completedTaskCount: number;
}

export interface SprintBucketDto {
    label: string;
    startDate: string;
    endDate: string;
    taskCount: number;
    doneCount: number;
    overdueCount: number;
    activeCount: number;
    plannedPoints: number;
}

export interface TimelineDependencyDto {
    taskId: string;
    title: string;
    status: string;
    dueDate: string | null;
    blockingTaskIds: string[];
    isBlocked: boolean;
}

export interface TaskAssigneeDto {
    userId: string;
    fullName: string;
    avatarUrl: string | null;
}

export interface TaskLabelDto {
    id: string;
    name: string;
    color: string;
}

export interface TaskAttentionDto {
    id: string;
    title: string;
    projectId: string;
    projectName: string;
    status: string;
    priority: string;
    startDate: string | null;
    dueDate: string | null;
    reporterId: string;
    reporterName: string;
    assigneeId: string | null;
    assigneeName: string | null;
    assignedAt: string | null;
    lastViewedAt: string | null;
    isDueSoon: boolean;
    isOverdue: boolean;
    isStaleTodo: boolean;
    isStaleInProgress: boolean;
    isUnseenByAssignee: boolean;
    reasons: string[];
    allowedActions: string[];
}

export interface TaskMeetingSourceDto {
    taskId: string;
    meetingImportId: string;
    meetingTitle: string;
    meetingStartedAt: string | null;
    actionItemIndex: number;
    actionItemTitle: string | null;
    actionItemDescription: string | null;
    sourcePriority: string | null;
    sourceDueDate: string | null;
    sourceQuote: string | null;
    mappingStatus: string;
    linkedTaskId: string | null;
}

export interface TaskAssignmentInsightDto {
    taskId: string;
    projectId: string;
    taskTitle: string;
    taskDescription: string | null;
    taskPriority: string;
    taskStatus: string;
    dueDate: string | null;
    recommendedUserId: string | null;
    recommendedUserName: string;
    recommendationSummary: string;
    generatedAt: string;
    candidates: TaskAssignmentCandidateDto[];
}

export interface TaskAssignmentCandidateDto {
    userId: string;
    fullName: string;
    role: string;
    activeTaskCount: number;
    overdueTaskCount: number;
    recentCompletionCount: number;
    skillMatchScore: number;
    historyScore: number;
    workloadScore: number;
    totalScore: number;
    skillSignals: string[];
    recentSignals: string[];
}

export interface SearchResultDto {
    type: string;
    id: string;
    title: string;
    summary: string | null;
    projectId: string | null;
    url: string;
}

export interface VoteSummaryDto {
    targetType: string;
    targetId: string;
    upvoteCount: number;
    downvoteCount: number;
    score: number;
    myVote: number;
}

export interface AttentionSummaryDto {
    overdueTasks: number;
    dueSoonTasks: number;
    riskProjects: number;
    blockedTasks: number;
    safeProjects: number;
    totalAttentionItems: number;
}

export interface ActivityByDayDto {
    date: string;
    count: number;
}

export interface RecentActivityDto {
    type: string;
    title: string;
    actorName: string;
    projectName: string | null;
    projectId: string | null;
    createdAt: string;
}

export interface RecentActivitiesResponseDto {
    todayCount: number;
    weekCount: number;
    activityByDay: ActivityByDayDto[];
    latestActivities: RecentActivityDto[];
}

export interface StrategicOverviewDto {
    workspaceHealthScore: number;
    averageProjectProgress: number;
    taskCompletionRate: number;
    riskProjectCount: number;
    overdueTaskCount: number;
    dueSoonTaskCount: number;
    activeProjectCount: number;
    teamWorkloadLevel: string;
    riskLevel: string;
    topPriorityTasks: DashboardTask[];
}

export interface AiStrategyResponseDto {
    summary: string;
    riskAnalysis: string[];
    recommendations: string[];
    priorityPlan: string[];
}
