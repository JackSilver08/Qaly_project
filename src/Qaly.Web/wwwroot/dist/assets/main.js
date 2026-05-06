const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["assets/ArchivedProjectsPage.js","assets/ProjectList.vue_vue_type_script_setup_true_lang.js","assets/vendor-icons.js","assets/vendor-vue.js","assets/vendor-markdown.js","assets/vendor-realtime.js","assets/DashboardPage.js","assets/ProjectToolbar.vue_vue_type_script_setup_true_lang.js","assets/ProfilePage.js","assets/ProjectDetailPage.js","assets/ProjectDetailPage.css","assets/ProjectsPage.js","assets/TasksPage.js","assets/TeamsPage.js"])))=>i.map(i=>d[i]);
import { d as defineComponent, u as useRoute, r as resolveComponent, o as openBlock, c as createElementBlock, a as createBaseVNode, F as Fragment, b as renderList, e as createBlock, w as withCtx, n as normalizeClass, f as resolveDynamicComponent, t as toDisplayString, g as createVNode, i as unref, j as onMounted, k as onBeforeUnmount, l as createCommentVNode, m as ref, p as renderSlot, q as inject, s as watch, v as useRouter, x as nextTick, y as withModifiers, z as withDirectives, A as vModelText, B as computed, C as provide, D as createRouter, E as createWebHistory, G as createApp } from './vendor-vue.js';
import { M as MarkdownIt } from './vendor-markdown.js';
import { B as Box, M as Menu, a as Bell, C as ChevronDown, U as User, L as LogOut, X, b as LayoutDashboard, F as FolderKanban, c as ClipboardList, d as Users } from './vendor-icons.js';
import { H as HubConnectionBuilder } from './vendor-realtime.js';
const _hoisted_1$3 = { class: "shell-sidebar no-scrollbar" };
const _hoisted_2$3 = {
    class: "shell-nav",
    "aria-label": "Main navigation"
};
const _hoisted_3$2 = ["href", "onClick"];
const _hoisted_4$2 = ["href", "onClick"];
const _sfc_main$4 = /*@__PURE__*/ defineComponent({
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
            return (openBlock(), createElementBlock("aside", _hoisted_1$3, [
                createBaseVNode("nav", _hoisted_2$3, [
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
                                ], 10, _hoisted_3$2)
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
                        ], 10, _hoisted_4$2)
                    ]),
                    _: 1
                })
            ]));
        };
    }
});
const erumiRobotUrl = '/images/erumi-chatbot.png';
const _sfc_main$3 = /*@__PURE__*/ defineComponent({
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
const _hoisted_1$2 = { class: "shell-header" };
const _hoisted_2$2 = { class: "shell-brand" };
const _hoisted_3$1 = { class: "shell-header-actions" };
const _hoisted_4$1 = {
    key: 0,
    class: "shell-action-badge"
};
const _hoisted_5$1 = ["aria-expanded"];
const _hoisted_6$1 = {
    key: 0,
    class: "shell-user-dropdown-menu",
    role: "menu"
};
const _sfc_main$2 = /*@__PURE__*/ defineComponent({
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
            return (openBlock(), createElementBlock("header", _hoisted_1$2, [
                createBaseVNode("div", _hoisted_2$2, [
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
                createBaseVNode("div", _hoisted_3$1, [
                    createBaseVNode("button", {
                        class: "shell-icon-button",
                        type: "button",
                        "aria-label": "Thong bao",
                        onClick: _cache[1] || (_cache[1] = ($event) => (_ctx.$emit('notifications')))
                    }, [
                        createVNode(unref(Bell), { size: 18 }),
                        (__props.notificationCount > 0)
                            ? (openBlock(), createElementBlock("span", _hoisted_4$1, toDisplayString(__props.notificationCount), 1))
                            : createCommentVNode("", true)
                    ]),
                    createBaseVNode("button", {
                        class: "shell-icon-button",
                        type: "button",
                        "aria-label": "Tro ly Qaly",
                        onClick: _cache[2] || (_cache[2] = ($event) => (_ctx.$emit('assistant')))
                    }, [
                        createVNode(_sfc_main$3, { size: "launcher" })
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
                        ], 10, _hoisted_5$1),
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
const _hoisted_1$1 = { class: "app-shell" };
const _hoisted_2$1 = { class: "shell-main no-scrollbar" };
const _sfc_main$1 = /*@__PURE__*/ defineComponent({
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
            return (openBlock(), createElementBlock("div", _hoisted_1$1, [
                createVNode(_sfc_main$2, {
                    "brand-name": "QALY",
                    "notification-count": __props.notificationCount,
                    "user-name": __props.userName,
                    "user-initials": __props.userInitials,
                    onToggleSidebar: _cache[0] || (_cache[0] = ($event) => (sidebarOpen.value = true)),
                    onNotifications: _cache[1] || (_cache[1] = ($event) => (_ctx.$emit('notifications'))),
                    onAssistant: _cache[2] || (_cache[2] = ($event) => (_ctx.$emit('assistant'))),
                    onLogout: _cache[3] || (_cache[3] = ($event) => (_ctx.$emit('logout')))
                }, null, 8, ["notification-count", "user-name", "user-initials"]),
                createVNode(_sfc_main$4, {
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
                createBaseVNode("main", _hoisted_2$1, [
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
const _hoisted_1 = {
    key: 0,
    class: "notification-popover glass-card home-notification-popover"
};
const _hoisted_2 = { class: "panel-heading" };
const _hoisted_3 = { class: "popover-actions" };
const _hoisted_4 = { class: "notice__top" };
const _hoisted_5 = ["onClick"];
const _hoisted_6 = {
    key: 1,
    class: "action-toast"
};
const _hoisted_7 = { class: "chat-drawer__header" };
const _hoisted_8 = { class: "chat-drawer__identity" };
const _hoisted_9 = {
    key: 0,
    class: "chat-avatar chat-avatar--robot",
    "aria-hidden": "true"
};
const _hoisted_10 = {
    key: 1,
    class: "chat-avatar chat-avatar--user",
    "aria-hidden": "true"
};
const _hoisted_11 = {
    key: 0,
    class: "chat-message chat-message--assistant"
};
const _hoisted_12 = {
    class: "chat-avatar chat-avatar--robot is-thinking",
    "aria-hidden": "true"
};
const _hoisted_13 = { class: "prompt-list" };
const _hoisted_14 = ["disabled", "onClick"];
const _hoisted_15 = {
    key: 0,
    class: "project-suggestions glass-card"
};
const _hoisted_16 = ["onClick"];
const _hoisted_17 = ["disabled"];
const _hoisted_18 = ["disabled"];
const _sfc_main = /*@__PURE__*/ defineComponent({
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
        const isLoading = ref(true);
        const usingFallback = ref(true);
        const chatOpen = ref(false);
        const notificationsOpen = ref(false);
        const createProjectOpen = ref(false);
        const createTaskOpen = ref(false);
        const projectBeingEditedId = ref(null);
        const selectedTaskId = ref(null);
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
        const actionNotice = ref('');
        const chatDraft = ref('');
        const showProjectSuggestions = ref(false);
        const chatBodyRef = ref(null);
        const projectSuggestions = computed(() => {
            const parts = chatDraft.value.split(' ');
            const lastPart = parts[parts.length - 1];
            if (lastPart.startsWith('@')) {
                const query = lastPart.slice(1).toLowerCase();
                return projects.value.filter(p => p.name.toLowerCase().includes(query));
            }
            return [];
        });
        watch(chatDraft, (val) => {
            const parts = val.split(' ');
            const lastPart = parts[parts.length - 1];
            showProjectSuggestions.value = lastPart.startsWith('@');
        });
        function tagProject(project) {
            const parts = chatDraft.value.split(' ');
            parts[parts.length - 1] = `@${project.name} `;
            chatDraft.value = parts.join(' ');
            showProjectSuggestions.value = false;
        }
        new MarkdownIt({
            html: false,
            linkify: true,
            typographer: true
        });
        const isAssistantThinking = ref(false);
        const chatMessages = ref([
            {
                id: 'assistant-welcome',
                role: 'assistant',
                text: 'Ask me about project risk, overdue work, priority, or assignment suggestions.',
            },
        ]);
        let actionNoticeTimer;
        let notificationConnectionStarted = false;
        const router = useRouter();
        const route = useRoute();
        const projects = computed(() => dashboard.value.projects);
        const team = computed(() => dashboard.value.team);
        const activeProjectsCount = computed(() => projects.value.filter((project) => project.status !== 'Archived').length);
        const totalTasks = computed(() => projects.value.reduce((sum, project) => sum + project.tasks.length, 0));
        const completedTasks = computed(() => projects.value.reduce((sum, project) => sum + project.tasks.filter((task) => task.status === 'Done').length, 0));
        const overdueTasks = computed(() => projects.value.reduce((sum, project) => sum + project.tasks.filter((task) => isTaskOverdue(task)).length, 0));
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
        const selectedProjectSummary = computed(() => {
            const project = selectedProject.value;
            if (!project)
                return 'Select a project to inspect its work.';
            return `${project.name}: ${project.completedTaskCount}/${project.taskCount} tasks complete.`;
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
        const quickPrompts = computed(() => {
            const prompts = [];
            // 1. Gợi ý cho dự án đang chọn
            if (selectedProject.value) {
                prompts.push(`Tóm tắt dự án ${selectedProject.value.name}`);
            }
            // 2. Gợi ý cho dự án MỚI NHẤT
            const latestProject = [...projects.value]
                .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
            if (latestProject && latestProject.id !== selectedProject.value?.id) {
                prompts.push(`Xem dự án mới: ${latestProject.name}`);
            }
            // 3. Gợi ý dựa trên rủi ro (quá hạn)
            const highRiskProject = projects.value.find(p => p.overdueTaskCount > 0);
            if (highRiskProject) {
                prompts.push(`Phân tích rủi ro ${highRiskProject.name}`);
            }
            // 4. Gợi ý chung
            prompts.push('Tôi nên làm gì tiếp theo?');
            prompts.push('Phân bổ công việc có đều không?');
            // Trả về tối đa 3 gợi ý để đảm bảo giao diện đẹp
            return prompts.slice(0, 3);
        });
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
            }
            else {
                comments.value = [];
                attachments.value = [];
            }
        }, { immediate: true });
        watch(() => [activeProjectId.value, activeProjectTab.value], ([projectId, tab]) => {
            if (projectId && tab === 'wiki' && !usingFallback.value) {
                void loadWikiPages(String(projectId));
            }
        }, { immediate: true });
        watch(() => [chatMessages.value.length, isAssistantThinking.value], () => {
            void scrollChatToBottom();
        });
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
                showActionNotice(notification.message);
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
                showActionNotice(`Created project "${project.name}".`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice(`Updated project "${name}".`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
            }
        }
        async function deleteProject(projectId) {
            const project = projects.value.find((item) => item.id === projectId);
            if (!project)
                return;
            try {
                await apiCommand(`/api/projects/${projectId}`, { method: 'DELETE' });
                await loadDashboard();
                showActionNotice(`Deleted project "${project.name}".`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice(task.aiPrioritySuggestion ?? 'Task created.');
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice(`Moved "${task.title}" to ${displayStatus(status)}.`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
            }
        }
        const taskBeingEdited = ref(null);
        function beginEditTask(task) {
            taskBeingEdited.value = task;
            newTaskTitle.value = task.title;
            newTaskDescription.value = ''; // We don't have desc in DashboardTask, but we could load it if needed
            newTaskPriority.value = task.priority;
            newTaskAssigneeId.value = ''; // Need to find assignee ID
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
                showActionNotice('Task updated.');
            }
            catch (error) {
                showActionNotice(errorMessage(error));
            }
        }
        async function deleteTask(taskId) {
            if (!confirm('Bạn có chắc chắn muốn xóa task này?'))
                return;
            try {
                await apiCommand(`/api/tasks/${taskId}`, { method: 'DELETE' });
                await loadDashboard();
                showActionNotice('Task deleted.');
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice('Comment deleted.');
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice('Member added.');
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice('Member removed.');
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice(`Updated role to ${role}.`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice(`Created wiki page "${title}".`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice(`Updated wiki page "${title}".`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
            }
        }
        async function deleteWikiPage(pageId) {
            const project = selectedProject.value;
            if (!project)
                return;
            try {
                await apiCommand(`/api/projects/${project.id}/wiki/${pageId}`, { method: 'DELETE' });
                await loadWikiPages(project.id);
                showActionNotice('Wiki page deleted.');
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                showActionNotice(`Uploaded "${file.name}".`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
            }
        }
        async function deleteAttachment(attachment) {
            const taskId = selectedTask.value?.id;
            try {
                await apiCommand(`/api/attachments/${attachment.id}`, { method: 'DELETE' });
                if (taskId)
                    await loadAttachments(taskId);
                await loadDashboard();
                showActionNotice(`Deleted "${attachment.fileName}".`);
            }
            catch (error) {
                showActionNotice(errorMessage(error));
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
                }
            }
        }
        async function clearActionableNotifications() {
            notifications.value = [];
            dashboard.value.notifications = dashboard.value.notifications.filter((notification) => notification.tone === 'info');
            notificationsOpen.value = false;
            try {
                await apiCommand('/api/notifications/read-all', { method: 'PATCH' });
            }
            catch (error) {
                console.warn('Could not mark notifications as read.', error);
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
        function openChatWithPrompt(prompt) {
            chatOpen.value = true;
            if (prompt)
                void submitChat(prompt);
            else
                void scrollChatToBottom();
        }
        async function submitChat(explicitPrompt) {
            const prompt = (explicitPrompt ?? chatDraft.value).trim();
            if (!prompt || isAssistantThinking.value)
                return;
            chatMessages.value.push({ id: `user-${Date.now()}`, role: 'user', text: prompt });
            chatDraft.value = '';
            isAssistantThinking.value = true;
            // Extract project ID if tagged with @
            let taggedProjectId = null;
            const tagMatch = prompt.match(/@([\w\s]+)/);
            if (tagMatch) {
                const taggedName = tagMatch[1].trim().toLowerCase();
                const project = projects.value.find(p => p.name.toLowerCase() === taggedName);
                if (project) {
                    taggedProjectId = project.id;
                }
            }
            try {
                const assistantMsgId = `assistant-${Date.now()}`;
                chatMessages.value.push({ id: assistantMsgId, role: 'assistant', text: '' });
                const response = await fetch('/api/ai/chat/stream', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        message: prompt,
                        projectId: taggedProjectId ?? selectedProject.value?.id ?? null,
                    }),
                });
                if (!response.ok)
                    throw new Error('Streaming failed');
                const reader = response.body?.getReader();
                const decoder = new TextDecoder();
                let assistantReply = '';
                if (reader) {
                    isAssistantThinking.value = false;
                    while (true) {
                        const { done, value } = await reader.read();
                        if (done)
                            break;
                        const chunk = decoder.decode(value, { stream: true });
                        assistantReply += chunk;
                        const msgIndex = chatMessages.value.findIndex(m => m.id === assistantMsgId);
                        if (msgIndex !== -1) {
                            chatMessages.value[msgIndex].text = assistantReply;
                        }
                        void scrollChatToBottom();
                    }
                }
            }
            catch (error) {
                chatMessages.value.push({ id: `assistant-${Date.now()}`, role: 'assistant', text: createAssistantReply(prompt) });
            }
            finally {
                isAssistantThinking.value = false;
            }
        }
        async function scrollChatToBottom() {
            await nextTick();
            chatBodyRef.value?.scrollTo({ top: chatBodyRef.value.scrollHeight, behavior: 'smooth' });
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
            const payload = text ? JSON.parse(text) : null;
            if (!response.ok) {
                throw new Error(payload?.error ?? `Request failed with status ${response.status}`);
            }
            return payload;
        }
        async function apiResult(url, options = {}) {
            const result = await apiJson(url, options);
            if (!result.isSuccess || result.data == null) {
                throw new Error(result.error ?? 'Request failed.');
            }
            return result.data;
        }
        async function apiCommand(url, options = {}) {
            const result = await apiJson(url, options);
            if ('isSuccess' in result && !result.isSuccess) {
                throw new Error(result.error ?? 'Request failed.');
            }
        }
        function tasksByStatus(status) {
            return selectedProjectTasks.value.filter((task) => task.status === status);
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
        function createAssistantReply(prompt) {
            const query = prompt.toLowerCase();
            const project = selectedProject.value;
            const keywords = [
                'risk', 'rủi ro', 'rui ro', 'summary', 'tóm tắt', 'tom tat', 'overdue', 'quá hạn', 'qua han',
                'priority', 'ưu tiên', 'u tien', 'assignment', 'phân công', 'phan cong', 'task', 'công việc', 'cong viec',
                'project', 'dự án', 'du an', 'status', 'trạng thái', 'trang thai', 'deadline', 'hạn', 'han chot',
                'progress', 'tiến độ', 'tien do', 'member', 'thành viên', 'thanh vien', 'done', 'hoàn thành', 'hoan thanh',
                'todo', 'cần làm', 'can lam', 'doing', 'đang làm', 'dang lam'
            ];
            const isRelevant = keywords.some(k => query.includes(k));
            if (!isRelevant) {
                return 'Tao đéo biết';
            }
            if (query.includes('risk') || query.includes('rủi ro') || query.includes('rui ro')) {
                return `${overdueTasks.value} tasks are overdue across the workspace. Review ${project?.name ?? 'the highest-risk project'} first.`;
            }
            const task = selectedProjectTasks.value.find((item) => item.status !== 'Done');
            return task ? `Next candidate: "${task.title}" in ${project?.name}.` : selectedProjectSummary.value;
        }
        function showActionNotice(message) {
            actionNotice.value = message;
            if (actionNoticeTimer)
                window.clearTimeout(actionNoticeTimer);
            actionNoticeTimer = window.setTimeout(() => {
                actionNotice.value = '';
            }, 3800);
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
            return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
        }
        function errorMessage(error) {
            return error instanceof Error ? error.message : 'Request failed.';
        }
        provide(dashboardContextKey, {
            actionNotice,
            activeProjectCards,
            activeProjectId,
            activeProjectTab,
            activeTaskMenu,
            addMember,
            archivedProjectCards,
            assignedTaskCards,
            attachments,
            beginEditProject,
            beginEditTask,
            chatDraft,
            chatMessages,
            chatOpen,
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
            isAssistantThinking,
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
            openChatWithPrompt,
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
            projectSuggestions,
            projects,
            quickPrompts,
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
            showProjectSuggestions,
            statusColumns,
            statusTone,
            submitChat,
            submitComment,
            summaryCards,
            tabs,
            tagProject,
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
        });
        return (_ctx, _cache) => {
            const _component_RouterView = resolveComponent("RouterView");
            return (openBlock(), createBlock(_sfc_main$1, {
                "nav-items": navigation,
                "notification-count": notificationCount.value,
                "user-name": currentUser.value?.fullName || currentUser.value?.email || 'Qaly user',
                "user-initials": initials(currentUser.value?.fullName || currentUser.value?.email || 'QU'),
                onNotifications: _cache[4] || (_cache[4] = ($event) => (notificationsOpen.value = !notificationsOpen.value)),
                onAssistant: _cache[5] || (_cache[5] = ($event) => (openChatWithPrompt())),
                onLogout: logout
            }, {
                default: withCtx(() => [
                    createVNode(_component_RouterView),
                    (notificationsOpen.value)
                        ? (openBlock(), createElementBlock("div", _hoisted_1, [
                            createBaseVNode("div", _hoisted_2, [
                                _cache[6] || (_cache[6] = createBaseVNode("div", null, [
                                    createBaseVNode("span", null, "Notifications"),
                                    createBaseVNode("h2", null, "Current signals")
                                ], -1)),
                                createBaseVNode("div", _hoisted_3, [
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
                                    createBaseVNode("div", _hoisted_4, [
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
                    (actionNotice.value)
                        ? (openBlock(), createElementBlock("div", _hoisted_6, toDisplayString(actionNotice.value), 1))
                        : createCommentVNode("", true),
                    createBaseVNode("aside", {
                        class: normalizeClass(["chat-drawer glass-card", { 'is-open': chatOpen.value }])
                    }, [
                        createBaseVNode("div", _hoisted_7, [
                            createBaseVNode("div", _hoisted_8, [
                                createVNode(_sfc_main$3, { size: "medium" }),
                                _cache[7] || (_cache[7] = createBaseVNode("div", null, [
                                    createBaseVNode("span", null, "AI assistant"),
                                    createBaseVNode("h2", null, "Qaly assistant")
                                ], -1))
                            ]),
                            createBaseVNode("button", {
                                class: "icon-button",
                                type: "button",
                                onClick: _cache[1] || (_cache[1] = ($event) => (chatOpen.value = false))
                            }, [
                                createVNode(unref(X), { size: 18 })
                            ])
                        ]),
                        createBaseVNode("div", {
                            ref_key: "chatBodyRef",
                            ref: chatBodyRef,
                            class: "chat-drawer__body no-scrollbar"
                        }, [
                            (openBlock(true), createElementBlock(Fragment, null, renderList(chatMessages.value, (message) => {
                                return (openBlock(), createElementBlock("article", {
                                    key: message.id,
                                    class: normalizeClass(["chat-message", `chat-message--${message.role}`])
                                }, [
                                    (message.role === 'assistant')
                                        ? (openBlock(), createElementBlock("div", _hoisted_9, [
                                            createVNode(_sfc_main$3, { size: "small" })
                                        ]))
                                        : createCommentVNode("", true),
                                    createBaseVNode("div", {
                                        class: normalizeClass(["chat-bubble", `chat-bubble--${message.role}`])
                                    }, toDisplayString(message.text), 3),
                                    (message.role === 'user')
                                        ? (openBlock(), createElementBlock("div", _hoisted_10, toDisplayString(initials(currentUser.value?.fullName || currentUser.value?.email || 'QU')), 1))
                                        : createCommentVNode("", true)
                                ], 2));
                            }), 128)),
                            (isAssistantThinking.value)
                                ? (openBlock(), createElementBlock("article", _hoisted_11, [
                                    createBaseVNode("div", _hoisted_12, [
                                        createVNode(_sfc_main$3, { size: "small" })
                                    ]),
                                    _cache[8] || (_cache[8] = createBaseVNode("div", {
                                        class: "chat-bubble chat-bubble--assistant chat-bubble--thinking",
                                        "aria-label": "Assistant is thinking"
                                    }, [
                                        createBaseVNode("span"),
                                        createBaseVNode("span"),
                                        createBaseVNode("span")
                                    ], -1))
                                ]))
                                : createCommentVNode("", true)
                        ], 512),
                        createBaseVNode("div", _hoisted_13, [
                            (openBlock(true), createElementBlock(Fragment, null, renderList(quickPrompts.value, (prompt) => {
                                return (openBlock(), createElementBlock("button", {
                                    key: prompt,
                                    type: "button",
                                    class: "prompt-chip",
                                    disabled: isAssistantThinking.value,
                                    onClick: ($event) => (submitChat(prompt))
                                }, toDisplayString(prompt), 9, _hoisted_14));
                            }), 128))
                        ]),
                        (showProjectSuggestions.value && projectSuggestions.value.length > 0)
                            ? (openBlock(), createElementBlock("div", _hoisted_15, [
                                (openBlock(true), createElementBlock(Fragment, null, renderList(projectSuggestions.value, (p) => {
                                    return (openBlock(), createElementBlock("button", {
                                        key: p.id,
                                        type: "button",
                                        onClick: ($event) => (tagProject(p))
                                    }, [
                                        createBaseVNode("strong", null, "@" + toDisplayString(p.name), 1),
                                        createBaseVNode("span", null, toDisplayString(p.status), 1)
                                    ], 8, _hoisted_16));
                                }), 128))
                            ]))
                            : createCommentVNode("", true),
                        createBaseVNode("form", {
                            class: "chat-drawer__composer",
                            onSubmit: _cache[3] || (_cache[3] = withModifiers(($event) => (submitChat()), ["prevent"]))
                        }, [
                            withDirectives(createBaseVNode("input", {
                                "onUpdate:modelValue": _cache[2] || (_cache[2] = ($event) => ((chatDraft).value = $event)),
                                type: "text",
                                disabled: isAssistantThinking.value,
                                placeholder: "Ask about risk, priority, work... Use @ to tag project"
                            }, null, 8, _hoisted_17), [
                                [vModelText, chatDraft.value]
                            ]),
                            createBaseVNode("button", {
                                class: "primary-button",
                                type: "submit",
                                disabled: isAssistantThinking.value || !chatDraft.value.trim()
                            }, "Ask", 8, _hoisted_18)
                        ], 32)
                    ], 2)
                ]),
                _: 1
            }, 8, ["notification-count", "user-name", "user-initials"]));
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
const target = document.getElementById('qaly-dashboard-app');
if (target) {
    createApp(_sfc_main).use(router).mount(target);
}
export { _sfc_main$3 as _, useDashboardContext as u };
//# sourceMappingURL=main.js.map
