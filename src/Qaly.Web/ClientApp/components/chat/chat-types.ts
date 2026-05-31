export interface ChatGroupModel {
    id: string;
    name: string;
    description: string;
    unreadCount: number;
}

export interface TeamChatPoll {
    id?: string;
    question: string;
    options: string[];
}

export interface TeamChatAttachment {
    name: string;
    sizeLabel: string;
}

export interface TeamChatMessage {
    id: string;
    groupId: string;
    senderId: string;
    senderName: string;
    senderInitials: string;
    text: string;
    createdAt: string;
    pinned: boolean;
    attachments: TeamChatAttachment[];
    poll?: TeamChatPoll;
}
