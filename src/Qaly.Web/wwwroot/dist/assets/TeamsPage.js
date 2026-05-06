import { k as Plus, r as Pin, s as FileUp, t as SmilePlus, V as Vote, o as Send } from './vendor-icons.js';
import { d as defineComponent, c as createElementBlock, a as createBaseVNode, g as createVNode, i as unref, F as Fragment, b as renderList, o as openBlock, n as normalizeClass, t as toDisplayString, l as createCommentVNode, s as watch, z as withDirectives, A as vModelText, y as withModifiers, m as ref, B as computed, e as createBlock, x as nextTick } from './vendor-vue.js';
import { u as useDashboardContext } from './main.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
const _hoisted_1$3 = { class: "team-chat-sidebar glass-card" };
const _hoisted_2$3 = { class: "team-chat-sidebar__header" };
const _hoisted_3$3 = ["onClick"];
const _hoisted_4$2 = { key: 0 };
const _sfc_main$3 = /*@__PURE__*/ defineComponent({
    __name: 'ChatSidebar',
    props: {
        groups: {},
        activeGroupId: {}
    },
    emits: ["select", "create"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("aside", _hoisted_1$3, [
                createBaseVNode("div", _hoisted_2$3, [
                    _cache[1] || (_cache[1] = createBaseVNode("div", null, [
                        createBaseVNode("span", null, "Groups"),
                        createBaseVNode("h2", null, "Nhóm chat")
                    ], -1)),
                    createBaseVNode("button", {
                        class: "icon-button icon-button--small",
                        type: "button",
                        "aria-label": "Tạo nhóm mới",
                        onClick: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('create')))
                    }, [
                        createVNode(unref(Plus), { size: 16 })
                    ])
                ]),
                (openBlock(true), createElementBlock(Fragment, null, renderList(__props.groups, (group) => {
                    return (openBlock(), createElementBlock("button", {
                        key: group.id,
                        type: "button",
                        class: normalizeClass(["team-chat-group", { 'is-active': group.id === __props.activeGroupId }]),
                        onClick: ($event) => (_ctx.$emit('select', group.id))
                    }, [
                        createBaseVNode("div", null, [
                            createBaseVNode("strong", null, toDisplayString(group.name), 1),
                            createBaseVNode("span", null, toDisplayString(group.description), 1)
                        ]),
                        (group.unreadCount > 0)
                            ? (openBlock(), createElementBlock("small", _hoisted_4$2, toDisplayString(group.unreadCount), 1))
                            : createCommentVNode("", true)
                    ], 10, _hoisted_3$3));
                }), 128))
            ]));
        };
    }
});
const _hoisted_1$2 = { class: "team-message__avatar" };
const _hoisted_2$2 = { class: "team-message__bubble" };
const _hoisted_3$2 = { class: "team-message__meta" };
const _hoisted_4$1 = ["aria-label"];
const _hoisted_5$1 = { key: 0 };
const _hoisted_6$1 = {
    key: 1,
    class: "team-message__attachments"
};
const _hoisted_7$1 = {
    key: 2,
    class: "team-message__poll"
};
const _sfc_main$2 = /*@__PURE__*/ defineComponent({
    __name: 'MessageItem',
    props: {
        message: {},
        currentUserId: {}
    },
    emits: ["pin"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("article", {
                class: normalizeClass(["team-message", { 'is-mine': __props.message.senderId === __props.currentUserId }])
            }, [
                createBaseVNode("div", _hoisted_1$2, toDisplayString(__props.message.senderInitials), 1),
                createBaseVNode("div", _hoisted_2$2, [
                    createBaseVNode("div", _hoisted_3$2, [
                        createBaseVNode("strong", null, toDisplayString(__props.message.senderName), 1),
                        createBaseVNode("span", null, toDisplayString(__props.message.createdAt), 1),
                        createBaseVNode("button", {
                            type: "button",
                            "aria-label": __props.message.pinned ? 'Bỏ ghim' : 'Ghim tin nhắn',
                            onClick: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('pin', __props.message.id)))
                        }, [
                            createVNode(unref(Pin), { size: 13 })
                        ], 8, _hoisted_4$1)
                    ]),
                    (__props.message.text)
                        ? (openBlock(), createElementBlock("p", _hoisted_5$1, toDisplayString(__props.message.text), 1))
                        : createCommentVNode("", true),
                    (__props.message.attachments.length)
                        ? (openBlock(), createElementBlock("div", _hoisted_6$1, [
                            (openBlock(true), createElementBlock(Fragment, null, renderList(__props.message.attachments, (file) => {
                                return (openBlock(), createElementBlock("span", {
                                    key: file.name
                                }, toDisplayString(file.name) + " · " + toDisplayString(file.sizeLabel), 1));
                            }), 128))
                        ]))
                        : createCommentVNode("", true),
                    (__props.message.poll)
                        ? (openBlock(), createElementBlock("div", _hoisted_7$1, [
                            createBaseVNode("strong", null, toDisplayString(__props.message.poll.question), 1),
                            (openBlock(true), createElementBlock(Fragment, null, renderList(__props.message.poll.options, (option) => {
                                return (openBlock(), createElementBlock("button", {
                                    key: option,
                                    type: "button"
                                }, toDisplayString(option), 1));
                            }), 128))
                        ]))
                        : createCommentVNode("", true)
                ])
            ], 2));
        };
    }
});
const _hoisted_1$1 = { class: "team-chat-window glass-card" };
const _hoisted_2$1 = { class: "team-chat-window__header" };
const _hoisted_3$1 = {
    key: 0,
    class: "team-pinned"
};
const _hoisted_4 = {
    key: 0,
    class: "team-pinned-list"
};
const _hoisted_5 = {
    key: 1,
    class: "team-poll-composer"
};
const _hoisted_6 = ["onUpdate:modelValue", "placeholder"];
const _hoisted_7 = {
    key: 2,
    class: "team-attachment-preview"
};
const _hoisted_8 = {
    class: "icon-button icon-button--small",
    "aria-label": "Gửi file"
};
const _hoisted_9 = {
    class: "primary-button primary-button--compact",
    type: "submit"
};
const _hoisted_10 = {
    key: 3,
    class: "team-emoji-picker glass-card"
};
const _hoisted_11 = ["onClick"];
const _sfc_main$1 = /*@__PURE__*/ defineComponent({
    __name: 'ChatWindow',
    props: {
        group: {},
        messages: {},
        currentUserId: {}
    },
    emits: ["send", "pin"],
    setup(__props, { emit: __emit }) {
        const props = __props;
        const emit = __emit;
        const draft = ref('');
        const showEmoji = ref(false);
        const showPoll = ref(false);
        const pollQuestion = ref('');
        const pollOptions = ref(['', '']);
        const pendingAttachments = ref([]);
        const bodyRef = ref(null);
        const pinnedMessages = computed(() => props.messages.filter((message) => message.pinned));
        const emojiOptions = ['👍', '✅', '🔥', '🎯', '🙏', '💡'];
        watch(() => props.messages.length, async () => {
            await nextTick();
            bodyRef.value?.scrollTo({ top: bodyRef.value.scrollHeight, behavior: 'smooth' });
        });
        function attachFiles(event) {
            const input = event.target;
            const files = Array.from(input.files ?? []);
            pendingAttachments.value = files.map((file) => ({
                name: file.name,
                sizeLabel: file.size < 1024 ? `${file.size} B` : `${Math.round(file.size / 1024)} KB`,
            }));
            input.value = '';
        }
        function addEmoji(emoji) {
            draft.value += emoji;
            showEmoji.value = false;
        }
        function sendMessage() {
            const text = draft.value.trim();
            const options = pollOptions.value.map((option) => option.trim()).filter(Boolean);
            const poll = showPoll.value && pollQuestion.value.trim() && options.length >= 2
                ? { question: pollQuestion.value.trim(), options }
                : undefined;
            if (!text && pendingAttachments.value.length === 0 && !poll)
                return;
            emit('send', {
                text,
                attachments: pendingAttachments.value,
                poll,
            });
            draft.value = '';
            pendingAttachments.value = [];
            pollQuestion.value = '';
            pollOptions.value = ['', ''];
            showPoll.value = false;
        }
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("section", _hoisted_1$1, [
                createBaseVNode("header", _hoisted_2$1, [
                    createBaseVNode("div", null, [
                        _cache[6] || (_cache[6] = createBaseVNode("span", null, "Chat", -1)),
                        createBaseVNode("h2", null, toDisplayString(__props.group?.name ?? 'Chọn nhóm chat'), 1)
                    ]),
                    (pinnedMessages.value.length)
                        ? (openBlock(), createElementBlock("div", _hoisted_3$1, [
                            createVNode(unref(Pin), { size: 14 }),
                            createBaseVNode("span", null, toDisplayString(pinnedMessages.value.length) + " pinned", 1)
                        ]))
                        : createCommentVNode("", true)
                ]),
                (pinnedMessages.value.length)
                    ? (openBlock(), createElementBlock("div", _hoisted_4, [
                        (openBlock(true), createElementBlock(Fragment, null, renderList(pinnedMessages.value, (message) => {
                            return (openBlock(), createElementBlock("span", {
                                key: message.id
                            }, toDisplayString(message.text || message.poll?.question), 1));
                        }), 128))
                    ]))
                    : createCommentVNode("", true),
                createBaseVNode("div", {
                    ref_key: "bodyRef",
                    ref: bodyRef,
                    class: "team-chat-body no-scrollbar"
                }, [
                    (openBlock(true), createElementBlock(Fragment, null, renderList(__props.messages, (message) => {
                        return (openBlock(), createBlock(_sfc_main$2, {
                            key: message.id,
                            message: message,
                            "current-user-id": __props.currentUserId,
                            onPin: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('pin', $event)))
                        }, null, 8, ["message", "current-user-id"]));
                    }), 128))
                ], 512),
                (showPoll.value)
                    ? (openBlock(), createElementBlock("div", _hoisted_5, [
                        withDirectives(createBaseVNode("input", {
                            "onUpdate:modelValue": _cache[1] || (_cache[1] = ($event) => ((pollQuestion).value = $event)),
                            type: "text",
                            placeholder: "Câu hỏi bình chọn"
                        }, null, 512), [
                            [vModelText, pollQuestion.value]
                        ]),
                        (openBlock(true), createElementBlock(Fragment, null, renderList(pollOptions.value, (_, index) => {
                            return withDirectives((openBlock(), createElementBlock("input", {
                                key: index,
                                "onUpdate:modelValue": ($event) => ((pollOptions.value[index]) = $event),
                                type: "text",
                                placeholder: `Lựa chọn ${index + 1}`
                            }, null, 8, _hoisted_6)), [
                                [vModelText, pollOptions.value[index]]
                            ]);
                        }), 128)),
                        createBaseVNode("button", {
                            class: "text-button",
                            type: "button",
                            onClick: _cache[2] || (_cache[2] = ($event) => (pollOptions.value.push('')))
                        }, "Thêm lựa chọn")
                    ]))
                    : createCommentVNode("", true),
                (pendingAttachments.value.length)
                    ? (openBlock(), createElementBlock("div", _hoisted_7, [
                        (openBlock(true), createElementBlock(Fragment, null, renderList(pendingAttachments.value, (file) => {
                            return (openBlock(), createElementBlock("span", {
                                key: file.name
                            }, toDisplayString(file.name), 1));
                        }), 128))
                    ]))
                    : createCommentVNode("", true),
                createBaseVNode("form", {
                    class: "team-chat-composer",
                    onSubmit: withModifiers(sendMessage, ["prevent"])
                }, [
                    createBaseVNode("label", _hoisted_8, [
                        createVNode(unref(FileUp), { size: 16 }),
                        createBaseVNode("input", {
                            type: "file",
                            multiple: "",
                            onChange: attachFiles
                        }, null, 32)
                    ]),
                    createBaseVNode("button", {
                        class: "icon-button icon-button--small",
                        type: "button",
                        "aria-label": "Emoji",
                        onClick: _cache[3] || (_cache[3] = ($event) => (showEmoji.value = !showEmoji.value))
                    }, [
                        createVNode(unref(SmilePlus), { size: 16 })
                    ]),
                    createBaseVNode("button", {
                        class: "icon-button icon-button--small",
                        type: "button",
                        "aria-label": "Tạo poll",
                        onClick: _cache[4] || (_cache[4] = ($event) => (showPoll.value = !showPoll.value))
                    }, [
                        createVNode(unref(Vote), { size: 16 })
                    ]),
                    withDirectives(createBaseVNode("input", {
                        "onUpdate:modelValue": _cache[5] || (_cache[5] = ($event) => ((draft).value = $event)),
                        type: "text",
                        placeholder: "Nhập tin nhắn..."
                    }, null, 512), [
                        [vModelText, draft.value]
                    ]),
                    createBaseVNode("button", _hoisted_9, [
                        createVNode(unref(Send), { size: 15 })
                    ])
                ], 32),
                (showEmoji.value)
                    ? (openBlock(), createElementBlock("div", _hoisted_10, [
                        (openBlock(), createElementBlock(Fragment, null, renderList(emojiOptions, (emoji) => {
                            return createBaseVNode("button", {
                                key: emoji,
                                type: "button",
                                onClick: ($event) => (addEmoji(emoji))
                            }, toDisplayString(emoji), 9, _hoisted_11);
                        }), 64))
                    ]))
                    : createCommentVNode("", true)
            ]));
        };
    }
});
const _hoisted_1 = { class: "dashboard-scroll dashboard-scroll--embedded no-scrollbar" };
const _hoisted_2 = { class: "dashboard-main project-home-main no-scrollbar" };
const _hoisted_3 = { class: "team-chat-page" };
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'TeamsPage',
    setup(__props) {
        const { currentUser } = useDashboardContext();
        const groups = ref([
            { id: 'core', name: 'Qaly Core', description: 'Điều phối triển khai hằng ngày', unreadCount: 2 },
            { id: 'design', name: 'Design System', description: 'UI, component và guideline', unreadCount: 0 },
            { id: 'release', name: 'Release Room', description: 'Checklist trước khi phát hành', unreadCount: 1 },
        ]);
        const activeGroupId = ref(groups.value[0]?.id ?? '');
        const messages = ref([
            {
                id: 'm1',
                groupId: 'core',
                senderId: 'admin',
                senderName: 'Quản trị viên',
                senderInitials: 'QT',
                text: 'Mọi người cập nhật tiến độ API dashboard trước 16:00 nhé.',
                createdAt: '09:12',
                pinned: true,
                attachments: [],
            },
            {
                id: 'm2',
                groupId: 'core',
                senderId: 'me',
                senderName: 'Bạn',
                senderInitials: 'BU',
                text: 'Đã xong phần routing, đang kiểm tra responsive.',
                createdAt: '09:24',
                pinned: false,
                attachments: [],
            },
            {
                id: 'm3',
                groupId: 'design',
                senderId: 'design',
                senderName: 'Trần Thị B',
                senderInitials: 'TB',
                text: 'Poll nhanh cho layout project card.',
                createdAt: '10:05',
                pinned: false,
                attachments: [],
                poll: {
                    question: 'Dùng card hay table cho danh sách dự án?',
                    options: ['Card', 'Table', 'Hybrid'],
                },
            },
        ]);
        const currentUserId = computed(() => currentUser.value?.id ?? 'me');
        const activeGroup = computed(() => groups.value.find((group) => group.id === activeGroupId.value) ?? null);
        const activeMessages = computed(() => messages.value.filter((message) => message.groupId === activeGroupId.value));
        function createGroup() {
            const name = window.prompt('Tên nhóm chat mới');
            if (!name?.trim())
                return;
            const group = {
                id: `group-${Date.now()}`,
                name: name.trim(),
                description: 'Nhóm chat mới',
                unreadCount: 0,
            };
            groups.value = [group, ...groups.value];
            activeGroupId.value = group.id;
        }
        function sendMessage(payload) {
            if (!activeGroupId.value)
                return;
            messages.value.push({
                id: `message-${Date.now()}`,
                groupId: activeGroupId.value,
                senderId: currentUserId.value,
                senderName: currentUser.value?.fullName ?? 'Bạn',
                senderInitials: (currentUser.value?.fullName ?? 'Bạn')
                    .split(' ')
                    .filter(Boolean)
                    .slice(0, 2)
                    .map((part) => part[0]?.toUpperCase() ?? '')
                    .join(''),
                text: payload.text,
                createdAt: new Intl.DateTimeFormat('vi', { hour: '2-digit', minute: '2-digit' }).format(new Date()),
                pinned: false,
                attachments: payload.attachments,
                poll: payload.poll,
            });
        }
        function togglePin(messageId) {
            messages.value = messages.value.map((message) => message.id === messageId ? { ...message, pinned: !message.pinned } : message);
        }
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                createBaseVNode("div", _hoisted_2, [
                    createBaseVNode("section", _hoisted_3, [
                        createVNode(_sfc_main$3, {
                            groups: groups.value,
                            "active-group-id": activeGroupId.value,
                            onSelect: _cache[0] || (_cache[0] = ($event) => (activeGroupId.value = $event)),
                            onCreate: createGroup
                        }, null, 8, ["groups", "active-group-id"]),
                        createVNode(_sfc_main$1, {
                            group: activeGroup.value,
                            messages: activeMessages.value,
                            "current-user-id": currentUserId.value,
                            onSend: sendMessage,
                            onPin: togglePin
                        }, null, 8, ["group", "messages", "current-user-id"])
                    ])
                ])
            ]));
        };
    }
});
export { _sfc_main as default };
//# sourceMappingURL=TeamsPage.js.map
