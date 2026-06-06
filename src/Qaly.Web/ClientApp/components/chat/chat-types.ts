export interface ChatGroupModel {
    id: string;
    name: string;
    avatarUrl?: string;
    summary: string;
    unreadCount: number;
}

export interface TeamChatPoll {
    id?: string;
    question: string;
    options: string[];
}

export interface TeamChatAttachment {
    id?: string;
    name: string;
    sizeLabel: string;
    contentType?: string;
    url?: string;
    downloadUrl?: string;
    kind?: "file" | "image" | "video";
    sourceFile?: File;
}

export interface TeamChatMeeting {
    id: string;
    joinUrl?: string;
    text: string;
    active: boolean;
}

export interface TeamChatReaction {
    emoji: string;
    count: number;
    userIds: string[];
    reactedByCurrentUser: boolean;
}

export interface TeamChatMemberMention {
    id: string;
    name: string;
    initials: string;
    isAll?: boolean;
}

export interface TeamChatMessage {
    id: string;
    groupId: string;
    senderId: string;
    senderName: string;
    senderInitials: string;
    text: string;
    createdAt: string;
    createdAtRaw: string;
    messageType: string;
    isDeleted: boolean;
    editedAt?: string;
    pinned: boolean;
    pinnedAt?: string;
    pinnedByUserId?: string;
    attachments: TeamChatAttachment[];
    reactions: TeamChatReaction[];
    poll?: TeamChatPoll;
    meeting?: TeamChatMeeting;
}
