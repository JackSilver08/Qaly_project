import { d as defineComponent, c as createElementBlock, a as createBaseVNode, g as createVNode, i as unref, t as toDisplayString, n as normalizeClass, j as createCommentVNode, I as normalizeStyle, o as openBlock, y as withDirectives, K as vModelSelect, F as Fragment, b as renderList, l as ref, H as createTextVNode, z as vModelText, G as withKeys, e as createBlock, x as withModifiers, J as isRef } from './vendor-vue.js';
import { _ as _sfc_main$5, u as useDashboardContext } from './main.js';
import { A as ArrowLeft, L as LayoutDashboard, g as UserPlus, h as Mail, i as Shield, T as Trash2, P as Plus, S as Search, j as FileText, f as Pencil, k as MessageSquare, l as Send, m as Ellipsis } from './vendor-icons.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
const _hoisted_1$4 = { class: "project-detail-header glass-card reveal" };
const _hoisted_2$4 = { class: "project-detail-header__top" };
const _hoisted_3$4 = { class: "back-icon" };
const _hoisted_4$4 = { class: "project-detail-header__actions" };
const _hoisted_5$4 = { class: "project-detail-header__main" };
const _hoisted_6$4 = { class: "project-info" };
const _hoisted_7$4 = { class: "project-info__title-row" };
const _hoisted_8$4 = { class: "project-icon-box" };
const _hoisted_9$4 = { class: "title-stack" };
const _hoisted_10$4 = {
    key: 0,
    class: "project-info__desc"
};
const _hoisted_11$4 = { class: "project-progress-summary" };
const _hoisted_12$4 = { class: "progress-info" };
const _hoisted_13$4 = { class: "progress-bar-rail" };
const _sfc_main$4 = /*@__PURE__*/ defineComponent({
    __name: 'ProjectDetailHeader',
    props: {
        projectName: {},
        description: {},
        statusLabel: {},
        statusTone: {},
        progressLabel: {},
        progressPercentage: {}
    },
    emits: ["back", "assistant"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("header", _hoisted_1$4, [
                createBaseVNode("div", _hoisted_2$4, [
                    createBaseVNode("button", {
                        class: "back-link",
                        type: "button",
                        onClick: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('back')))
                    }, [
                        createBaseVNode("div", _hoisted_3$4, [
                            createVNode(unref(ArrowLeft), { size: 18 })
                        ]),
                        _cache[2] || (_cache[2] = createBaseVNode("span", null, "Quay lại danh sách", -1))
                    ]),
                    createBaseVNode("div", _hoisted_4$4, [
                        createBaseVNode("button", {
                            class: "assistant-button",
                            type: "button",
                            onClick: _cache[1] || (_cache[1] = ($event) => (_ctx.$emit('assistant')))
                        }, [
                            createVNode(_sfc_main$5, { size: "launcher" }),
                            _cache[3] || (_cache[3] = createBaseVNode("span", null, "Qaly AI Assistant", -1))
                        ])
                    ])
                ]),
                createBaseVNode("div", _hoisted_5$4, [
                    createBaseVNode("div", _hoisted_6$4, [
                        createBaseVNode("div", _hoisted_7$4, [
                            createBaseVNode("div", _hoisted_8$4, [
                                createVNode(unref(LayoutDashboard), {
                                    size: 24,
                                    class: "text-primary"
                                })
                            ]),
                            createBaseVNode("div", _hoisted_9$4, [
                                createBaseVNode("h1", null, toDisplayString(__props.projectName), 1),
                                createBaseVNode("span", {
                                    class: normalizeClass(`project-status project-status--${__props.statusTone}`)
                                }, toDisplayString(__props.statusLabel), 3)
                            ])
                        ]),
                        (__props.description)
                            ? (openBlock(), createElementBlock("p", _hoisted_10$4, toDisplayString(__props.description), 1))
                            : createCommentVNode("", true)
                    ]),
                    createBaseVNode("div", _hoisted_11$4, [
                        createBaseVNode("div", _hoisted_12$4, [
                            _cache[4] || (_cache[4] = createBaseVNode("span", null, "Tiến độ hoàn thành", -1)),
                            createBaseVNode("strong", null, toDisplayString(__props.progressLabel), 1)
                        ]),
                        createBaseVNode("div", _hoisted_13$4, [
                            createBaseVNode("div", {
                                class: "progress-bar-fill",
                                style: normalizeStyle({ width: `${__props.progressPercentage}%` })
                            }, [...(_cache[5] || (_cache[5] = [
                                    createBaseVNode("div", { class: "progress-shimmer" }, null, -1)
                                ]))], 4)
                        ])
                    ])
                ])
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
const ProjectDetailHeader = /*#__PURE__*/ _export_sfc(_sfc_main$4, [['__scopeId', "data-v-3ceb04d7"]]);
const _hoisted_1$3 = { class: "members-tab-content glass-card" };
const _hoisted_2$3 = { class: "panel-heading" };
const _hoisted_3$3 = {
    key: 0,
    class: "add-member-form glass-card reveal"
};
const _hoisted_4$3 = { class: "form-row" };
const _hoisted_5$3 = ["value"];
const _hoisted_6$3 = ["disabled"];
const _hoisted_7$3 = { class: "members-list" };
const _hoisted_8$3 = { class: "member-avatar" };
const _hoisted_9$3 = { class: "member-info" };
const _hoisted_10$3 = { class: "member-meta" };
const _hoisted_11$3 = { class: "member-email" };
const _hoisted_12$3 = { class: "member-role-actions" };
const _hoisted_13$3 = {
    key: 0,
    class: "role-selector"
};
const _hoisted_14$3 = ["value", "onChange"];
const _hoisted_15$2 = ["value"];
const _hoisted_16$2 = ["onClick"];
const _hoisted_17$2 = {
    key: 0,
    class: "empty-state"
};
const _sfc_main$3 = /*@__PURE__*/ defineComponent({
    __name: 'ProjectMembersTab',
    props: {
        members: {},
        users: {},
        isAdmin: { type: Boolean }
    },
    emits: ["add", "remove", "update-role"],
    setup(__props, { emit: __emit }) {
        const emit = __emit;
        const showAddForm = ref(false);
        const selectedUserId = ref('');
        function handleAdd() {
            if (!selectedUserId.value)
                return;
            emit('add', selectedUserId.value);
            selectedUserId.value = '';
            showAddForm.value = false;
        }
        const roles = ['Manager', 'Member', 'Viewer'];
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1$3, [
                createBaseVNode("div", _hoisted_2$3, [
                    _cache[4] || (_cache[4] = createBaseVNode("div", null, [
                        createBaseVNode("span", null, "Members"),
                        createBaseVNode("h2", null, "DANH SÁCH THÀNH VIÊN")
                    ], -1)),
                    (__props.isAdmin)
                        ? (openBlock(), createElementBlock("button", {
                            key: 0,
                            class: "primary-button primary-button--compact",
                            type: "button",
                            onClick: _cache[0] || (_cache[0] = ($event) => (showAddForm.value = !showAddForm.value))
                        }, [
                            createVNode(unref(UserPlus), { size: 16 }),
                            _cache[3] || (_cache[3] = createBaseVNode("span", null, "Thêm thành viên", -1))
                        ]))
                        : createCommentVNode("", true)
                ]),
                (showAddForm.value)
                    ? (openBlock(), createElementBlock("div", _hoisted_3$3, [
                        _cache[6] || (_cache[6] = createBaseVNode("h3", null, "Thêm thành viên mới", -1)),
                        createBaseVNode("div", _hoisted_4$3, [
                            withDirectives(createBaseVNode("select", {
                                "onUpdate:modelValue": _cache[1] || (_cache[1] = ($event) => ((selectedUserId).value = $event))
                            }, [
                                _cache[5] || (_cache[5] = createBaseVNode("option", {
                                    value: "",
                                    disabled: ""
                                }, "Chọn người dùng...", -1)),
                                (openBlock(true), createElementBlock(Fragment, null, renderList(__props.users.filter(u => !__props.members.some(m => m.id === u.id)), (user) => {
                                    return (openBlock(), createElementBlock("option", {
                                        key: user.id,
                                        value: user.id
                                    }, toDisplayString(user.fullName) + " (" + toDisplayString(user.email) + ") ", 9, _hoisted_5$3));
                                }), 128))
                            ], 512), [
                                [vModelSelect, selectedUserId.value]
                            ]),
                            createBaseVNode("button", {
                                class: "primary-button",
                                type: "button",
                                disabled: !selectedUserId.value,
                                onClick: handleAdd
                            }, "Thêm", 8, _hoisted_6$3),
                            createBaseVNode("button", {
                                class: "text-button",
                                type: "button",
                                onClick: _cache[2] || (_cache[2] = ($event) => (showAddForm.value = false))
                            }, "Hủy")
                        ])
                    ]))
                    : createCommentVNode("", true),
                createBaseVNode("div", _hoisted_7$3, [
                    (openBlock(true), createElementBlock(Fragment, null, renderList(__props.members, (member) => {
                        return (openBlock(), createElementBlock("article", {
                            key: member.id,
                            class: "member-item"
                        }, [
                            createBaseVNode("div", _hoisted_8$3, toDisplayString(member.initials), 1),
                            createBaseVNode("div", _hoisted_9$3, [
                                createBaseVNode("strong", null, toDisplayString(member.fullName), 1),
                                createBaseVNode("div", _hoisted_10$3, [
                                    createBaseVNode("span", _hoisted_11$3, [
                                        createVNode(unref(Mail), { size: 14 }),
                                        createTextVNode(" " + toDisplayString(member.email), 1)
                                    ])
                                ])
                            ]),
                            createBaseVNode("div", _hoisted_12$3, [
                                (__props.isAdmin && member.role !== 'Owner')
                                    ? (openBlock(), createElementBlock("div", _hoisted_13$3, [
                                        createBaseVNode("select", {
                                            value: member.role,
                                            onChange: e => _ctx.$emit('update-role', member.id, e.target.value)
                                        }, [
                                            (openBlock(), createElementBlock(Fragment, null, renderList(roles, (role) => {
                                                return createBaseVNode("option", {
                                                    key: role,
                                                    value: role
                                                }, toDisplayString(role), 9, _hoisted_15$2);
                                            }), 64))
                                        ], 40, _hoisted_14$3)
                                    ]))
                                    : (openBlock(), createElementBlock("span", {
                                        key: 1,
                                        class: normalizeClass(`role-badge role-badge--${member.role.toLowerCase()}`)
                                    }, [
                                        createVNode(unref(Shield), { size: 14 }),
                                        createTextVNode(" " + toDisplayString(member.role), 1)
                                    ], 2)),
                                (__props.isAdmin && member.role !== 'Owner')
                                    ? (openBlock(), createElementBlock("button", {
                                        key: 2,
                                        class: "icon-button icon-button--small",
                                        style: { "color": "var(--peach-500)", "margin-left": "12px" },
                                        type: "button",
                                        onClick: ($event) => (_ctx.$emit('remove', member.id))
                                    }, [
                                        createVNode(unref(Trash2), { size: 16 })
                                    ], 8, _hoisted_16$2))
                                    : createCommentVNode("", true)
                            ])
                        ]));
                    }), 128)),
                    (__props.members.length === 0)
                        ? (openBlock(), createElementBlock("div", _hoisted_17$2, " Không có thành viên nào trong dự án này. "))
                        : createCommentVNode("", true)
                ])
            ]));
        };
    }
});
const ProjectMembersTab = /*#__PURE__*/ _export_sfc(_sfc_main$3, [['__scopeId', "data-v-8e6e36c9"]]);
const _hoisted_1$2 = { class: "stats-tab-content" };
const _hoisted_2$2 = { class: "stats-grid" };
const _hoisted_3$2 = { class: "stat-card glass-card" };
const _hoisted_4$2 = { class: "stat-card__value" };
const _hoisted_5$2 = { class: "stat-card glass-card" };
const _hoisted_6$2 = { class: "stat-card__value" };
const _hoisted_7$2 = { class: "stat-card glass-card" };
const _hoisted_8$2 = { class: "stat-card__value" };
const _hoisted_9$2 = { class: "stat-card glass-card" };
const _hoisted_10$2 = {
    class: "stat-card__value",
    style: { "color": "var(--mint-500)" }
};
const _hoisted_11$2 = { class: "stat-card glass-card" };
const _hoisted_12$2 = { class: "stat-card__value" };
const _hoisted_13$2 = { class: "stat-card__value" };
const _hoisted_14$2 = { class: "charts-row" };
const _hoisted_15$1 = { class: "chart-card glass-card" };
const _hoisted_16$1 = {
    key: 0,
    class: "donut-chart-wrapper"
};
const _hoisted_17$1 = { class: "chart-legend" };
const _hoisted_18$1 = { class: "legend-item" };
const _hoisted_19$1 = { class: "legend-item" };
const _hoisted_20$1 = {
    key: 1,
    class: "empty-state"
};
const _hoisted_21$1 = { class: "chart-card glass-card progress-overview" };
const _hoisted_22$1 = {
    key: 0,
    class: "progress-details"
};
const _hoisted_23$1 = { class: "progress-row" };
const _hoisted_24$1 = { class: "bar-rail" };
const _hoisted_25$1 = { class: "progress-row" };
const _hoisted_26$1 = { class: "bar-rail" };
const _hoisted_27$1 = { class: "progress-row" };
const _hoisted_28$1 = { class: "bar-rail" };
const _hoisted_29$1 = {
    key: 1,
    class: "empty-state"
};
const _sfc_main$2 = /*@__PURE__*/ defineComponent({
    __name: 'ProjectStatsTab',
    props: {
        stats: {}
    },
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1$2, [
                createBaseVNode("div", _hoisted_2$2, [
                    createBaseVNode("div", _hoisted_3$2, [
                        _cache[0] || (_cache[0] = createBaseVNode("span", { class: "stat-card__label" }, "Tổng số Task", -1)),
                        createBaseVNode("strong", _hoisted_4$2, toDisplayString(__props.stats.total), 1)
                    ]),
                    createBaseVNode("div", _hoisted_5$2, [
                        _cache[1] || (_cache[1] = createBaseVNode("span", { class: "stat-card__label" }, "Đang làm", -1)),
                        createBaseVNode("strong", _hoisted_6$2, toDisplayString(__props.stats.inProgress), 1)
                    ]),
                    createBaseVNode("div", _hoisted_7$2, [
                        _cache[2] || (_cache[2] = createBaseVNode("span", { class: "stat-card__label" }, "Đang Review", -1)),
                        createBaseVNode("strong", _hoisted_8$2, toDisplayString(__props.stats.inReview), 1)
                    ]),
                    createBaseVNode("div", _hoisted_9$2, [
                        _cache[3] || (_cache[3] = createBaseVNode("span", { class: "stat-card__label" }, "Đã hoàn thành", -1)),
                        createBaseVNode("strong", _hoisted_10$2, toDisplayString(__props.stats.done), 1)
                    ]),
                    createBaseVNode("div", _hoisted_11$2, [
                        _cache[4] || (_cache[4] = createBaseVNode("span", { class: "stat-card__label" }, "Tỷ lệ hoàn thành", -1)),
                        createBaseVNode("strong", _hoisted_12$2, toDisplayString(__props.stats.completionRate) + "%", 1)
                    ]),
                    createBaseVNode("div", {
                        class: normalizeClass(["stat-card glass-card", { 'is-risk': __props.stats.overdue > 0 }])
                    }, [
                        _cache[5] || (_cache[5] = createBaseVNode("span", { class: "stat-card__label" }, "Quá hạn", -1)),
                        createBaseVNode("strong", _hoisted_13$2, toDisplayString(__props.stats.overdue), 1)
                    ], 2)
                ]),
                createBaseVNode("div", _hoisted_14$2, [
                    createBaseVNode("div", _hoisted_15$1, [
                        _cache[8] || (_cache[8] = createBaseVNode("h3", null, "Phân bổ trạng thái", -1)),
                        (__props.stats.total > 0)
                            ? (openBlock(), createElementBlock("div", _hoisted_16$1, [
                                createBaseVNode("div", {
                                    class: "donut-chart",
                                    style: normalizeStyle({ '--progress': `${__props.stats.completionRate}%` })
                                }, [
                                    createBaseVNode("strong", null, toDisplayString(__props.stats.completionRate) + "%", 1)
                                ], 4),
                                createBaseVNode("div", _hoisted_17$1, [
                                    createBaseVNode("div", _hoisted_18$1, [
                                        _cache[6] || (_cache[6] = createBaseVNode("span", { class: "dot done" }, null, -1)),
                                        createTextVNode(" Đã xong (" + toDisplayString(__props.stats.done) + ")", 1)
                                    ]),
                                    createBaseVNode("div", _hoisted_19$1, [
                                        _cache[7] || (_cache[7] = createBaseVNode("span", { class: "dot active" }, null, -1)),
                                        createTextVNode(" Đang làm (" + toDisplayString(__props.stats.inProgress + __props.stats.inReview + __props.stats.todo) + ")", 1)
                                    ])
                                ])
                            ]))
                            : (openBlock(), createElementBlock("div", _hoisted_20$1, "Chưa đủ dữ liệu thống kê"))
                    ]),
                    createBaseVNode("div", _hoisted_21$1, [
                        _cache[12] || (_cache[12] = createBaseVNode("h3", null, "Tiến độ dự án", -1)),
                        (__props.stats.total > 0)
                            ? (openBlock(), createElementBlock("div", _hoisted_22$1, [
                                createBaseVNode("div", _hoisted_23$1, [
                                    _cache[9] || (_cache[9] = createBaseVNode("span", null, "Hoàn thành", -1)),
                                    createBaseVNode("div", _hoisted_24$1, [
                                        createBaseVNode("div", {
                                            class: "bar-fill done",
                                            style: normalizeStyle({ width: `${__props.stats.total > 0 ? (__props.stats.done / __props.stats.total) * 100 : 0}%` })
                                        }, null, 4)
                                    ])
                                ]),
                                createBaseVNode("div", _hoisted_25$1, [
                                    _cache[10] || (_cache[10] = createBaseVNode("span", null, "Đang thực hiện", -1)),
                                    createBaseVNode("div", _hoisted_26$1, [
                                        createBaseVNode("div", {
                                            class: "bar-fill doing",
                                            style: normalizeStyle({ width: `${__props.stats.total > 0 ? ((__props.stats.inProgress + __props.stats.inReview) / __props.stats.total) * 100 : 0}%` })
                                        }, null, 4)
                                    ])
                                ]),
                                createBaseVNode("div", _hoisted_27$1, [
                                    _cache[11] || (_cache[11] = createBaseVNode("span", null, "Chưa bắt đầu", -1)),
                                    createBaseVNode("div", _hoisted_28$1, [
                                        createBaseVNode("div", {
                                            class: "bar-fill todo",
                                            style: normalizeStyle({ width: `${__props.stats.total > 0 ? (__props.stats.todo / __props.stats.total) * 100 : 0}%` })
                                        }, null, 4)
                                    ])
                                ])
                            ]))
                            : (openBlock(), createElementBlock("div", _hoisted_29$1, "Chưa đủ dữ liệu thống kê"))
                    ])
                ])
            ]));
        };
    }
});
const ProjectStatsTab = /*#__PURE__*/ _export_sfc(_sfc_main$2, [['__scopeId', "data-v-cde34b5f"]]);
const _hoisted_1$1 = { class: "wiki-tab-content glass-card" };
const _hoisted_2$1 = { class: "panel-heading" };
const _hoisted_3$1 = {
    key: 0,
    class: "wiki-add-form glass-card reveal"
};
const _hoisted_4$1 = { class: "form-row" };
const _hoisted_5$1 = ["disabled"];
const _hoisted_6$1 = {
    key: 1,
    class: "wiki-list"
};
const _hoisted_7$1 = { class: "wiki-search-bar" };
const _hoisted_8$1 = { class: "wiki-item__icon" };
const _hoisted_9$1 = { class: "wiki-item__main" };
const _hoisted_10$1 = {
    key: 0,
    class: "wiki-item__actions"
};
const _hoisted_11$1 = {
    class: "icon-button icon-button--small",
    type: "button",
    title: "Sửa"
};
const _hoisted_12$1 = ["onClick"];
const _hoisted_13$1 = {
    key: 2,
    class: "wiki-empty"
};
const _hoisted_14$1 = { class: "wiki-empty__icon" };
const _sfc_main$1 = /*@__PURE__*/ defineComponent({
    __name: 'ProjectWikiTab',
    props: {
        projectName: {},
        isAdmin: { type: Boolean }
    },
    setup(__props) {
        const { wikiPages, createWikiPage, deleteWikiPage, updateWikiPage, formatDate } = useDashboardContext();
        const showAddForm = ref(false);
        const newPageTitle = ref('');
        async function handleCreate() {
            if (!newPageTitle.value.trim())
                return;
            await createWikiPage(newPageTitle.value.trim());
            newPageTitle.value = '';
            showAddForm.value = false;
        }
        async function handleDelete(id) {
            if (!confirm('Bạn có chắc chắn muốn xóa trang Wiki này?'))
                return;
            await deleteWikiPage(id);
        }
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1$1, [
                createBaseVNode("div", _hoisted_2$1, [
                    createBaseVNode("div", null, [
                        _cache[4] || (_cache[4] = createBaseVNode("span", null, "Knowledge Base", -1)),
                        createBaseVNode("h2", null, toDisplayString(__props.projectName) + " Wiki", 1)
                    ]),
                    (__props.isAdmin)
                        ? (openBlock(), createElementBlock("button", {
                            key: 0,
                            class: "primary-button primary-button--compact",
                            type: "button",
                            onClick: _cache[0] || (_cache[0] = ($event) => (showAddForm.value = !showAddForm.value))
                        }, [
                            createVNode(unref(Plus), { size: 16 }),
                            _cache[5] || (_cache[5] = createBaseVNode("span", null, "Tạo trang mới", -1))
                        ]))
                        : createCommentVNode("", true)
                ]),
                (showAddForm.value)
                    ? (openBlock(), createElementBlock("div", _hoisted_3$1, [
                        _cache[6] || (_cache[6] = createBaseVNode("h3", null, "Tạo trang Wiki mới", -1)),
                        createBaseVNode("div", _hoisted_4$1, [
                            withDirectives(createBaseVNode("input", {
                                "onUpdate:modelValue": _cache[1] || (_cache[1] = ($event) => ((newPageTitle).value = $event)),
                                type: "text",
                                placeholder: "Tiêu đề trang...",
                                onKeyup: withKeys(handleCreate, ["enter"])
                            }, null, 544), [
                                [vModelText, newPageTitle.value]
                            ]),
                            createBaseVNode("button", {
                                class: "primary-button",
                                type: "button",
                                disabled: !newPageTitle.value.trim(),
                                onClick: handleCreate
                            }, "Tạo", 8, _hoisted_5$1),
                            createBaseVNode("button", {
                                class: "text-button",
                                type: "button",
                                onClick: _cache[2] || (_cache[2] = ($event) => (showAddForm.value = false))
                            }, "Hủy")
                        ])
                    ]))
                    : createCommentVNode("", true),
                (unref(wikiPages).length > 0)
                    ? (openBlock(), createElementBlock("div", _hoisted_6$1, [
                        createBaseVNode("div", _hoisted_7$1, [
                            createVNode(unref(Search), { size: 16 }),
                            _cache[7] || (_cache[7] = createBaseVNode("input", {
                                type: "text",
                                placeholder: "Tìm kiếm trang Wiki..."
                            }, null, -1))
                        ]),
                        (openBlock(true), createElementBlock(Fragment, null, renderList(unref(wikiPages), (page) => {
                            return (openBlock(), createElementBlock("article", {
                                key: page.id,
                                class: "wiki-item"
                            }, [
                                createBaseVNode("div", _hoisted_8$1, [
                                    createVNode(unref(FileText), { size: 20 })
                                ]),
                                createBaseVNode("div", _hoisted_9$1, [
                                    createBaseVNode("strong", null, toDisplayString(page.title), 1),
                                    createBaseVNode("span", null, "Cập nhật bởi " + toDisplayString(page.authorName) + " vào " + toDisplayString(unref(formatDate)(page.updatedAt)), 1)
                                ]),
                                (__props.isAdmin)
                                    ? (openBlock(), createElementBlock("div", _hoisted_10$1, [
                                        createBaseVNode("button", _hoisted_11$1, [
                                            createVNode(unref(Pencil), { size: 14 })
                                        ]),
                                        createBaseVNode("button", {
                                            class: "icon-button icon-button--small risk",
                                            type: "button",
                                            title: "Xóa",
                                            onClick: ($event) => (handleDelete(page.id))
                                        }, [
                                            createVNode(unref(Trash2), { size: 14 })
                                        ], 8, _hoisted_12$1)
                                    ]))
                                    : createCommentVNode("", true)
                            ]));
                        }), 128))
                    ]))
                    : (openBlock(), createElementBlock("div", _hoisted_13$1, [
                        createBaseVNode("div", _hoisted_14$1, [
                            createVNode(unref(FileText), { size: 48 })
                        ]),
                        _cache[8] || (_cache[8] = createBaseVNode("h3", null, "Chưa có trang Wiki nào", -1)),
                        _cache[9] || (_cache[9] = createBaseVNode("p", null, "Wiki là nơi lưu trữ kiến thức dự án, guideline và quy trình làm việc của team.", -1)),
                        (__props.isAdmin)
                            ? (openBlock(), createElementBlock("button", {
                                key: 0,
                                class: "secondary-button",
                                type: "button",
                                style: { "margin-top": "16px" },
                                onClick: _cache[3] || (_cache[3] = ($event) => (showAddForm.value = true))
                            }, "Bắt đầu viết Wiki"))
                            : createCommentVNode("", true)
                    ]))
            ]));
        };
    }
});
const ProjectWikiTab = /*#__PURE__*/ _export_sfc(_sfc_main$1, [['__scopeId', "data-v-485c003c"]]);
const _hoisted_1 = { class: "dashboard-scroll dashboard-scroll--embedded no-scrollbar" };
const _hoisted_2 = { class: "dashboard-main project-home-main no-scrollbar" };
const _hoisted_3 = { class: "project-tabs glass-card" };
const _hoisted_4 = ["onClick"];
const _hoisted_5 = {
    key: 1,
    class: "tab-pane reveal"
};
const _hoisted_6 = { key: 2 };
const _hoisted_7 = {
    id: "tasks",
    class: "task-board-shell glass-card"
};
const _hoisted_8 = { class: "panel-heading" };
const _hoisted_9 = ["value"];
const _hoisted_10 = ["value"];
const _hoisted_11 = ["disabled"];
const _hoisted_12 = { class: "kanban-board" };
const _hoisted_13 = { class: "kanban-column__header" };
const _hoisted_14 = ["onClick"];
const _hoisted_15 = { class: "kanban-card__top" };
const _hoisted_16 = { class: "task-card-actions" };
const _hoisted_17 = {
    key: 0,
    class: "task-menu-dropdown"
};
const _hoisted_18 = ["onClick"];
const _hoisted_19 = {
    key: 0,
    class: "dropdown-content glass-card"
};
const _hoisted_20 = ["onClick"];
const _hoisted_21 = ["onClick"];
const _hoisted_22 = { class: "kanban-card__meta" };
const _hoisted_23 = { key: 0 };
const _hoisted_24 = {
    key: 1,
    class: "project-risk"
};
const _hoisted_25 = { class: "kanban-card__actions" };
const _hoisted_26 = ["onClick"];
const _hoisted_27 = {
    key: 0,
    class: "empty-state"
};
const _hoisted_28 = {
    class: "task-detail-panel glass-card",
    style: { "margin-top": "24px" }
};
const _hoisted_29 = { class: "panel-heading" };
const _hoisted_30 = {
    key: 0,
    class: "comment-list"
};
const _hoisted_31 = { class: "attachment-panel" };
const _hoisted_32 = { class: "attachment-panel__header" };
const _hoisted_33 = { class: "attachment-upload" };
const _hoisted_34 = ["onClick"];
const _hoisted_35 = {
    key: 0,
    class: "empty-state"
};
const _hoisted_36 = { class: "comment-row__top" };
const _hoisted_37 = ["onClick"];
const _hoisted_38 = {
    key: 0,
    class: "empty-state"
};
const _hoisted_39 = ["disabled"];
const _hoisted_40 = {
    key: 1,
    class: "empty-state"
};
const _hoisted_41 = {
    key: 3,
    class: "tab-pane reveal"
};
const _hoisted_42 = {
    key: 4,
    class: "tab-pane reveal"
};
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'ProjectDetailPage',
    setup(__props) {
        const { activeProjectTab, activeTaskMenu, addMember, attachments, beginEditTask, closeProjectDetails, comments, createTask, createTaskOpen, currentUser, deleteAttachment, deleteComment, deleteTask, displayStatus, formatDate, formatFileSize, formatTime, isProjectAdmin, isTaskOverdue, moveTask, newComment, newTaskAssigneeId, newTaskDescription, newTaskDueDate, newTaskPriority, newTaskTitle, nextStatuses, openChatWithPrompt, priorities, removeMember, selectTaskInProject, selectedProject, selectedProjectMembers, selectedProjectStats, selectedTask, statusTone, statusColumns, submitComment, tabs, tasksByStatus, toggleTaskMenu, updateMemberRole, uploadAttachment, users, } = useDashboardContext();
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                createBaseVNode("div", _hoisted_2, [
                    (unref(selectedProject))
                        ? (openBlock(), createBlock(ProjectDetailHeader, {
                            key: 0,
                            "project-name": unref(selectedProject).name,
                            description: unref(selectedProject).description,
                            "status-label": unref(displayStatus)(unref(selectedProject).status),
                            "status-tone": unref(statusTone)(unref(selectedProject).status),
                            "progress-label": `${unref(selectedProject).completedTaskCount}/${unref(selectedProject).taskCount} task hoàn thành`,
                            "progress-percentage": unref(selectedProject).progressPercentage,
                            onBack: unref(closeProjectDetails),
                            onAssistant: _cache[0] || (_cache[0] = ($event) => (unref(openChatWithPrompt)()))
                        }, null, 8, ["project-name", "description", "status-label", "status-tone", "progress-label", "progress-percentage", "onBack"]))
                        : createCommentVNode("", true),
                    createBaseVNode("nav", _hoisted_3, [
                        (openBlock(true), createElementBlock(Fragment, null, renderList(unref(tabs), (tab) => {
                            return (openBlock(), createElementBlock("button", {
                                key: tab.id,
                                type: "button",
                                class: normalizeClass(["tab-link", { 'is-active': unref(activeProjectTab) === tab.id }]),
                                onClick: ($event) => (activeProjectTab.value = tab.id)
                            }, toDisplayString(tab.label), 11, _hoisted_4));
                        }), 128))
                    ]),
                    (unref(activeProjectTab) === 'stats')
                        ? (openBlock(), createElementBlock("div", _hoisted_5, [
                            createVNode(ProjectStatsTab, { stats: unref(selectedProjectStats) }, null, 8, ["stats"])
                        ]))
                        : createCommentVNode("", true),
                    (unref(activeProjectTab) === 'tasks')
                        ? (openBlock(), createElementBlock("div", _hoisted_6, [
                            createBaseVNode("section", _hoisted_7, [
                                createBaseVNode("div", _hoisted_8, [
                                    _cache[12] || (_cache[12] = createBaseVNode("div", null, [
                                        createBaseVNode("span", null, "Tasks"),
                                        createBaseVNode("h2", null, "Board")
                                    ], -1)),
                                    createBaseVNode("button", {
                                        class: "primary-button primary-button--compact",
                                        type: "button",
                                        onClick: _cache[1] || (_cache[1] = ($event) => (createTaskOpen.value = !unref(createTaskOpen)))
                                    }, [
                                        createVNode(unref(Plus), { size: 16 }),
                                        _cache[11] || (_cache[11] = createBaseVNode("span", null, "Task", -1))
                                    ])
                                ]),
                                (unref(createTaskOpen))
                                    ? (openBlock(), createElementBlock("form", {
                                        key: 0,
                                        class: "task-create-form",
                                        onSubmit: _cache[7] || (_cache[7] = withModifiers(
                                        //@ts-ignore
                                        (...args) => (unref(createTask) && unref(createTask)(...args)), ["prevent"]))
                                    }, [
                                        withDirectives(createBaseVNode("input", {
                                            "onUpdate:modelValue": _cache[2] || (_cache[2] = ($event) => (isRef(newTaskTitle) ? (newTaskTitle).value = $event : null)),
                                            type: "text",
                                            placeholder: "Task title"
                                        }, null, 512), [
                                            [vModelText, unref(newTaskTitle)]
                                        ]),
                                        withDirectives(createBaseVNode("input", {
                                            "onUpdate:modelValue": _cache[3] || (_cache[3] = ($event) => (isRef(newTaskDescription) ? (newTaskDescription).value = $event : null)),
                                            type: "text",
                                            placeholder: "Description"
                                        }, null, 512), [
                                            [vModelText, unref(newTaskDescription)]
                                        ]),
                                        withDirectives(createBaseVNode("select", {
                                            "onUpdate:modelValue": _cache[4] || (_cache[4] = ($event) => (isRef(newTaskPriority) ? (newTaskPriority).value = $event : null)),
                                            "aria-label": "Priority"
                                        }, [
                                            (openBlock(true), createElementBlock(Fragment, null, renderList(unref(priorities), (priority) => {
                                                return (openBlock(), createElementBlock("option", {
                                                    key: priority,
                                                    value: priority
                                                }, toDisplayString(priority), 9, _hoisted_9));
                                            }), 128))
                                        ], 512), [
                                            [vModelSelect, unref(newTaskPriority)]
                                        ]),
                                        withDirectives(createBaseVNode("select", {
                                            "onUpdate:modelValue": _cache[5] || (_cache[5] = ($event) => (isRef(newTaskAssigneeId) ? (newTaskAssigneeId).value = $event : null)),
                                            "aria-label": "Assignee"
                                        }, [
                                            _cache[13] || (_cache[13] = createBaseVNode("option", { value: "" }, "Unassigned", -1)),
                                            (openBlock(true), createElementBlock(Fragment, null, renderList(unref(users), (user) => {
                                                return (openBlock(), createElementBlock("option", {
                                                    key: user.id,
                                                    value: user.id
                                                }, toDisplayString(user.fullName), 9, _hoisted_10));
                                            }), 128))
                                        ], 512), [
                                            [vModelSelect, unref(newTaskAssigneeId)]
                                        ]),
                                        withDirectives(createBaseVNode("input", {
                                            "onUpdate:modelValue": _cache[6] || (_cache[6] = ($event) => (isRef(newTaskDueDate) ? (newTaskDueDate).value = $event : null)),
                                            type: "date"
                                        }, null, 512), [
                                            [vModelText, unref(newTaskDueDate)]
                                        ]),
                                        createBaseVNode("button", {
                                            class: "primary-button primary-button--compact",
                                            type: "submit",
                                            disabled: !unref(newTaskTitle).trim()
                                        }, "Create", 8, _hoisted_11)
                                    ], 32))
                                    : createCommentVNode("", true),
                                createBaseVNode("div", _hoisted_12, [
                                    (openBlock(true), createElementBlock(Fragment, null, renderList(unref(statusColumns), (status) => {
                                        return (openBlock(), createElementBlock("section", {
                                            key: status,
                                            class: "kanban-column"
                                        }, [
                                            createBaseVNode("div", _hoisted_13, [
                                                createBaseVNode("strong", null, toDisplayString(unref(displayStatus)(status)), 1),
                                                createBaseVNode("span", null, toDisplayString(unref(tasksByStatus)(status).length), 1)
                                            ]),
                                            (openBlock(true), createElementBlock(Fragment, null, renderList(unref(tasksByStatus)(status), (task) => {
                                                return (openBlock(), createElementBlock("article", {
                                                    key: task.id,
                                                    class: normalizeClass(["kanban-card", { 'is-selected': unref(selectedTask)?.id === task.id }]),
                                                    onClick: ($event) => (unref(selectTaskInProject)(task.id))
                                                }, [
                                                    createBaseVNode("div", _hoisted_15, [
                                                        createBaseVNode("strong", null, toDisplayString(task.title), 1),
                                                        createBaseVNode("div", _hoisted_16, [
                                                            createBaseVNode("span", {
                                                                class: normalizeClass(`priority priority--${task.priority.toLowerCase()}`)
                                                            }, toDisplayString(task.priority), 3),
                                                            (unref(isProjectAdmin))
                                                                ? (openBlock(), createElementBlock("div", _hoisted_17, [
                                                                    createBaseVNode("button", {
                                                                        class: "icon-button icon-button--small",
                                                                        type: "button",
                                                                        onClick: withModifiers(($event) => (unref(toggleTaskMenu)(task.id)), ["stop"])
                                                                    }, [
                                                                        createVNode(unref(Ellipsis), { size: 14 })
                                                                    ], 8, _hoisted_18),
                                                                    (unref(activeTaskMenu) === task.id)
                                                                        ? (openBlock(), createElementBlock("div", _hoisted_19, [
                                                                            createBaseVNode("button", {
                                                                                type: "button",
                                                                                onClick: withModifiers(($event) => (unref(beginEditTask)(task)), ["stop"])
                                                                            }, "Sửa", 8, _hoisted_20),
                                                                            createBaseVNode("button", {
                                                                                type: "button",
                                                                                style: { "color": "var(--peach-500)" },
                                                                                onClick: withModifiers(($event) => (unref(deleteTask)(task.id)), ["stop"])
                                                                            }, "Xóa", 8, _hoisted_21)
                                                                        ]))
                                                                        : createCommentVNode("", true)
                                                                ]))
                                                                : createCommentVNode("", true)
                                                        ])
                                                    ]),
                                                    createBaseVNode("p", null, toDisplayString(task.assigneeName || 'Unassigned') + " - " + toDisplayString(unref(formatDate)(task.dueDate)), 1),
                                                    createBaseVNode("div", _hoisted_22, [
                                                        createBaseVNode("span", null, toDisplayString(task.commentCount) + " comments", 1),
                                                        (task.isPrivate)
                                                            ? (openBlock(), createElementBlock("span", _hoisted_23, "Private"))
                                                            : createCommentVNode("", true),
                                                        (unref(isTaskOverdue)(task))
                                                            ? (openBlock(), createElementBlock("span", _hoisted_24, "Overdue"))
                                                            : createCommentVNode("", true)
                                                    ]),
                                                    createBaseVNode("div", _hoisted_25, [
                                                        (openBlock(true), createElementBlock(Fragment, null, renderList(unref(nextStatuses)(task.status), (nextStatus) => {
                                                            return (openBlock(), createElementBlock("button", {
                                                                key: nextStatus,
                                                                type: "button",
                                                                onClick: withModifiers(($event) => (unref(moveTask)(task, nextStatus)), ["stop"])
                                                            }, toDisplayString(unref(displayStatus)(nextStatus)), 9, _hoisted_26));
                                                        }), 128))
                                                    ])
                                                ], 10, _hoisted_14));
                                            }), 128)),
                                            (unref(tasksByStatus)(status).length === 0)
                                                ? (openBlock(), createElementBlock("div", _hoisted_27, "No tasks"))
                                                : createCommentVNode("", true)
                                        ]));
                                    }), 128))
                                ])
                            ]),
                            createBaseVNode("section", _hoisted_28, [
                                createBaseVNode("div", _hoisted_29, [
                                    createBaseVNode("div", null, [
                                        _cache[14] || (_cache[14] = createBaseVNode("span", null, "Task detail", -1)),
                                        createBaseVNode("h2", null, toDisplayString(unref(selectedTask)?.title ?? 'No task selected'), 1)
                                    ]),
                                    createVNode(unref(MessageSquare), { size: 18 })
                                ]),
                                (unref(selectedTask))
                                    ? (openBlock(), createElementBlock("div", _hoisted_30, [
                                        createBaseVNode("div", _hoisted_31, [
                                            createBaseVNode("div", _hoisted_32, [
                                                _cache[16] || (_cache[16] = createBaseVNode("strong", null, "Attachments", -1)),
                                                createBaseVNode("label", _hoisted_33, [
                                                    createBaseVNode("input", {
                                                        type: "file",
                                                        onChange: _cache[8] || (_cache[8] =
                                                            //@ts-ignore
                                                            (...args) => (unref(uploadAttachment) && unref(uploadAttachment)(...args)))
                                                    }, null, 32),
                                                    _cache[15] || (_cache[15] = createBaseVNode("span", null, "Upload", -1))
                                                ])
                                            ]),
                                            (openBlock(true), createElementBlock(Fragment, null, renderList(unref(attachments), (attachment) => {
                                                return (openBlock(), createElementBlock("article", {
                                                    key: attachment.id,
                                                    class: "attachment-row"
                                                }, [
                                                    createBaseVNode("div", null, [
                                                        createBaseVNode("strong", null, toDisplayString(attachment.fileName), 1),
                                                        createBaseVNode("span", null, toDisplayString(unref(formatFileSize)(attachment.fileSize)) + " - " + toDisplayString(attachment.uploadedByName), 1)
                                                    ]),
                                                    createBaseVNode("button", {
                                                        type: "button",
                                                        onClick: ($event) => (unref(deleteAttachment)(attachment))
                                                    }, "Delete", 8, _hoisted_34)
                                                ]));
                                            }), 128)),
                                            (unref(attachments).length === 0)
                                                ? (openBlock(), createElementBlock("div", _hoisted_35, "No attachments."))
                                                : createCommentVNode("", true)
                                        ]),
                                        (openBlock(true), createElementBlock(Fragment, null, renderList(unref(comments), (comment) => {
                                            return (openBlock(), createElementBlock("article", {
                                                key: comment.id,
                                                class: "comment-row"
                                            }, [
                                                createBaseVNode("div", _hoisted_36, [
                                                    createBaseVNode("strong", null, toDisplayString(comment.authorName), 1),
                                                    (unref(isProjectAdmin) || comment.authorId === unref(currentUser)?.id)
                                                        ? (openBlock(), createElementBlock("button", {
                                                            key: 0,
                                                            class: "text-button",
                                                            style: { "color": "var(--peach-500)", "padding": "0 4px", "height": "auto" },
                                                            type: "button",
                                                            onClick: ($event) => (unref(deleteComment)(comment.id))
                                                        }, " Xóa ", 8, _hoisted_37))
                                                        : createCommentVNode("", true)
                                                ]),
                                                createBaseVNode("p", null, toDisplayString(comment.content), 1),
                                                createBaseVNode("span", null, toDisplayString(unref(formatTime)(comment.createdAt)), 1)
                                            ]));
                                        }), 128)),
                                        (unref(comments).length === 0)
                                            ? (openBlock(), createElementBlock("div", _hoisted_38, "No comments yet."))
                                            : createCommentVNode("", true),
                                        createBaseVNode("form", {
                                            class: "comment-form",
                                            onSubmit: _cache[10] || (_cache[10] = withModifiers(
                                            //@ts-ignore
                                            (...args) => (unref(submitComment) && unref(submitComment)(...args)), ["prevent"]))
                                        }, [
                                            withDirectives(createBaseVNode("input", {
                                                "onUpdate:modelValue": _cache[9] || (_cache[9] = ($event) => (isRef(newComment) ? (newComment).value = $event : null)),
                                                type: "text",
                                                placeholder: "Add a comment..."
                                            }, null, 512), [
                                                [vModelText, unref(newComment)]
                                            ]),
                                            createBaseVNode("button", {
                                                class: "primary-button primary-button--compact",
                                                type: "submit",
                                                disabled: !unref(newComment).trim()
                                            }, [
                                                createVNode(unref(Send), { size: 15 })
                                            ], 8, _hoisted_39)
                                        ], 32)
                                    ]))
                                    : (openBlock(), createElementBlock("div", _hoisted_40, "Select a task from the board."))
                            ])
                        ]))
                        : createCommentVNode("", true),
                    (unref(activeProjectTab) === 'members')
                        ? (openBlock(), createElementBlock("div", _hoisted_41, [
                            createVNode(ProjectMembersTab, {
                                members: unref(selectedProjectMembers),
                                users: unref(users),
                                "is-admin": true,
                                onAdd: unref(addMember),
                                onRemove: unref(removeMember),
                                onUpdateRole: unref(updateMemberRole)
                            }, null, 8, ["members", "users", "onAdd", "onRemove", "onUpdateRole"])
                        ]))
                        : createCommentVNode("", true),
                    (unref(activeProjectTab) === 'wiki')
                        ? (openBlock(), createElementBlock("div", _hoisted_42, [
                            createVNode(ProjectWikiTab, {
                                "project-name": unref(selectedProject)?.name ?? '',
                                "is-admin": true
                            }, null, 8, ["project-name"])
                        ]))
                        : createCommentVNode("", true)
                ])
            ]));
        };
    }
});
const ProjectDetailPage = /*#__PURE__*/ _export_sfc(_sfc_main, [['__scopeId', "data-v-7fdedbcb"]]);
export { ProjectDetailPage as default };
//# sourceMappingURL=ProjectDetailPage.js.map
