import { h as UserRound, g as CircleCheck, i as CalendarDays, E as Eye, P as Pencil, j as Trash2 } from './vendor-icons.js';
import { d as defineComponent, o as openBlock, c as createElementBlock, n as normalizeClass, a as createBaseVNode, t as toDisplayString, g as createVNode, i as unref, x as createTextVNode, l as createCommentVNode, G as normalizeStyle, F as Fragment, b as renderList, e as createBlock } from './vendor-vue.js';
const _hoisted_1$1 = { class: "project-list-item__title-row" };
const _hoisted_2$1 = { class: "project-list-item__title" };
const _hoisted_3 = { class: "project-list-item__progress-label" };
const _hoisted_4 = { class: "project-list-item__meta" };
const _hoisted_5 = {
    key: 0,
    class: "project-risk"
};
const _hoisted_6 = {
    class: "project-list-item__progress",
    "aria-hidden": "true"
};
const _hoisted_7 = { class: "project-list-item__side" };
const _hoisted_8 = {
    class: "project-list-item__team",
    "aria-label": "Thành viên dự án"
};
const _hoisted_9 = {
    key: 0,
    class: "project-list-item__actions"
};
const _hoisted_10 = {
    key: 1,
    class: "project-list-item__actions"
};
const _sfc_main$1 = /*@__PURE__*/ defineComponent({
    __name: 'ProjectListItem',
    props: {
        project: {},
        isActive: { type: Boolean },
        readOnly: { type: Boolean }
    },
    emits: ["view", "edit", "delete"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("article", {
                class: normalizeClass(["project-list-item", { 'is-active': __props.isActive }])
            }, [
                createBaseVNode("button", {
                    class: "project-list-item__main",
                    type: "button",
                    onClick: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('view', __props.project.id)))
                }, [
                    createBaseVNode("div", _hoisted_1$1, [
                        createBaseVNode("div", _hoisted_2$1, [
                            createBaseVNode("strong", null, toDisplayString(__props.project.name), 1),
                            createBaseVNode("span", {
                                class: normalizeClass(`project-status project-status--${__props.project.statusTone}`)
                            }, toDisplayString(__props.project.statusLabel), 3)
                        ]),
                        createBaseVNode("span", _hoisted_3, toDisplayString(__props.project.progressPercentage) + "%", 1)
                    ]),
                    createBaseVNode("p", null, toDisplayString(__props.project.description), 1),
                    createBaseVNode("div", _hoisted_4, [
                        createBaseVNode("span", null, [
                            createVNode(unref(UserRound), { size: 13 }),
                            createTextVNode(" " + toDisplayString(__props.project.ownerName), 1)
                        ]),
                        createBaseVNode("span", null, [
                            createVNode(unref(CircleCheck), { size: 13 }),
                            createTextVNode(" " + toDisplayString(__props.project.completedTaskCount) + "/" + toDisplayString(__props.project.taskCount) + " task ", 1)
                        ]),
                        createBaseVNode("span", null, [
                            createVNode(unref(CalendarDays), { size: 13 }),
                            createTextVNode(" " + toDisplayString(__props.project.dueDateLabel), 1)
                        ]),
                        (__props.project.overdueTaskCount > 0)
                            ? (openBlock(), createElementBlock("span", _hoisted_5, toDisplayString(__props.project.overdueTaskCount) + " quá hạn ", 1))
                            : createCommentVNode("", true)
                    ]),
                    createBaseVNode("div", _hoisted_6, [
                        createBaseVNode("span", {
                            style: normalizeStyle({ width: `${__props.project.progressPercentage}%` })
                        }, null, 4)
                    ])
                ]),
                createBaseVNode("div", _hoisted_7, [
                    createBaseVNode("div", _hoisted_8, [
                        (openBlock(true), createElementBlock(Fragment, null, renderList(__props.project.memberInitials, (member, index) => {
                            return (openBlock(), createElementBlock("span", {
                                key: `${member}-${index}`
                            }, toDisplayString(member), 1));
                        }), 128))
                    ]),
                    (!__props.readOnly)
                        ? (openBlock(), createElementBlock("div", _hoisted_9, [
                            createBaseVNode("button", {
                                type: "button",
                                "aria-label": "Xem dự án",
                                onClick: _cache[1] || (_cache[1] = ($event) => (_ctx.$emit('view', __props.project.id)))
                            }, [
                                createVNode(unref(Eye), { size: 16 })
                            ]),
                            createBaseVNode("button", {
                                type: "button",
                                "aria-label": "Sửa dự án",
                                onClick: _cache[2] || (_cache[2] = ($event) => (_ctx.$emit('edit', __props.project.id)))
                            }, [
                                createVNode(unref(Pencil), { size: 16 })
                            ]),
                            createBaseVNode("button", {
                                type: "button",
                                "aria-label": "Xóa dự án",
                                onClick: _cache[3] || (_cache[3] = ($event) => (_ctx.$emit('delete', __props.project.id)))
                            }, [
                                createVNode(unref(Trash2), { size: 16 })
                            ])
                        ]))
                        : (openBlock(), createElementBlock("div", _hoisted_10, [
                            createBaseVNode("button", {
                                type: "button",
                                "aria-label": "Xem dự án",
                                onClick: _cache[4] || (_cache[4] = ($event) => (_ctx.$emit('view', __props.project.id)))
                            }, [
                                createVNode(unref(Eye), { size: 16 })
                            ])
                        ]))
                ])
            ], 2));
        };
    }
});
const _hoisted_1 = { class: "project-list-shell" };
const _hoisted_2 = {
    key: 0,
    class: "empty-state"
};
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'ProjectList',
    props: {
        projects: {},
        activeProjectId: {},
        readOnly: { type: Boolean }
    },
    emits: ["view", "edit", "delete"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                (openBlock(true), createElementBlock(Fragment, null, renderList(__props.projects, (project) => {
                    return (openBlock(), createBlock(_sfc_main$1, {
                        key: project.id,
                        project: project,
                        "is-active": project.id === __props.activeProjectId,
                        "read-only": __props.readOnly,
                        onView: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('view', $event))),
                        onEdit: _cache[1] || (_cache[1] = ($event) => (_ctx.$emit('edit', $event))),
                        onDelete: _cache[2] || (_cache[2] = ($event) => (_ctx.$emit('delete', $event)))
                    }, null, 8, ["project", "is-active", "read-only"]));
                }), 128)),
                (__props.projects.length === 0)
                    ? (openBlock(), createElementBlock("div", _hoisted_2, " Không tìm thấy dự án phù hợp với bộ lọc hiện tại. "))
                    : createCommentVNode("", true)
            ]));
        };
    }
});
export { _sfc_main as _ };
//# sourceMappingURL=ProjectList.vue_vue_type_script_setup_true_lang.js.map
