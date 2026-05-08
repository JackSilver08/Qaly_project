const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["assets/ArchivedProjectsPage.js","assets/ProjectList.vue_vue_type_script_setup_true_lang.js","assets/vendor-icons.js","assets/vendor-vue.js","assets/vendor-markdown.js","assets/vendor-realtime.js","assets/DashboardPage.js","assets/ProjectToolbar.vue_vue_type_script_setup_true_lang.js","assets/ProfilePage.js","assets/ProjectDetailPage.js","assets/ProjectDetailPage.css","assets/ProjectsPage.js","assets/TasksPage.js","assets/TeamsPage.js"])))=>i.map(i=>d[i]);
import { d as defineComponent, u as useRoute, r as resolveComponent, o as openBlock, c as createElementBlock, a as createBaseVNode, F as Fragment, b as renderList, e as createBlock, w as withCtx, n as normalizeClass, f as resolveDynamicComponent, t as toDisplayString, g as createVNode, i as unref, j as onMounted, k as onBeforeUnmount, l as createCommentVNode, m as ref, p as renderSlot, q as inject, s as readonly, v as watch, x as createTextVNode, y as withModifiers, z as withDirectives, A as vModelText, T as Transition, B as computed, C as nextTick, D as useRouter, E as provide, G as normalizeStyle, H as TransitionGroup, I as Teleport, J as createRouter, K as createWebHistory, L as createApp } from './vendor-vue.js';
import { B as Box, M as Menu, a as Bell, C as ChevronDown, U as User, L as LogOut, X, S as Sparkles, b as Send, c as LayoutDashboard, F as FolderKanban, d as ClipboardList, e as Users, I as Info, T as TriangleAlert, f as CircleX, g as CircleCheck } from './vendor-icons.js';
import { M as MarkdownIt, p as purify } from './vendor-markdown.js';
import { H as HubConnectionBuilder } from './vendor-realtime.js';
const _hoisted_1$5 = { class: "shell-sidebar no-scrollbar" };
const _hoisted_2$5 = {
    class: "shell-nav",
    "aria-label": "Main navigation"
};
const _hoisted_3$4 = ["href", "onClick"];
const _hoisted_4$4 = ["href", "onClick"];
const _sfc_main$6 = /*@__PURE__*/ defineComponent({
    __name: 'SidebarNav',
    props: {
        items: {}
    },
    emits: ["navigate"],
    setup(__props, { emit: __emit }) {
        const emit = __emit;
        const route = useRoute();
        function handleNavigate(event, navigate) {
            void navigate(event);
            emit('navigate');
        }
        function isItemActive(item, isActive, isExactActive) {
            if (item.to === '/dashboard')
                return isExactActive;
            if (item.to === '/projects')
                return isActive && !route.path.startsWith('/projects/archived');
            return isActive;
        }
        return (_ctx, _cache) => {
            const _component_RouterLink = resolveComponent("RouterLink");
            return (openBlock(), createElementBlock("aside", _hoisted_1$5, [
                createBaseVNode("nav", _hoisted_2$5, [
                    (openBlock(true), createElementBlock(Fragment, null, renderList(__props.items, (item) => {
                        return (openBlock(), createBlock(_component_RouterLink, {
                            key: item.to,
                            to: item.to,
                            custom: ""
                        }, {
                            default: withCtx(({ href, navigate, isActive, isExactActive }) => [
                                createBaseVNode("a", {
                                    href: href,
                                    class: normalizeClass(["shell-nav-item", { 'is-active': isItemActive(item, isActive, isExactActive) }]),
                                    onClick: ($event) => (handleNavigate($event, navigate))
                                }, [
                                    (openBlock(), createBlock(resolveDynamicComponent(item.icon), { size: 20 })),
                                    createBaseVNode("span", null, toDisplayString(item.label), 1)
                                ], 10, _hoisted_3$4)
                            ]),
                            _: 2
                        }, 1032, ["to"]));
                    }), 128))
                ]),
                createVNode(_component_RouterLink, {
                    to: "/projects/archived",
                    custom: ""
                }, {
                    default: withCtx(({ href, navigate, isExactActive }) => [
                        createBaseVNode("a", {
                            href: href,
                            class: normalizeClass(["shell-archive-button", { 'is-active': isExactActive }]),
                            onClick: ($event) => (handleNavigate($event, navigate))
                        }, [
                            createVNode(unref(Box), { size: 19 }),
                            _cache[0] || (_cache[0] = createBaseVNode("span", null, "Dự án đã lưu trữ", -1))
                        ], 10, _hoisted_4$4)
                    ]),
                    _: 1
                })
            ]));
        };
    }
});
const erumiRobotUrl = '/images/erumi-chatbot.png';
const _sfc_main$5 = /*@__PURE__*/ defineComponent({
    __name: 'ChatbotAvatar',
    props: {
        size: { default: 'small' }
    },
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("span", {
                class: normalizeClass(["chatbot-avatar", `chatbot-avatar--${__props.size}`])
            }, [
                createBaseVNode("img", {
                    src: erumiRobotUrl,
                    alt: "Erumi chatbot"
                })
            ], 2));
        };
    }
});
const _hoisted_1$4 = { class: "shell-header" };
const _hoisted_2$4 = { class: "shell-brand" };
const _hoisted_3$3 = { class: "shell-header-actions" };
const _hoisted_4$3 = {
    key: 0,
    class: "shell-action-badge"
};
const _hoisted_5$2 = ["aria-expanded"];
const _hoisted_6$1 = {
    key: 0,
    class: "shell-user-dropdown-menu",
    role: "menu"
};
const _sfc_main$4 = /*@__PURE__*/ defineComponent({
    __name: 'TopHeader',
    props: {
        brandName: {},
        notificationCount: {},
        userInitials: {},
        userName: {}
    },
    emits: ["toggleSidebar", "notifications", "assistant", "logout"],
    setup(__props, { emit: __emit }) {
        const emit = __emit;
        const userMenuOpen = ref(false);
        const userMenuRef = ref(null);
        function closeUserMenu() {
            userMenuOpen.value = false;
        }
        function toggleUserMenu() {
            userMenuOpen.value = !userMenuOpen.value;
        }
        function handleLogout() {
            closeUserMenu();
            emit('logout');
        }
        function handleDocumentPointerDown(event) {
            const target = event.target;
            if (target && userMenuRef.value?.contains(target))
                return;
            closeUserMenu();
        }
        function handleDocumentKeydown(event) {
            if (event.key === 'Escape') {
                closeUserMenu();
            }
        }
        onMounted(() => {
            document.addEventListener('pointerdown', handleDocumentPointerDown);
            document.addEventListener('keydown', handleDocumentKeydown);
        });
        onBeforeUnmount(() => {
            document.removeEventListener('pointerdown', handleDocumentPointerDown);
            document.removeEventListener('keydown', handleDocumentKeydown);
        });
        return (_ctx, _cache) => {
            const _component_RouterLink = resolveComponent("RouterLink");
            return (openBlock(), createElementBlock("header", _hoisted_1$4, [
                createBaseVNode("div", _hoisted_2$4, [
                    createBaseVNode("button", {
                        class: "shell-menu-button",
                        type: "button",
                        "aria-label": "Mo menu",
                        onClick: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('toggleSidebar')))
                    }, [
                        createVNode(unref(Menu), { size: 20 })
                    ]),
                    createVNode(_component_RouterLink, {
                        class: "shell-brand-link",
                        to: "/dashboard",
                        "aria-label": "QALY trang chu"
                    }, {
                        default: withCtx(() => [
                            _cache[3] || (_cache[3] = createBaseVNode("div", {
                                class: "shell-brand-mark",
                                "aria-hidden": "true"
                            }, "Q", -1)),
                            createBaseVNode("strong", null, toDisplayString(__props.brandName), 1)
                        ]),
                        _: 1
                    })
                ]),
                createBaseVNode("div", _hoisted_3$3, [
                    createBaseVNode("button", {
                        class: "shell-icon-button",
                        type: "button",
                        "aria-label": "Thong bao",
                        onClick: _cache[1] || (_cache[1] = ($event) => (_ctx.$emit('notifications')))
                    }, [
                        createVNode(unref(Bell), { size: 18 }),
                        (__props.notificationCount > 0)
                            ? (openBlock(), createElementBlock("span", _hoisted_4$3, toDisplayString(__props.notificationCount), 1))
                            : createCommentVNode("", true)
                    ]),
                    createBaseVNode("button", {
                        class: "shell-icon-button",
                        type: "button",
                        "aria-label": "Tro ly Qaly",
                        onClick: _cache[2] || (_cache[2] = ($event) => (_ctx.$emit('assistant')))
                    }, [
                        createVNode(_sfc_main$5, { size: "launcher" })
                    ]),
                    createBaseVNode("div", {
                        ref_key: "userMenuRef",
                        ref: userMenuRef,
                        class: "shell-user-dropdown"
                    }, [
                        createBaseVNode("button", {
                            class: normalizeClass(["shell-user-menu", { 'is-open': userMenuOpen.value }]),
                            type: "button",
                            "aria-haspopup": "menu",
                            "aria-expanded": userMenuOpen.value,
                            onClick: toggleUserMenu
                        }, [
                            createBaseVNode("span", null, toDisplayString(__props.userInitials), 1),
                            createBaseVNode("strong", null, toDisplayString(__props.userName), 1),
                            createVNode(unref(ChevronDown), { size: 16 })
                        ], 10, _hoisted_5$2),
                        (userMenuOpen.value)
                            ? (openBlock(), createElementBlock("div", _hoisted_6$1, [
                                createVNode(_component_RouterLink, {
                                    class: "shell-user-dropdown-item",
                                    to: "/profile",
                                    role: "menuitem",
                                    onClick: closeUserMenu
                                }, {
                                    default: withCtx(() => [
                                        createVNode(unref(User), { size: 17 }),
                                        _cache[4] || (_cache[4] = createBaseVNode("span", null, "Trang cá nhân", -1))
                                    ]),
                                    _: 1
                                }),
                                createBaseVNode("button", {
                                    class: "shell-user-dropdown-item shell-user-dropdown-item--danger",
                                    type: "button",
                                    role: "menuitem",
                                    onClick: handleLogout
                                }, [
                                    createVNode(unref(LogOut), { size: 17 }),
                                    _cache[5] || (_cache[5] = createBaseVNode("span", null, "Đăng xuất", -1))
                                ])
                            ]))
                            : createCommentVNode("", true)
                    ], 512)
                ])
            ]));
        };
    }
});
const _hoisted_1$3 = { class: "app-shell" };
const _hoisted_2$3 = { class: "shell-main no-scrollbar" };
const _sfc_main$3 = /*@__PURE__*/ defineComponent({
    __name: 'AppShell',
    props: {
        navItems: {},
        notificationCount: {},
        userName: {},
        userInitials: {}
    },
    emits: ["navigate", "notifications", "assistant", "logout"],
    setup(__props, { emit: __emit }) {
        const emit = __emit;
        const sidebarOpen = ref(false);
        function handleNavigate() {
            sidebarOpen.value = false;
            emit('navigate');
        }
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1$3, [
                createVNode(_sfc_main$4, {
                    "brand-name": "QALY",
                    "notification-count": __props.notificationCount,
                    "user-name": __props.userName,
                    "user-initials": __props.userInitials,
                    onToggleSidebar: _cache[0] || (_cache[0] = ($event) => (sidebarOpen.value = true)),
                    onNotifications: _cache[1] || (_cache[1] = ($event) => (_ctx.$emit('notifications'))),
                    onAssistant: _cache[2] || (_cache[2] = ($event) => (_ctx.$emit('assistant'))),
                    onLogout: _cache[3] || (_cache[3] = ($event) => (_ctx.$emit('logout')))
                }, null, 8, ["notification-count", "user-name", "user-initials"]),
                createVNode(_sfc_main$6, {
                    class: normalizeClass({ 'is-open': sidebarOpen.value }),
                    items: __props.navItems,
                    onNavigate: handleNavigate
                }, null, 8, ["class", "items"]),
                (sidebarOpen.value)
                    ? (openBlock(), createElementBlock("div", {
                        key: 0,
                        class: "shell-backdrop",
                        onClick: _cache[4] || (_cache[4] = ($event) => (sidebarOpen.value = false))
                    }))
                    : createCommentVNode("", true),
                createBaseVNode("main", _hoisted_2$3, [
                    renderSlot(_ctx.$slots, "default")
                ]),
                renderSlot(_ctx.$slots, "overlays")
            ]));
        };
    }
});
const dashboardContextKey = Symbol('dashboard-context');
function useDashboardContext() {
    const context = inject(dashboardContextKey);
    if (!context) {
        throw new Error('Dashboard context is not available.');
    }
    return context;
}
const DEFAULT_DURATION = 4600;
const MAX_TOASTS = 5;
const toasts = ref([]);
const timers = new Map();
let toastSequence = 0;
function clearToastTimer(id) {
    const timer = timers.get(id);
    if (timer !== undefined) {
        if (typeof window !== 'undefined')
            window.clearTimeout(timer);
        timers.delete(id);
    }
}
function dismissToast(id) {
    clearToastTimer(id);
    toasts.value = toasts.value.filter((toast) => toast.id !== id);
}
function showToast(input) {
    const message = input.message.trim();
    if (!message)
        return '';
    const id = `toast-${Date.now()}-${toastSequence++}`;
    const toast = {
        id,
        type: input.type ?? 'info',
        message,
        title: input.title,
        duration: input.duration ?? DEFAULT_DURATION,
    };
    toasts.value = [toast, ...toasts.value];
    if (toasts.value.length > MAX_TOASTS) {
        const overflow = toasts.value.slice(MAX_TOASTS);
        overflow.forEach((item) => clearToastTimer(item.id));
        toasts.value = toasts.value.slice(0, MAX_TOASTS);
    }
    if (typeof window !== 'undefined') {
        timers.set(id, window.setTimeout(() => dismissToast(id), toast.duration));
    }
    return id;
}
function showSuccess(message, options = {}) {
    return showToast({ ...options, type: 'success', message });
}
function showError(message, options = {}) {
    return showToast({ ...options, type: 'error', message });
}
function showWarning(message, options = {}) {
    return showToast({ ...options, type: 'warning', message });
}
function showInfo(message, options = {}) {
    return showToast({ ...options, type: 'info', message });
}
function useToast() {
    return {
        toasts: readonly(toasts),
        showToast,
        showSuccess,
        showError,
        showWarning,
        showInfo,
        dismissToast,
    };
}
const _hoisted_1$2 = { class: "floating-erumi" };
const _hoisted_2$2 = {
    key: 2,
    class: "launcher-badge"
};
const _hoisted_3$2 = {
    key: 0,
    class: "erumi-window glass-card shadow-2xl"
};
const _hoisted_4$2 = { class: "erumi-header" };
const _hoisted_5$1 = { class: "erumi-identity" };
const _hoisted_6 = { class: "header-actions" };
const _hoisted_7 = {
    key: 0,
    class: "msg-avatar"
};
const _hoisted_8 = ["innerHTML"];
const _hoisted_9 = {
    key: 1,
    class: "msg-avatar user-icon"
};
const _hoisted_10 = {
    key: 0,
    class: "msg-row msg-assistant"
};
const _hoisted_11 = { class: "msg-avatar" };
const _hoisted_12 = { class: "erumi-footer" };
const _hoisted_13 = { class: "quick-prompts no-scrollbar" };
const _hoisted_14 = ["onClick"];
const _hoisted_15 = {
    key: 0,
    class: "mention-suggestions"
};
const _hoisted_16 = ["onClick"];
const _hoisted_17 = ["disabled"];
const _hoisted_18 = ["disabled"];
const _sfc_main$2 = /*@__PURE__*/ defineComponent({
    __name: 'FloatingChatbot',
    setup(__props) {
        const { projects, selectedProject, currentUser } = useDashboardContext();
        const isOpen = ref(false);
        const isThinking = ref(false);
        const draft = ref('');
        const messages = ref([
            {
                id: 'welcome',
                role: 'assistant',
                text: 'Chào bạn! Mình là Erumi, trợ lý AI của Qaly. Bạn cần mình giúp gì hôm nay?'
            }
        ]);
        const bodyRef = ref(null);
        const showSuggestions = ref(false);
        const markdown = new MarkdownIt({
            html: false,
            linkify: true,
            typographer: true
        });
        function renderMarkdown(content) {
            return purify.sanitize(markdown.render(content));
        }
        const suggestions = computed(() => {
            const parts = draft.value.split(' ');
            const lastPart = parts[parts.length - 1];
            if (lastPart.startsWith('@')) {
                const query = lastPart.slice(1).toLowerCase();
                return projects.value.filter((p) => p.name.toLowerCase().includes(query));
            }
            return [];
        });
        watch(draft, (val) => {
            const parts = val.split(' ');
            const lastPart = parts[parts.length - 1];
            showSuggestions.value = lastPart.startsWith('@');
        });
        function tagProject(project) {
            const parts = draft.value.split(' ');
            parts[parts.length - 1] = `@${project.name} `;
            draft.value = parts.join(' ');
            showSuggestions.value = false;
        }
        async function submitChat(explicit) {
            const prompt = (explicit ?? draft.value).trim();
            if (!prompt || isThinking.value)
                return;
            messages.value.push({ id: `u-${Date.now()}`, role: 'user', text: prompt });
            draft.value = '';
            isThinking.value = true;
            await scrollBottom();
            let taggedProjectId = null;
            const tagMatch = prompt.match(/@([\w\s]+)/);
            if (tagMatch) {
                const name = tagMatch[1].trim().toLowerCase();
                const p = projects.value.find((x) => x.name.toLowerCase() === name);
                if (p)
                    taggedProjectId = p.id;
            }
            try {
                const assistantId = `a-${Date.now()}`;
                messages.value.push({ id: assistantId, role: 'assistant', text: '' });
                const response = await fetch('/api/ai/chat/stream', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        message: prompt,
                        projectId: taggedProjectId ?? selectedProject.value?.id ?? null
                    })
                });
                if (!response.ok)
                    throw new Error('Streaming failed');
                const reader = response.body?.getReader();
                const decoder = new TextDecoder();
                let fullText = '';
                if (reader) {
                    isThinking.value = false;
                    while (true) {
                        const { done, value } = await reader.read();
                        if (done)
                            break;
                        fullText += decoder.decode(value, { stream: true });
                        const idx = messages.value.findIndex(m => m.id === assistantId);
                        if (idx !== -1)
                            messages.value[idx].text = fullText;
                        void scrollBottom();
                    }
                }
            }
            catch (e) {
                messages.value.push({ id: `err-${Date.now()}`, role: 'assistant', text: 'Xin lỗi, Erumi đang gặp chút trục trặc. Thử lại sau nhé!' });
                showError('Không thể gửi yêu cầu tới trợ lý AI');
            }
            finally {
                isThinking.value = false;
            }
        }
        async function scrollBottom() {
            await nextTick();
            if (bodyRef.value) {
                bodyRef.value.scrollTo({ top: bodyRef.value.scrollHeight, behavior: 'smooth' });
            }
        }
        function toggle() {
            isOpen.value = !isOpen.value;
            if (isOpen.value)
                void scrollBottom();
        }
        function initials(name) {
            return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
        }
        const quickPrompts = [
            'Tóm tắt dự án hiện tại',
            'Có task nào quá hạn không?',
            'Ai đang rảnh để nhận việc?'
        ];
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1$2, [
                createBaseVNode("button", {
                    class: normalizeClass(["erumi-launcher shadow-lg", { 'is-active': isOpen.value }]),
                    onClick: toggle,
                    "aria-label": "Toggle AI Assistant"
                }, [
                    (!isOpen.value)
                        ? (openBlock(), createBlock(_sfc_main$5, {
                            key: 0,
                            size: "medium"
                        }))
                        : (openBlock(), createBlock(unref(X), {
                            key: 1,
                            size: 24
                        })),
                    (!isOpen.value)
                        ? (openBlock(), createElementBlock("span", _hoisted_2$2))
                        : createCommentVNode("", true)
                ], 2),
                createVNode(Transition, { name: "fade-up" }, {
                    default: withCtx(() => [
                        (isOpen.value)
                            ? (openBlock(), createElementBlock("div", _hoisted_3$2, [
                                createBaseVNode("header", _hoisted_4$2, [
                                    createBaseVNode("div", _hoisted_5$1, [
                                        createVNode(_sfc_main$5, { size: "small" }),
                                        _cache[2] || (_cache[2] = createBaseVNode("div", null, [
                                            createBaseVNode("h3", null, "Erumi Agent"),
                                            createBaseVNode("div", { class: "status-indicator" }, [
                                                createBaseVNode("span", { class: "pulse" }),
                                                createTextVNode(" Trực tuyến ")
                                            ])
                                        ], -1))
                                    ]),
                                    createBaseVNode("div", _hoisted_6, [
                                        createBaseVNode("button", {
                                            class: "icon-btn",
                                            onClick: toggle
                                        }, [
                                            createVNode(unref(X), { size: 18 })
                                        ])
                                    ])
                                ]),
                                createBaseVNode("div", {
                                    ref_key: "bodyRef",
                                    ref: bodyRef,
                                    class: "erumi-body no-scrollbar"
                                }, [
                                    (openBlock(true), createElementBlock(Fragment, null, renderList(messages.value, (m) => {
                                        return (openBlock(), createElementBlock("div", {
                                            key: m.id,
                                            class: normalizeClass(['msg-row', `msg-${m.role}`])
                                        }, [
                                            (m.role === 'assistant')
                                                ? (openBlock(), createElementBlock("div", _hoisted_7, [
                                                    createVNode(_sfc_main$5, { size: "small" })
                                                ]))
                                                : createCommentVNode("", true),
                                            createBaseVNode("div", {
                                                class: "msg-bubble",
                                                innerHTML: renderMarkdown(m.text)
                                            }, null, 8, _hoisted_8),
                                            (m.role === 'user')
                                                ? (openBlock(), createElementBlock("div", _hoisted_9, toDisplayString(unref(currentUser) ? initials(unref(currentUser).fullName) : 'U'), 1))
                                                : createCommentVNode("", true)
                                        ], 2));
                                    }), 128)),
                                    (isThinking.value)
                                        ? (openBlock(), createElementBlock("div", _hoisted_10, [
                                            createBaseVNode("div", _hoisted_11, [
                                                createVNode(_sfc_main$5, { size: "small" })
                                            ]),
                                            _cache[3] || (_cache[3] = createBaseVNode("div", { class: "msg-bubble thinking-dots" }, [
                                                createBaseVNode("span"),
                                                createBaseVNode("span"),
                                                createBaseVNode("span")
                                            ], -1))
                                        ]))
                                        : createCommentVNode("", true)
                                ], 512),
                                createBaseVNode("div", _hoisted_12, [
                                    createBaseVNode("div", _hoisted_13, [
                                        (openBlock(), createElementBlock(Fragment, null, renderList(quickPrompts, (p) => {
                                            return createBaseVNode("button", {
                                                key: p,
                                                class: "prompt-btn",
                                                onClick: ($event) => (submitChat(p))
                                            }, [
                                                createVNode(unref(Sparkles), { size: 12 }),
                                                createTextVNode(" " + toDisplayString(p), 1)
                                            ], 8, _hoisted_14);
                                        }), 64))
                                    ]),
                                    (showSuggestions.value && suggestions.value.length)
                                        ? (openBlock(), createElementBlock("div", _hoisted_15, [
                                            (openBlock(true), createElementBlock(Fragment, null, renderList(suggestions.value, (s) => {
                                                return (openBlock(), createElementBlock("button", {
                                                    key: s.id,
                                                    onClick: ($event) => (tagProject(s))
                                                }, " @" + toDisplayString(s.name), 9, _hoisted_16));
                                            }), 128))
                                        ]))
                                        : createCommentVNode("", true),
                                    createBaseVNode("form", {
                                        class: "composer",
                                        onSubmit: _cache[1] || (_cache[1] = withModifiers(($event) => (submitChat()), ["prevent"]))
                                    }, [
                                        withDirectives(createBaseVNode("input", {
                                            "onUpdate:modelValue": _cache[0] || (_cache[0] = ($event) => ((draft).value = $event)),
                                            placeholder: "Hỏi Erumi... (Dùng @ để tag dự án)",
                                            disabled: isThinking.value,
                                            ref: "inputRef"
                                        }, null, 8, _hoisted_17), [
                                            [vModelText, draft.value]
                                        ]),
                                        createBaseVNode("button", {
                                            type: "submit",
                                            disabled: !draft.value.trim() || isThinking.value,
                                            class: "send-btn"
                                        }, [
                                            createVNode(unref(Send), { size: 18 })
                                        ], 8, _hoisted_18)
                                    ], 32)
                                ])
                            ]))
                            : createCommentVNode("", true)
                    ]),
                    _: 1
                })
            ]));
        };
    }
});
const _export_sfc = (sfc, props) => {
    const target = sfc.__vccOpts || sfc;
    for (const [key, val] of props) {
        target[key] = val;
    }
    return target;
};
const FloatingChatbot = /*#__PURE__*/ _export_sfc(_sfc_main$2, [['__scopeId', "data-v-1b8183d1"]]);
const fallbackDashboard = {
    generatedAt: '2026-05-01T16:20:00Z',
    stats: {
        activeProjects: 3,
        totalTasks: 18,
        overdueTasks: 2,
        teamMembers: 7,
        completedTasks: 7,
        completionRate: 39,
        tasksAtRisk: 5,
    },
    summary: 'Qaly đang điều phối nhiều luồng triển khai cho MVP, xác thực và hệ thống thiết kế. Không gian làm việc được tối ưu cho lập kế hoạch nhanh, hiển thị rủi ro rõ ràng và hỗ trợ quyết định bằng AI.',
    riskDigest: 'Các điểm áp lực chính là công việc triển khai quá hạn, tải công việc chưa đều trong đội nòng cốt và một nhóm nhỏ việc ưu tiên cao vẫn chờ người phụ trách rõ ràng.',
    projects: [
        {
            id: '3f9f7728-40d6-4fd3-bfef-90f7fdad7001',
            name: 'Qaly MVP',
            description: 'Không gian quản lý công việc nội bộ cho triển khai chu kỳ làm việc, lập kế hoạch đội ngũ và hỗ trợ quyết định bằng AI.',
            status: 'Active',
            ownerId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3001',
            ownerName: 'Quản trị viên',
            memberCount: 3,
            taskCount: 8,
            completedTaskCount: 3,
            overdueTaskCount: 1,
            progressPercentage: 38,
            members: [
                {
                    userId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3001',
                    fullName: 'Quản trị viên',
                    role: 'Owner',
                    email: 'admin@qaly.dev',
                },
                {
                    userId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3002',
                    fullName: 'Nguyễn Văn A',
                    role: 'Member',
                    email: 'nguyenvana@qaly.dev',
                },
                {
                    userId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3003',
                    fullName: 'Trần Thị B',
                    role: 'Member',
                    email: 'tranthib@qaly.dev',
                },
            ],
            createdAt: '2026-04-20T09:00:00Z',
            endDate: '2026-07-31T17:00:00Z',
            tasks: [
                {
                    id: '0ca40d7d-93aa-42d0-b9f6-2d2d9a5f2001',
                    title: 'Thiết kế khung làm việc',
                    status: 'Done',
                    priority: 'High',
                    dueDate: '2026-04-24T17:00:00Z',
                    assigneeName: 'Quản trị viên',
                    reporterName: 'Quản trị viên',
                    projectName: 'Qaly MVP',
                    isPrivate: false,
                    commentCount: 3,
                    attachmentCount: 1,
                },
                {
                    id: '0ca40d7d-93aa-42d0-b9f6-2d2d9a5f2002',
                    title: 'Triển khai API chỉ số bảng điều khiển',
                    status: 'InProgress',
                    priority: 'High',
                    dueDate: '2026-05-03T17:00:00Z',
                    assigneeName: 'Nguyễn Văn A',
                    reporterName: 'Quản trị viên',
                    projectName: 'Qaly MVP',
                    isPrivate: false,
                    commentCount: 5,
                    attachmentCount: 0,
                },
                {
                    id: '0ca40d7d-93aa-42d0-b9f6-2d2d9a5f2003',
                    title: 'Tạo trạng thái tương tác Kanban',
                    status: 'Todo',
                    priority: 'Medium',
                    dueDate: '2026-05-07T17:00:00Z',
                    assigneeName: 'Trần Thị B',
                    reporterName: 'Quản trị viên',
                    projectName: 'Qaly MVP',
                    isPrivate: false,
                    commentCount: 2,
                    attachmentCount: 0,
                },
                {
                    id: '0ca40d7d-93aa-42d0-b9f6-2d2d9a5f2004',
                    title: 'Tích hợp bề mặt gợi ý AI',
                    status: 'Todo',
                    priority: 'High',
                    dueDate: '2026-05-05T17:00:00Z',
                    assigneeName: null,
                    reporterName: 'Quản trị viên',
                    projectName: 'Qaly MVP',
                    isPrivate: false,
                    commentCount: 1,
                    attachmentCount: 0,
                },
            ],
        },
        {
            id: '3f9f7728-40d6-4fd3-bfef-90f7fdad7002',
            name: 'Gia cố định danh',
            description: 'Phân quyền theo vai trò, an toàn phiên đăng nhập và trải nghiệm đăng nhập/đăng ký.',
            status: 'InProgress',
            ownerId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3002',
            ownerName: 'Nguyễn Văn A',
            memberCount: 2,
            taskCount: 5,
            completedTaskCount: 2,
            overdueTaskCount: 1,
            progressPercentage: 40,
            members: [
                {
                    userId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3002',
                    fullName: 'Nguyễn Văn A',
                    role: 'Owner',
                    email: 'nguyenvana@qaly.dev',
                },
                {
                    userId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3003',
                    fullName: 'Trần Thị B',
                    role: 'Member',
                    email: 'tranthib@qaly.dev',
                },
            ],
            createdAt: '2026-04-18T09:00:00Z',
            endDate: '2026-06-14T17:00:00Z',
            tasks: [
                {
                    id: '0ca40d7d-93aa-42d0-b9f6-2d2d9a5f2010',
                    title: 'Xác thực trang đăng ký',
                    status: 'Done',
                    priority: 'Medium',
                    dueDate: '2026-04-26T17:00:00Z',
                    assigneeName: 'Nguyễn Văn A',
                    reporterName: 'Nguyễn Văn A',
                    projectName: 'Gia cố định danh',
                    isPrivate: false,
                    commentCount: 1,
                    attachmentCount: 0,
                },
                {
                    id: '0ca40d7d-93aa-42d0-b9f6-2d2d9a5f2011',
                    title: 'Triển khai middleware phân quyền',
                    status: 'InProgress',
                    priority: 'High',
                    dueDate: '2026-05-02T17:00:00Z',
                    assigneeName: 'Trần Thị B',
                    reporterName: 'Nguyễn Văn A',
                    projectName: 'Gia cố định danh',
                    isPrivate: false,
                    commentCount: 4,
                    attachmentCount: 0,
                },
            ],
        },
        {
            id: '3f9f7728-40d6-4fd3-bfef-90f7fdad7003',
            name: 'Ngôn ngữ thiết kế',
            description: 'Hoàn thiện hệ thống hình ảnh, kiểu chữ và các mẫu giao diện tái sử dụng cho quy trình quản trị.',
            status: 'Planned',
            ownerId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3003',
            ownerName: 'Trần Thị B',
            memberCount: 2,
            taskCount: 5,
            completedTaskCount: 2,
            overdueTaskCount: 0,
            progressPercentage: 40,
            members: [
                {
                    userId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3003',
                    fullName: 'Trần Thị B',
                    role: 'Owner',
                    email: 'tranthib@qaly.dev',
                },
                {
                    userId: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3001',
                    fullName: 'Quản trị viên',
                    role: 'Member',
                    email: 'admin@qaly.dev',
                },
            ],
            createdAt: '2026-04-15T09:00:00Z',
            endDate: '2026-06-28T17:00:00Z',
            tasks: [
                {
                    id: '0ca40d7d-93aa-42d0-b9f6-2d2d9a5f2020',
                    title: 'Xây dựng hệ màu',
                    status: 'Done',
                    priority: 'Medium',
                    dueDate: '2026-04-23T17:00:00Z',
                    assigneeName: 'Trần Thị B',
                    reporterName: 'Quản trị viên',
                    projectName: 'Ngôn ngữ thiết kế',
                    isPrivate: false,
                    commentCount: 2,
                    attachmentCount: 1,
                },
                {
                    id: '0ca40d7d-93aa-42d0-b9f6-2d2d9a5f2021',
                    title: 'Rà soát khung tương thích đa màn hình',
                    status: 'Todo',
                    priority: 'Medium',
                    dueDate: '2026-05-09T17:00:00Z',
                    assigneeName: 'Quản trị viên',
                    reporterName: 'Trần Thị B',
                    projectName: 'Ngôn ngữ thiết kế',
                    isPrivate: false,
                    commentCount: 0,
                    attachmentCount: 0,
                },
            ],
        },
    ],
    team: [
        {
            id: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3001',
            fullName: 'Quản trị viên',
            role: 'Admin',
            email: 'admin@qaly.dev',
            isActive: true,
            assignedTaskCount: 5,
            completedTaskCount: 2,
            inProgressTaskCount: 1,
            overdueTaskCount: 0,
            capacityPercent: 68,
            focusArea: 'Điều phối dự án',
        },
        {
            id: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3002',
            fullName: 'Nguyễn Văn A',
            role: 'Member',
            email: 'nguyenvana@qaly.dev',
            isActive: true,
            assignedTaskCount: 4,
            completedTaskCount: 1,
            inProgressTaskCount: 2,
            overdueTaskCount: 1,
            capacityPercent: 83,
            focusArea: 'Luồng triển khai',
        },
        {
            id: '9f8e7d6c-1a2b-4f3c-9c8b-7a6d5e4f3003',
            fullName: 'Trần Thị B',
            role: 'Member',
            email: 'tranthib@qaly.dev',
            isActive: true,
            assignedTaskCount: 3,
            completedTaskCount: 1,
            inProgressTaskCount: 1,
            overdueTaskCount: 1,
            capacityPercent: 72,
            focusArea: 'Hệ thống thiết kế',
        },
    ],
    notifications: [
        {
            id: 'note-1',
            title: 'Rủi ro tăng đột biến',
            message: 'Gia cố định danh có một công việc ưu tiên cao đến hạn trong 24 giờ.',
            tone: 'critical',
            createdAt: '2026-05-01T16:00:00Z',
        },
        {
            id: 'note-2',
            title: 'Tải công việc chưa cân bằng',
            message: 'Nguyễn Văn A đang nhận số công việc đang mở cao nhất trong chu kỳ làm việc này.',
            tone: 'warning',
            createdAt: '2026-05-01T15:20:00Z',
        },
        {
            id: 'note-3',
            title: 'Trợ lý AI đã sẵn sàng',
            message: 'Tóm tắt thông minh, quét rủi ro và gợi ý hội thoại đã sẵn sàng cho dự án đang chọn.',
            tone: 'info',
            createdAt: '2026-05-01T14:45:00Z',
        },
    ],
};
const _hoisted_1$1 = {
    key: 0,
    class: "notification-popover glass-card home-notification-popover"
};
const _hoisted_2$1 = { class: "panel-heading" };
const _hoisted_3$1 = { class: "popover-actions" };
const _hoisted_4$1 = { class: "notice__top" };
const _hoisted_5 = ["onClick"];
const _sfc_main$1 = /*@__PURE__*/ defineComponent({
    __name: 'App',
    setup(__props) {
        const navigation = [
            { label: 'Tổng quan', to: '/dashboard', icon: LayoutDashboard },
            { label: 'Dự án', to: '/projects', icon: FolderKanban },
            { label: 'Nhiệm vụ', to: '/tasks', icon: ClipboardList },
            { label: 'Nhóm', to: '/teams', icon: Users },
        ];
        const statusColumns = ['Todo', 'InProgress', 'InReview', 'Done'];
        const priorities = ['Low', 'Medium', 'High', 'Critical'];
        const dashboard = ref(fallbackDashboard);
        const currentUser = ref(null);
        const users = ref([]);
        const notifications = ref([]);
        const comments = ref([]);
        const attachments = ref([]);
        const wikiPages = ref([]);
        const timeEntries = ref([]);
        const activeTimer = ref(null);
        const isLoading = ref(true);
        const usingFallback = ref(true);
        const notificationsOpen = ref(false);
        const createProjectOpen = ref(false);
        const createTaskOpen = ref(false);
        const projectBeingEditedId = ref(null);
        const selectedTaskId = ref(null);
        const taskSearchQuery = ref('');
        const taskBeingQuickEditedId = ref(null);
        const activeTaskMenu = ref(null);
        function toggleTaskMenu(taskId) {
            activeTaskMenu.value = activeTaskMenu.value === taskId ? null : taskId;
        }
        const searchQuery = ref('');
        const projectFilter = ref('all');
        const projectSort = ref('recent');
        const activeProjectId = ref(null);
        const activeProjectTab = ref('stats');
        const projectName = ref('');
        const projectDescription = ref('');
        const projectEndDate = ref('');
        const tabs = [
            { id: 'stats', label: 'Thống kê' },
            { id: 'tasks', label: 'Task' },
            { id: 'members', label: 'Member' },
            { id: 'wiki', label: 'Wiki' },
        ];
        const editProjectName = ref('');
        const editProjectDescription = ref('');
        const newTaskTitle = ref('');
        const newTaskDescription = ref('');
        const newTaskPriority = ref('Medium');
        const newTaskAssigneeId = ref('');
        const newTaskDueDate = ref('');
        const newComment = ref('');
        let notificationConnectionStarted = false;
        const router = useRouter();
        const route = useRoute();
        const projects = computed(() => dashboard.value.projects);
        const team = computed(() => dashboard.value.team);
        const activeProjectsCount = computed(() => projects.value.filter((project) => project.status !== 'Archived').length);
        const totalTasks = computed(() => projects.value.reduce((sum, project) => sum + project.tasks.length, 0));
        const completedTasks = computed(() => projects.value.reduce((sum, project) => sum + project.tasks.filter((task) => task.status === 'Done').length, 0));
        const summaryCards = computed(() => [
            {
                key: 'projects',
                label: 'Projects',
                value: String(projects.value.length),
                detail: `${activeProjectsCount.value} active`,
                tone: 'blue',
            },
            {
                key: 'tasks',
                label: 'Tasks',
                value: String(totalTasks.value),
                detail: `${completedTasks.value} done`,
                tone: 'mint',
            },
            {
                key: 'team',
                label: 'Team',
                value: String(team.value.length),
                detail: 'workspace members',
                tone: 'violet',
            },
        ]);
        const filteredProjects = computed(() => {
            const query = searchQuery.value.trim().toLowerCase();
            return projects.value
                .filter((project) => {
                if (projectFilter.value === 'active' && !['Active', 'InProgress'].includes(project.status))
                    return false;
                if (projectFilter.value === 'planned' && project.status !== 'Planned')
                    return false;
                if (projectFilter.value === 'at-risk' && project.overdueTaskCount === 0)
                    return false;
                if (!query)
                    return true;
                return [project.name, project.description, project.ownerName, ...project.members.map((member) => member.fullName)]
                    .join(' ')
                    .toLowerCase()
                    .includes(query);
            })
                .sort((left, right) => {
                if (projectSort.value === 'name')
                    return left.name.localeCompare(right.name);
                if (projectSort.value === 'progress')
                    return right.progressPercentage - left.progressPercentage;
                if (projectSort.value === 'risk')
                    return right.overdueTaskCount - left.overdueTaskCount;
                return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime();
            });
        });
        function toProjectCard(project) {
            return {
                id: project.id,
                name: project.name,
                description: project.description || 'No description yet.',
                status: project.status,
                statusLabel: displayStatus(project.status),
                statusTone: statusTone(project.status),
                ownerId: project.ownerId,
                ownerName: project.ownerName,
                dueDateLabel: project.endDate ? `Due ${formatDate(project.endDate)}` : 'No due date',
                completedTaskCount: project.completedTaskCount,
                taskCount: project.taskCount,
                overdueTaskCount: project.overdueTaskCount,
                progressPercentage: project.progressPercentage,
                memberInitials: (project.members || []).slice(0, 4).map(m => initials(m.fullName)),
            };
        }
        const projectCards = computed(() => filteredProjects.value.map(toProjectCard));
        const activeProjectCards = computed(() => filteredProjects.value
            .filter((project) => project.status !== 'Archived')
            .map(toProjectCard));
        const archivedProjectCards = computed(() => filteredProjects.value
            .filter((project) => project.status === 'Archived')
            .map(toProjectCard));
        const assignedTaskCards = computed(() => projects.value.flatMap((project) => project.tasks
            .filter((task) => !currentUser.value || task.assigneeName === currentUser.value.fullName)
            .map((task) => ({
            id: task.id,
            projectId: project.id,
            title: task.title,
            projectName: project.name,
            assignedAtLabel: formatDate(project.createdAt),
            priority: task.priority,
            dueDateLabel: formatDate(task.dueDate),
            reporterName: task.reporterName,
            reporterInitials: initials(task.reporterName),
            statusLabel: displayStatus(task.status),
            isOverdue: isTaskOverdue(task),
        }))));
        const selectedProject = computed(() => {
            if (activeProjectId.value) {
                const active = projects.value.find((project) => project.id === activeProjectId.value);
                if (active)
                    return active;
            }
            return filteredProjects.value[0] ?? projects.value[0] ?? null;
        });
        const selectedProjectTasks = computed(() => selectedProject.value?.tasks ?? []);
        const selectedTask = computed(() => {
            if (!selectedProjectTasks.value.length)
                return null;
            return selectedProjectTasks.value.find((task) => task.id === selectedTaskId.value) ?? selectedProjectTasks.value[0];
        });
        const isProjectAdmin = computed(() => {
            const project = selectedProject.value;
            const user = currentUser.value;
            if (!project || !user)
                return false;
            const userRole = String(user.role || '').toLowerCase();
            if (userRole === 'admin' || user.email === 'admin@qaly.dev')
                return true;
            const userId = String(user.id || '').toLowerCase();
            if (project.ownerId?.toLowerCase() === userId)
                return true;
            const member = project.members?.find(m => String(m.userId || '').toLowerCase() === userId);
            if (member) {
                const memberRole = String(member.role || '').toLowerCase();
                if (memberRole === 'owner' || memberRole === 'manager')
                    return true;
            }
            return false;
        });
        const selectedProjectMembers = computed(() => {
            const project = selectedProject.value;
            if (!project)
                return [];
            return project.members.map((member) => ({
                id: member.userId,
                fullName: member.fullName,
                role: member.role,
                email: member.email,
                initials: initials(member.fullName),
            }));
        });
        const selectedProjectStats = computed(() => {
            const project = selectedProject.value;
            if (!project)
                return { total: 0, todo: 0, inProgress: 0, inReview: 0, done: 0, overdue: 0, completionRate: 0 };
            const tasks = project.tasks;
            const total = tasks.length;
            const todo = tasks.filter(t => t.status === 'Todo').length;
            const inProgress = tasks.filter(t => t.status === 'InProgress').length;
            const inReview = tasks.filter(t => t.status === 'InReview').length;
            const done = tasks.filter(t => t.status === 'Done').length;
            const overdue = tasks.filter(t => isTaskOverdue(t)).length;
            const completionRate = total > 0 ? Math.round((done / total) * 100) : 0;
            return {
                total,
                todo,
                inProgress,
                inReview,
                done,
                overdue,
                completionRate
            };
        });
        const notificationItems = computed(() => {
            const realtime = notifications.value.map(toDashboardNotification);
            const dashboardItems = dashboard.value.notifications;
            const seen = new Set();
            return [...realtime, ...dashboardItems]
                .filter((item) => {
                if (seen.has(item.id))
                    return false;
                seen.add(item.id);
                return true;
            })
                .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());
        });
        const notificationCount = computed(() => notifications.value.filter((notification) => !notification.isRead).length +
            dashboard.value.notifications.filter((notification) => notification.tone !== 'info').length);
        watch(filteredProjects, (items) => {
            if (!['dashboard', 'projects'].includes(String(route.name ?? '')))
                return;
            if (!items.some((project) => project.id === activeProjectId.value)) {
                activeProjectId.value = items[0]?.id ?? projects.value[0]?.id ?? null;
            }
        }, { immediate: true });
        watch(() => route.params.projectId, (projectId) => {
            if (typeof projectId === 'string') {
                activeProjectId.value = projectId;
            }
        }, { immediate: true });
        watch(() => route.params.taskId, (taskId) => {
            if (typeof taskId === 'string') {
                selectedTaskId.value = taskId;
                activeProjectTab.value = 'tasks';
            }
        }, { immediate: true });
        watch(selectedTask, (task) => {
            selectedTaskId.value = task?.id ?? null;
            if (task && !usingFallback.value) {
                void loadComments(task.id);
                void loadAttachments(task.id);
                void loadTimeEntries(task.id);
            }
            else {
                comments.value = [];
                attachments.value = [];
                timeEntries.value = [];
            }
        }, { immediate: true });
        watch(() => [activeProjectId.value, activeProjectTab.value], ([projectId, tab]) => {
            if (projectId && tab === 'wiki' && !usingFallback.value) {
                void loadWikiPages(String(projectId));
            }
        }, { immediate: true });
        onMounted(async () => {
            await Promise.all([loadMe(), loadDashboard(), loadUsers(), loadNotifications()]);
            await connectNotifications();
        });
        async function loadDashboard() {
            isLoading.value = true;
            const previousProjectId = activeProjectId.value;
            try {
                dashboard.value = await apiJson('/api/dashboard/overview');
                usingFallback.value = false;
            }
            catch (error) {
                console.warn('Using fallback dashboard data.', error);
                dashboard.value = fallbackDashboard;
                usingFallback.value = true;
            }
            finally {
                const routeProjectId = typeof route.params.projectId === 'string' ? route.params.projectId : null;
                const preferredProjectId = routeProjectId ?? previousProjectId;
                activeProjectId.value = dashboard.value.projects.some((project) => project.id === preferredProjectId)
                    ? preferredProjectId
                    : dashboard.value.projects[0]?.id ?? null;
                isLoading.value = false;
            }
        }
        async function loadMe() {
            try {
                currentUser.value = await apiResult('/api/auth/me');
            }
            catch (error) {
                console.warn('Could not load current user.', error);
            }
        }
        async function loadUsers() {
            try {
                users.value = await apiResult('/api/users');
            }
            catch (error) {
                console.warn('Could not load users.', error);
            }
        }
        async function loadNotifications() {
            try {
                notifications.value = await apiResult('/api/notifications');
            }
            catch (error) {
                console.warn('Could not load notifications.', error);
            }
        }
        async function loadComments(taskId) {
            try {
                comments.value = await apiResult(`/api/comments/task/${taskId}`);
            }
            catch (error) {
                console.warn('Could not load comments.', error);
                comments.value = [];
            }
        }
        async function loadAttachments(taskId) {
            try {
                attachments.value = await apiResult(`/api/attachments/task/${taskId}`);
            }
            catch (error) {
                console.warn('Could not load attachments.', error);
                attachments.value = [];
            }
        }
        async function loadTimeEntries(taskId) {
            try {
                const entries = await apiJson(`/api/tasks/${taskId}/time-entries`);
                timeEntries.value = entries;
                activeTimer.value = entries.find(e => e.endedAt === null) ?? null;
            }
            catch (error) {
                console.warn('Could not load time entries.', error);
                timeEntries.value = [];
                activeTimer.value = null;
            }
        }
        async function startTimer(taskId) {
            try {
                const entry = await apiJson(`/api/tasks/${taskId}/time-entries`, { method: 'POST' });
                activeTimer.value = entry;
                await loadTimeEntries(taskId);
                showSuccess('Đã bắt đầu ghi thời gian');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể bắt đầu ghi thời gian'));
            }
        }
        async function stopTimer(entryId) {
            try {
                await apiJson(`/api/time-entries/${entryId}/stop`, { method: 'PATCH' });
                activeTimer.value = null;
                if (selectedTaskId.value)
                    await loadTimeEntries(selectedTaskId.value);
                showSuccess('Đã dừng ghi thời gian');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể dừng ghi thời gian'));
            }
        }
        async function addManualTimeEntry(taskId, manualMinutes, note) {
            if (!taskId || manualMinutes <= 0)
                return false;
            try {
                await apiJson(`/api/tasks/${taskId}/time-entries/manual`, {
                    method: 'POST',
                    body: JSON.stringify({
                        taskId,
                        startedAt: new Date().toISOString(),
                        manualMinutes,
                        note: note.trim() || null,
                    }),
                });
                await loadTimeEntries(taskId);
                await loadDashboard();
                showSuccess('Ghi thời gian thủ công thành công');
                return true;
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể ghi thời gian thủ công'));
                return false;
            }
        }
        async function connectNotifications() {
            if (notificationConnectionStarted)
                return;
            notificationConnectionStarted = true;
            const connection = new HubConnectionBuilder()
                .withUrl('/hubs/notification')
                .withAutomaticReconnect()
                .build();
            connection.on('notificationReceived', (notification) => {
                notifications.value = [notification, ...notifications.value.filter((item) => item.id !== notification.id)];
                showInfo(notification.message);
                void loadDashboard();
            });
            try {
                await connection.start();
            }
            catch (error) {
                console.warn('SignalR notification connection failed.', error);
            }
        }
        function selectProject(projectId) {
            activeProjectId.value = projectId;
            selectedTaskId.value = projects.value.find((project) => project.id === projectId)?.tasks[0]?.id ?? null;
            activeProjectTab.value = 'stats';
            void router.push(`/projects/${projectId}`);
        }
        function closeProjectDetails() {
            void router.push('/projects');
        }
        function selectTaskInProject(taskId) {
            const projectId = selectedProject.value?.id;
            selectedTaskId.value = taskId;
            activeProjectTab.value = 'tasks';
            if (projectId) {
                void router.push(`/projects/${projectId}/tasks/${taskId}`);
            }
        }
        function openTask(projectId, taskId) {
            activeProjectId.value = projectId;
            selectedTaskId.value = taskId;
            activeProjectTab.value = 'tasks';
            void router.push(`/projects/${projectId}/tasks/${taskId}`);
        }
        function openCreateProject() {
            createProjectOpen.value = true;
            projectBeingEditedId.value = null;
            void router.push('/projects');
        }
        async function createProject() {
            const name = projectName.value.trim();
            if (!name)
                return;
            try {
                const project = await apiResult('/api/projects', {
                    method: 'POST',
                    body: JSON.stringify({
                        name,
                        description: projectDescription.value.trim() || null,
                        startDate: null,
                        endDate: projectEndDate.value ? new Date(projectEndDate.value).toISOString() : null,
                    }),
                });
                clearProjectForm();
                createProjectOpen.value = false;
                await loadDashboard();
                activeProjectId.value = project.id;
                void router.push(`/projects/${project.id}`);
                showSuccess(`Thêm dự án "${project.name}" thành công`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể thêm dự án'));
            }
        }
        function beginEditProject(projectId) {
            const project = projects.value.find((item) => item.id === projectId);
            if (!project)
                return;
            activeProjectId.value = projectId;
            createProjectOpen.value = false;
            projectBeingEditedId.value = projectId;
            editProjectName.value = project.name;
            editProjectDescription.value = project.description ?? '';
        }
        async function saveProjectEdit() {
            const project = projects.value.find((item) => item.id === projectBeingEditedId.value);
            const name = editProjectName.value.trim();
            if (!project || !name)
                return;
            try {
                await apiResult(`/api/projects/${project.id}`, {
                    method: 'PUT',
                    body: JSON.stringify({
                        name,
                        description: editProjectDescription.value.trim() || null,
                        status: project.status,
                        startDate: null,
                        endDate: project.endDate,
                    }),
                });
                projectBeingEditedId.value = null;
                await loadDashboard();
                activeProjectId.value = project.id;
                showSuccess(`Cập nhật dự án "${name}" thành công`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể cập nhật dự án'));
            }
        }
        async function deleteProject(projectId) {
            const project = projects.value.find((item) => item.id === projectId);
            if (!project)
                return;
            try {
                await apiCommand(`/api/projects/${projectId}`, { method: 'DELETE' });
                await loadDashboard();
                showSuccess(`Xóa dự án "${project.name}" thành công`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể xóa dự án'));
            }
        }
        async function createTask() {
            const project = selectedProject.value;
            const title = newTaskTitle.value.trim();
            if (!project || !title)
                return;
            if (taskBeingEdited.value) {
                await saveTaskEdit();
                return;
            }
            try {
                const task = await apiResult('/api/tasks', {
                    method: 'POST',
                    body: JSON.stringify({
                        title,
                        description: newTaskDescription.value.trim() || null,
                        priority: newTaskPriority.value,
                        dueDate: newTaskDueDate.value ? new Date(newTaskDueDate.value).toISOString() : null,
                        estimatedHours: null,
                        projectId: project.id,
                        assigneeId: newTaskAssigneeId.value || null,
                        isPrivate: false,
                    }),
                });
                clearTaskForm();
                createTaskOpen.value = false;
                await loadDashboard();
                showSuccess(task.aiPrioritySuggestion ? `Thêm nhiệm vụ thành công. ${task.aiPrioritySuggestion}` : 'Thêm nhiệm vụ thành công');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể thêm nhiệm vụ'));
            }
        }
        async function moveTask(task, status) {
            try {
                await apiCommand(`/api/tasks/${task.id}/status`, {
                    method: 'PATCH',
                    body: JSON.stringify({ status }),
                });
                await loadDashboard();
                selectedTaskId.value = task.id;
                showSuccess(`Đã chuyển nhiệm vụ sang ${displayStatus(status)}`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể cập nhật trạng thái nhiệm vụ'));
            }
        }
        const taskBeingEdited = ref(null);
        function beginEditTask(task) {
            taskBeingEdited.value = task;
            newTaskTitle.value = task.title;
            newTaskDescription.value = '';
            newTaskPriority.value = task.priority;
            newTaskAssigneeId.value = '';
            newTaskDueDate.value = task.dueDate ? new Date(task.dueDate).toISOString().split('T')[0] : '';
            createTaskOpen.value = true;
        }
        async function saveTaskEdit() {
            if (!taskBeingEdited.value)
                return;
            try {
                await apiResult(`/api/tasks/${taskBeingEdited.value.id}`, {
                    method: 'PUT',
                    body: JSON.stringify({
                        title: newTaskTitle.value.trim(),
                        description: newTaskDescription.value.trim() || null,
                        status: taskBeingEdited.value.status,
                        priority: newTaskPriority.value,
                        dueDate: newTaskDueDate.value ? new Date(newTaskDueDate.value).toISOString() : null,
                        assigneeId: newTaskAssigneeId.value || null,
                    }),
                });
                clearTaskForm();
                createTaskOpen.value = false;
                taskBeingEdited.value = null;
                await loadDashboard();
                showSuccess('Cập nhật nhiệm vụ thành công');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể cập nhật nhiệm vụ'));
            }
        }
        async function quickEditTaskTitle(taskId, title) {
            const nextTitle = title.trim();
            if (!taskId || !nextTitle)
                return false;
            try {
                const currentTask = await apiResult(`/api/tasks/${taskId}`);
                await apiResult(`/api/tasks/${taskId}`, {
                    method: 'PUT',
                    body: JSON.stringify({
                        title: nextTitle,
                        description: currentTask.description,
                        status: currentTask.status,
                        priority: currentTask.priority,
                        dueDate: currentTask.dueDate,
                        estimatedHours: currentTask.estimatedHours,
                        actualHours: currentTask.actualHours,
                        assigneeId: currentTask.assigneeId,
                        isPrivate: currentTask.isPrivate,
                    }),
                });
                await loadDashboard();
                selectedTaskId.value = taskId;
                showSuccess('Cập nhật nhiệm vụ thành công');
                return true;
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể cập nhật nhiệm vụ'));
                return false;
            }
        }
        async function deleteTask(taskId) {
            if (!confirm('Bạn có chắc chắn muốn xóa task này?'))
                return;
            try {
                await apiCommand(`/api/tasks/${taskId}`, { method: 'DELETE' });
                await loadDashboard();
                showSuccess('Xóa nhiệm vụ thành công');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể xóa nhiệm vụ'));
            }
        }
        async function submitComment() {
            const task = selectedTask.value;
            const content = newComment.value.trim();
            if (!task || !content)
                return;
            try {
                await apiResult('/api/comments', {
                    method: 'POST',
                    body: JSON.stringify({
                        taskItemId: task.id,
                        content,
                    }),
                });
                newComment.value = '';
                await loadComments(task.id);
                await loadDashboard();
                showSuccess('Thêm bình luận thành công');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể thêm bình luận'));
            }
        }
        async function deleteComment(commentId) {
            if (!confirm('Bạn có chắc chắn muốn xóa bình luận này?'))
                return;
            try {
                await apiCommand(`/api/comments/${commentId}`, { method: 'DELETE' });
                if (selectedTask.value)
                    await loadComments(selectedTask.value.id);
                await loadDashboard();
                showSuccess('Xóa bình luận thành công');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể xóa bình luận'));
            }
        }
        async function addMember(userId) {
            const project = selectedProject.value;
            if (!project)
                return;
            try {
                await apiCommand(`/api/projects/${project.id}/members`, {
                    method: 'POST',
                    body: JSON.stringify({ userId, role: 'Member' }),
                });
                await loadDashboard();
                showSuccess('Thêm thành viên thành công');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể thêm thành viên'));
            }
        }
        async function removeMember(userId) {
            const project = selectedProject.value;
            if (!project)
                return;
            if (!confirm('Bạn có chắc chắn muốn xóa thành viên này khỏi dự án?'))
                return;
            try {
                await apiCommand(`/api/projects/${project.id}/members/${userId}`, { method: 'DELETE' });
                await loadDashboard();
                showSuccess('Xóa thành viên khỏi dự án thành công');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể xóa thành viên khỏi dự án'));
            }
        }
        async function updateMemberRole(userId, role) {
            const project = selectedProject.value;
            if (!project)
                return;
            try {
                await apiCommand(`/api/projects/${project.id}/members`, {
                    method: 'POST',
                    body: JSON.stringify({ userId, role }),
                });
                await loadDashboard();
                showSuccess(`Cập nhật vai trò thành ${role} thành công`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể cập nhật vai trò thành viên'));
            }
        }
        async function loadWikiPages(projectId) {
            try {
                wikiPages.value = await apiResult(`/api/projects/${projectId}/wiki`);
            }
            catch (error) {
                console.warn('Could not load wiki pages.', error);
                wikiPages.value = [];
            }
        }
        async function createWikiPage(title, content = '') {
            const project = selectedProject.value;
            if (!project || !title)
                return;
            try {
                await apiResult(`/api/projects/${project.id}/wiki`, {
                    method: 'POST',
                    body: JSON.stringify({ title, content }),
                });
                await loadWikiPages(project.id);
                showSuccess(`Tạo trang Wiki "${title}" thành công`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể tạo trang Wiki'));
            }
        }
        async function updateWikiPage(pageId, title, content) {
            const project = selectedProject.value;
            if (!project || !title)
                return;
            try {
                await apiCommand(`/api/projects/${project.id}/wiki/${pageId}`, {
                    method: 'PUT',
                    body: JSON.stringify({ title, content }),
                });
                await loadWikiPages(project.id);
                showSuccess(`Cập nhật trang Wiki "${title}" thành công`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể cập nhật trang Wiki'));
            }
        }
        async function deleteWikiPage(pageId) {
            const project = selectedProject.value;
            if (!project)
                return;
            try {
                await apiCommand(`/api/projects/${project.id}/wiki/${pageId}`, { method: 'DELETE' });
                await loadWikiPages(project.id);
                showSuccess('Xóa trang Wiki thành công');
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể xóa trang Wiki'));
            }
        }
        async function uploadAttachment(event) {
            const task = selectedTask.value;
            const input = event.target;
            const file = input.files?.[0];
            if (!task || !file)
                return;
            const formData = new FormData();
            formData.append('file', file);
            try {
                await apiResult(`/api/attachments/task/${task.id}`, {
                    method: 'POST',
                    body: formData,
                });
                input.value = '';
                await loadAttachments(task.id);
                await loadDashboard();
                showSuccess(`Tải tệp "${file.name}" lên thành công`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể tải tệp lên'));
            }
        }
        async function deleteAttachment(attachment) {
            const taskId = selectedTask.value?.id;
            try {
                await apiCommand(`/api/attachments/${attachment.id}`, { method: 'DELETE' });
                if (taskId)
                    await loadAttachments(taskId);
                await loadDashboard();
                showSuccess(`Xóa tệp "${attachment.fileName}" thành công`);
            }
            catch (error) {
                showError(errorMessage(error, 'Không thể xóa tệp'));
            }
        }
        async function dismissNotification(notificationId) {
            notifications.value = notifications.value.filter((notification) => notification.id !== notificationId);
            dashboard.value.notifications = dashboard.value.notifications.filter((notification) => notification.id !== notificationId);
            if (isGuid(notificationId)) {
                try {
                    await apiCommand(`/api/notifications/${notificationId}/read`, { method: 'PATCH' });
                }
                catch (error) {
                    console.warn('Could not mark notification as read.', error);
                    showError(errorMessage(error, 'Không thể đánh dấu thông báo đã đọc'));
                }
            }
        }
        async function clearActionableNotifications() {
            notifications.value = [];
            dashboard.value.notifications = dashboard.value.notifications.filter((notification) => notification.tone === 'info');
            notificationsOpen.value = false;
            try {
                await apiCommand('/api/notifications/read-all', { method: 'PATCH' });
                showSuccess('Đã đánh dấu tất cả thông báo là đã đọc');
            }
            catch (error) {
                console.warn('Could not mark notifications as read.', error);
                showError(errorMessage(error, 'Không thể đánh dấu tất cả thông báo đã đọc'));
            }
        }
        async function logout() {
            try {
                await apiCommand('/api/auth/logout', { method: 'POST' });
            }
            finally {
                window.location.href = '/Account/Login';
            }
        }
        async function apiJson(url, options = {}) {
            const headers = new Headers(options.headers);
            if (options.body && !(options.body instanceof FormData)) {
                headers.set('Content-Type', 'application/json');
            }
            headers.set('Accept', 'application/json');
            const response = await fetch(url, {
                credentials: 'same-origin',
                ...options,
                headers,
            });
            if (response.status === 401) {
                window.location.href = `/Account/Login?returnUrl=${encodeURIComponent(window.location.pathname)}`;
                throw new Error('Authentication required.');
            }
            const text = await response.text();
            const payload = parseApiPayload(text);
            if (!response.ok) {
                throw new Error(apiPayloadError(payload, response.status));
            }
            return payload;
        }
        async function apiResult(url, options = {}) {
            const result = await apiJson(url, options);
            if (isApiResult(result)) {
                if (!result.isSuccess || result.data == null) {
                    throw new Error(result.error ?? 'Không thể hoàn tất yêu cầu.');
                }
                return result.data;
            }
            return result;
        }
        async function apiCommand(url, options = {}) {
            const result = await apiJson(url, options);
            if (isApiResult(result) && !result.isSuccess) {
                throw new Error(result.error ?? 'Không thể hoàn tất yêu cầu.');
            }
            if (result && typeof result === 'object' && 'ok' in result && result.ok === false) {
                throw new Error('Không thể hoàn tất yêu cầu.');
            }
        }
        function parseApiPayload(text) {
            if (!text)
                return null;
            try {
                return JSON.parse(text);
            }
            catch {
                return text;
            }
        }
        function apiPayloadError(payload, status) {
            if (typeof payload === 'string' && payload.trim())
                return payload.trim();
            if (payload && typeof payload === 'object') {
                if ('error' in payload && typeof payload.error === 'string' && payload.error.trim())
                    return payload.error;
                if ('message' in payload && typeof payload.message === 'string' && payload.message.trim())
                    return payload.message;
                if ('title' in payload && typeof payload.title === 'string' && payload.title.trim()) {
                    const validationMessage = validationErrorMessage(payload);
                    return validationMessage ?? payload.title;
                }
            }
            return `Không thể hoàn tất yêu cầu (mã ${status}).`;
        }
        function validationErrorMessage(payload) {
            if (!('errors' in payload) || !payload.errors || typeof payload.errors !== 'object')
                return null;
            for (const value of Object.values(payload.errors)) {
                if (Array.isArray(value) && typeof value[0] === 'string')
                    return value[0];
                if (typeof value === 'string')
                    return value;
            }
            return null;
        }
        function isApiResult(payload) {
            return Boolean(payload && typeof payload === 'object' && 'isSuccess' in payload);
        }
        function tasksByStatus(status) {
            const query = taskSearchQuery.value.trim().toLowerCase();
            return selectedProjectTasks.value.filter((task) => {
                if (task.status !== status)
                    return false;
                if (!query)
                    return true;
                return task.title.toLowerCase().includes(query);
            });
        }
        function nextStatuses(status) {
            switch (status) {
                case 'Todo':
                    return ['InProgress'];
                case 'InProgress':
                    return ['InReview', 'Done'];
                case 'InReview':
                    return ['InProgress', 'Done'];
                case 'Done':
                    return ['InReview'];
                default:
                    return ['Todo'];
            }
        }
        function clearProjectForm() {
            projectName.value = '';
            projectDescription.value = '';
            projectEndDate.value = '';
        }
        function clearTaskForm() {
            newTaskTitle.value = '';
            newTaskDescription.value = '';
            newTaskPriority.value = 'Medium';
            newTaskAssigneeId.value = '';
            newTaskDueDate.value = '';
        }
        function displayStatus(status) {
            switch (status) {
                case 'Active':
                    return 'Active';
                case 'InProgress':
                    return 'In progress';
                case 'InReview':
                    return 'In review';
                case 'Done':
                    return 'Done';
                case 'Planned':
                    return 'Planned';
                case 'Archived':
                    return 'Archived';
                case 'Todo':
                    return 'Todo';
                case 'Cancelled':
                    return 'Cancelled';
                default:
                    return status || 'Unknown';
            }
        }
        function displayRole(role) {
            return role === 'Admin' ? 'Admin' : role === 'Member' ? 'Member' : role || '';
        }
        function statusTone(status) {
            switch (status) {
                case 'Active':
                case 'InProgress':
                    return 'active';
                case 'Planned':
                    return 'planned';
                case 'Archived':
                    return 'archived';
                default:
                    return 'neutral';
            }
        }
        function formatDate(value) {
            if (!value)
                return 'No date';
            return new Intl.DateTimeFormat('en', {
                month: 'short',
                day: 'numeric',
            }).format(new Date(value));
        }
        function formatTime(value) {
            return new Intl.DateTimeFormat('en', {
                hour: 'numeric',
                minute: '2-digit',
            }).format(new Date(value));
        }
        function formatFileSize(bytes) {
            if (bytes < 1024)
                return `${bytes} B`;
            if (bytes < 1024 * 1024)
                return `${Math.round(bytes / 1024)} KB`;
            return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
        }
        function initials(name) {
            return name
                .split(' ')
                .filter(Boolean)
                .slice(0, 2)
                .map((part) => part[0]?.toUpperCase() ?? '')
                .join('');
        }
        function isTaskOverdue(task) {
            return Boolean(task.dueDate) && new Date(task.dueDate).getTime() < Date.now() && task.status !== 'Done';
        }
        function notificationClass(notification) {
            return `notice notice--${notification.tone}`;
        }
        function toDashboardNotification(notification) {
            return {
                id: notification.id,
                title: notification.type,
                message: notification.message,
                tone: notification.type === 'DueDateReminder' ? 'critical' : notification.type === 'Info' ? 'info' : 'warning',
                createdAt: notification.createdAt,
            };
        }
        function isGuid(value) {
            return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
        }
        function errorMessage(error, fallback = 'Đã xảy ra lỗi khi lưu dữ liệu') {
            const message = error instanceof Error ? error.message.trim() : '';
            if (!message ||
                message === 'Request failed.' ||
                message.startsWith('Request failed with status') ||
                message.startsWith('Không thể hoàn tất yêu cầu')) {
                return fallback;
            }
            return message;
        }
        provide(dashboardContextKey, {
            activeProjectCards,
            activeProjectId,
            activeProjectTab,
            activeTaskMenu,
            addManualTimeEntry,
            addMember,
            archivedProjectCards,
            assignedTaskCards,
            attachments,
            beginEditProject,
            beginEditTask,
            clearActionableNotifications,
            closeProjectDetails,
            comments,
            createProject,
            createProjectOpen,
            createTask,
            createTaskOpen,
            currentUser,
            deleteAttachment,
            deleteComment,
            deleteProject,
            deleteTask,
            displayRole,
            displayStatus,
            editProjectDescription,
            editProjectName,
            filteredProjects,
            formatDate,
            formatFileSize,
            formatTime,
            isLoading,
            isProjectAdmin,
            isTaskOverdue,
            logout,
            moveTask,
            newComment,
            newTaskAssigneeId,
            newTaskDescription,
            newTaskDueDate,
            newTaskPriority,
            newTaskTitle,
            nextStatuses,
            openCreateProject,
            openTask,
            priorities,
            projectBeingEditedId,
            projectCards,
            projectDescription,
            projectEndDate,
            projectFilter,
            projectName,
            projectSort,
            projects,
            quickEditTaskTitle,
            removeMember,
            saveProjectEdit,
            searchQuery,
            selectProject,
            selectedProject,
            selectedProjectMembers,
            selectedProjectStats,
            selectedTask,
            selectedTaskId,
            selectTaskInProject,
            statusColumns,
            statusTone,
            submitComment,
            summaryCards,
            tabs,
            tasksByStatus,
            team,
            toggleTaskMenu,
            updateMemberRole,
            uploadAttachment,
            users,
            wikiPages,
            loadWikiPages,
            createWikiPage,
            updateWikiPage,
            deleteWikiPage,
            taskSearchQuery,
            taskBeingQuickEditedId,
            timeEntries,
            activeTimer,
            startTimer,
            stopTimer,
            loadTimeEntries,
        });
        return (_ctx, _cache) => {
            const _component_RouterView = resolveComponent("RouterView");
            return (openBlock(), createBlock(_sfc_main$3, {
                "nav-items": navigation,
                "notification-count": notificationCount.value,
                "user-name": currentUser.value?.fullName || currentUser.value?.email || 'Qaly user',
                "user-initials": initials(currentUser.value?.fullName || currentUser.value?.email || 'QU'),
                onNotifications: _cache[1] || (_cache[1] = ($event) => (notificationsOpen.value = !notificationsOpen.value)),
                onAssistant: () => { },
                onLogout: logout
            }, {
                default: withCtx(() => [
                    createVNode(_component_RouterView),
                    (notificationsOpen.value)
                        ? (openBlock(), createElementBlock("div", _hoisted_1$1, [
                            createBaseVNode("div", _hoisted_2$1, [
                                _cache[2] || (_cache[2] = createBaseVNode("div", null, [
                                    createBaseVNode("span", null, "Notifications"),
                                    createBaseVNode("h2", null, "Current signals")
                                ], -1)),
                                createBaseVNode("div", _hoisted_3$1, [
                                    createBaseVNode("button", {
                                        class: "text-button",
                                        type: "button",
                                        onClick: clearActionableNotifications
                                    }, "Read all"),
                                    createBaseVNode("button", {
                                        class: "icon-button icon-button--small",
                                        type: "button",
                                        onClick: _cache[0] || (_cache[0] = ($event) => (notificationsOpen.value = false))
                                    }, [
                                        createVNode(unref(X), { size: 16 })
                                    ])
                                ])
                            ]),
                            (openBlock(true), createElementBlock(Fragment, null, renderList(notificationItems.value.slice(0, 6), (notification) => {
                                return (openBlock(), createElementBlock("article", {
                                    key: notification.id,
                                    class: normalizeClass(notificationClass(notification))
                                }, [
                                    createBaseVNode("div", _hoisted_4$1, [
                                        createBaseVNode("strong", null, toDisplayString(notification.title), 1),
                                        createBaseVNode("button", {
                                            type: "button",
                                            "aria-label": "Dismiss notification",
                                            onClick: ($event) => (dismissNotification(notification.id))
                                        }, [
                                            createVNode(unref(X), { size: 14 })
                                        ], 8, _hoisted_5)
                                    ]),
                                    createBaseVNode("p", null, toDisplayString(notification.message), 1),
                                    createBaseVNode("span", null, toDisplayString(formatTime(notification.createdAt)), 1)
                                ], 2));
                            }), 128))
                        ]))
                        : createCommentVNode("", true),
                    createVNode(FloatingChatbot)
                ]),
                _: 1
            }, 8, ["notification-count", "user-name", "user-initials"]));
        };
    }
});
const _hoisted_1 = ["role"];
const _hoisted_2 = {
    class: "toast-card__icon",
    "aria-hidden": "true"
};
const _hoisted_3 = { class: "toast-card__content" };
const _hoisted_4 = ["onClick"];
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'ToastContainer',
    setup(__props) {
        const { toasts, dismissToast } = useToast();
        const iconByType = {
            success: CircleCheck,
            error: CircleX,
            warning: TriangleAlert,
            info: Info,
        };
        const titleByType = {
            success: 'Thành công',
            error: 'Thất bại',
            warning: 'Cần chú ý',
            info: 'Thông báo',
        };
        return (_ctx, _cache) => {
            return (openBlock(), createBlock(Teleport, { to: "body" }, [
                createVNode(TransitionGroup, {
                    name: "toast-stack",
                    tag: "div",
                    class: "toast-viewport",
                    "aria-live": "polite"
                }, {
                    default: withCtx(() => [
                        (openBlock(true), createElementBlock(Fragment, null, renderList(unref(toasts), (toast) => {
                            return (openBlock(), createElementBlock("article", {
                                key: toast.id,
                                class: normalizeClass(["toast-card", `toast-card--${toast.type}`]),
                                role: toast.type === 'error' ? 'alert' : 'status'
                            }, [
                                createBaseVNode("div", _hoisted_2, [
                                    (openBlock(), createBlock(resolveDynamicComponent(iconByType[toast.type]), { size: 19 }))
                                ]),
                                createBaseVNode("div", _hoisted_3, [
                                    createBaseVNode("strong", null, toDisplayString(toast.title ?? titleByType[toast.type]), 1),
                                    createBaseVNode("p", null, toDisplayString(toast.message), 1)
                                ]),
                                createBaseVNode("button", {
                                    class: "toast-card__close",
                                    type: "button",
                                    "aria-label": "Đóng thông báo",
                                    onClick: ($event) => (unref(dismissToast)(toast.id))
                                }, [
                                    createVNode(unref(X), { size: 16 })
                                ], 8, _hoisted_4),
                                createBaseVNode("span", {
                                    class: "toast-card__progress",
                                    style: normalizeStyle({ animationDuration: `${toast.duration}ms` })
                                }, null, 4)
                            ], 10, _hoisted_1));
                        }), 128))
                    ]),
                    _: 1
                })
            ]));
        };
    }
});
const scriptRel = 'modulepreload';
const assetsURL = function (dep) { return "/dist/" + dep; };
const seen = {};
const __vitePreload = function preload(baseModule, deps, importerUrl) {
    let promise = Promise.resolve();
    if (true && deps && deps.length > 0) {
        let allSettled2 = function (promises) {
            return Promise.all(promises.map((p) => Promise.resolve(p).then((value) => ({ status: "fulfilled", value }), (reason) => ({ status: "rejected", reason }))));
        };
        document.getElementsByTagName("link");
        const cspNonceMeta = document.querySelector("meta[property=csp-nonce]");
        const cspNonce = cspNonceMeta?.nonce || cspNonceMeta?.getAttribute("nonce");
        promise = allSettled2(deps.map((dep) => {
            dep = assetsURL(dep);
            if (dep in seen)
                return;
            seen[dep] = true;
            const isCss = dep.endsWith(".css");
            const cssSelector = isCss ? '[rel="stylesheet"]' : "";
            if (document.querySelector(`link[href="${dep}"]${cssSelector}`)) {
                return;
            }
            const link = document.createElement("link");
            link.rel = isCss ? "stylesheet" : scriptRel;
            if (!isCss) {
                link.as = "script";
            }
            link.crossOrigin = "";
            link.href = dep;
            if (cspNonce) {
                link.setAttribute("nonce", cspNonce);
            }
            document.head.appendChild(link);
            if (isCss) {
                return new Promise((res, rej) => {
                    link.addEventListener("load", res);
                    link.addEventListener("error", () => rej(new Error(`Unable to preload CSS for ${dep}`)));
                });
            }
        }));
    }
    function handlePreloadError(err) {
        const e = new Event("vite:preloadError", {
            cancelable: true
        });
        e.payload = err;
        window.dispatchEvent(e);
        if (!e.defaultPrevented) {
            throw err;
        }
    }
    return promise.then((res) => {
        for (const item of res || []) {
            if (item.status !== "rejected")
                continue;
            handlePreloadError(item.reason);
        }
        return baseModule().catch(handlePreloadError);
    });
};
const ArchivedProjectsPage = () => __vitePreload(() => import('./ArchivedProjectsPage.js'), true ? __vite__mapDeps([0,1,2,3,4,5]) : void 0);
const DashboardPage = () => __vitePreload(() => import('./DashboardPage.js'), true ? __vite__mapDeps([6,3,2,7,4,5]) : void 0);
const ProfilePage = () => __vitePreload(() => import('./ProfilePage.js'), true ? __vite__mapDeps([8,2,3,4,5]) : void 0);
const ProjectDetailPage = () => __vitePreload(() => import('./ProjectDetailPage.js'), true ? __vite__mapDeps([9,3,2,4,5,10]) : void 0);
const ProjectsPage = () => __vitePreload(() => import('./ProjectsPage.js'), true ? __vite__mapDeps([11,3,1,2,7,4,5]) : void 0);
const TasksPage = () => __vitePreload(() => import('./TasksPage.js'), true ? __vite__mapDeps([12,2,3,4,5]) : void 0);
const TeamsPage = () => __vitePreload(() => import('./TeamsPage.js'), true ? __vite__mapDeps([13,2,3,4,5]) : void 0);
const router = createRouter({
    history: createWebHistory(),
    routes: [
        { path: '/', redirect: '/dashboard' },
        { path: '/dashboard', name: 'dashboard', component: DashboardPage },
        { path: '/profile', name: 'profile', component: ProfilePage },
        { path: '/projects', name: 'projects', component: ProjectsPage },
        { path: '/projects/archived', name: 'projects-archived', component: ArchivedProjectsPage },
        { path: '/projects/:projectId', name: 'project-detail', component: ProjectDetailPage },
        { path: '/projects/:projectId/tasks/:taskId', name: 'project-task', component: ProjectDetailPage },
        { path: '/tasks', name: 'tasks', component: TasksPage },
        { path: '/teams', name: 'teams', component: TeamsPage },
        { path: '/:pathMatch(.*)*', redirect: '/dashboard' },
    ],
    scrollBehavior() {
        return { top: 0 };
    },
});
const toastTarget = document.getElementById('qaly-toast-root');
const target = document.getElementById('qaly-dashboard-app');
if (toastTarget) {
    createApp(_sfc_main).mount(toastTarget);
}
window.qalyToast = {
    showToast,
    showSuccess,
    showError,
};
document.querySelectorAll('[data-toast-message]').forEach((element) => {
    const message = element.dataset.toastMessage?.trim();
    if (!message || element.dataset.toastConsumed === 'true')
        return;
    const type = normalizeToastType(element.dataset.toastType);
    element.dataset.toastConsumed = 'true';
    showToast({ type, message });
});
if (target) {
    createApp(_sfc_main$1).use(router).mount(target);
}
function normalizeToastType(type) {
    return type === 'success' || type === 'error' || type === 'warning' || type === 'info' ? type : 'info';
}
export { _sfc_main$5 as _, _export_sfc as a, showSuccess as s, useDashboardContext as u };
//# sourceMappingURL=main.js.map
