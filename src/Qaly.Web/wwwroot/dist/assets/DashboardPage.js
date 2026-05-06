import { d as defineComponent, c as createElementBlock, F as Fragment, b as renderList, o as openBlock, n as normalizeClass, a as createBaseVNode, e as createBlock, f as resolveDynamicComponent, t as toDisplayString, l as createCommentVNode, H as withKeys, y as withModifiers, g as createVNode, i as unref, I as createTextVNode, J as normalizeStyle, K as isRef, z as withDirectives, A as vModelText } from './vendor-vue.js';
import { d as Users, c as ClipboardList, F as FolderKanban, e as UserRound, f as CircleCheck, g as CalendarDays, E as Eye, P as Pencil, T as Trash2 } from './vendor-icons.js';
import { _ as _sfc_main$3 } from './ProjectToolbar.vue_vue_type_script_setup_true_lang.js';
import { u as useDashboardContext } from './main.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
const _hoisted_1$2 = {
    class: "summary-card-grid",
    "aria-label": "Tổng quan dự án"
};
const _hoisted_2$2 = { class: "summary-card__icon" };
const _sfc_main$2 = /*@__PURE__*/ defineComponent({
    __name: 'DashboardSummaryCards',
    props: {
        cards: {}
    },
    setup(__props) {
        const cardIcons = {
            projects: FolderKanban,
            tasks: ClipboardList,
            team: Users,
        };
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("section", _hoisted_1$2, [
                (openBlock(true), createElementBlock(Fragment, null, renderList(__props.cards, (card) => {
                    return (openBlock(), createElementBlock("article", {
                        key: card.key,
                        class: normalizeClass(["summary-card glass-card", `summary-card--${card.tone}`])
                    }, [
                        createBaseVNode("div", _hoisted_2$2, [
                            (openBlock(), createBlock(resolveDynamicComponent(cardIcons[card.key]), { size: 18 }))
                        ]),
                        createBaseVNode("span", null, toDisplayString(card.label), 1),
                        createBaseVNode("strong", null, toDisplayString(card.value), 1),
                        createBaseVNode("p", null, toDisplayString(card.detail), 1)
                    ], 2));
                }), 128))
            ]));
        };
    }
});
const _hoisted_1$1 = { class: "project-grid-shell" };
const _hoisted_2$1 = ["onClick", "onKeydown"];
const _hoisted_3$1 = { class: "project-grid-card__body" };
const _hoisted_4$1 = { class: "project-grid-card__title-row" };
const _hoisted_5$1 = { class: "project-grid-card__meta" };
const _hoisted_6$1 = {
    class: "project-grid-card__progress",
    "aria-hidden": "true"
};
const _hoisted_7 = { class: "project-grid-card__footer" };
const _hoisted_8 = {
    class: "project-grid-card__team",
    "aria-label": "Thành viên dự án"
};
const _hoisted_9 = { class: "project-grid-card__actions" };
const _hoisted_10 = ["onClick"];
const _hoisted_11 = ["onClick"];
const _hoisted_12 = ["onClick"];
const _hoisted_13 = {
    key: 0,
    class: "empty-state"
};
const _sfc_main$1 = /*@__PURE__*/ defineComponent({
    __name: 'ProjectGrid',
    props: {
        projects: {},
        activeProjectId: {},
        readOnly: { type: Boolean }
    },
    emits: ["view", "edit", "delete"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1$1, [
                (openBlock(true), createElementBlock(Fragment, null, renderList(__props.projects, (project) => {
                    return (openBlock(), createElementBlock("article", {
                        key: project.id,
                        class: normalizeClass(["project-grid-card", { 'is-active': project.id === __props.activeProjectId }]),
                        role: "button",
                        tabindex: "0",
                        onClick: ($event) => (_ctx.$emit('view', project.id)),
                        onKeydown: [
                            withKeys(($event) => (_ctx.$emit('view', project.id)), ["enter"]),
                            withKeys(withModifiers(($event) => (_ctx.$emit('view', project.id)), ["prevent"]), ["space"])
                        ]
                    }, [
                        createBaseVNode("span", {
                            class: normalizeClass(`project-grid-card__status-tab project-grid-card__status-tab--${project.statusTone}`)
                        }, toDisplayString(project.statusLabel), 3),
                        createBaseVNode("div", _hoisted_3$1, [
                            createBaseVNode("div", _hoisted_4$1, [
                                createBaseVNode("strong", null, toDisplayString(project.name), 1),
                                createBaseVNode("span", null, toDisplayString(project.progressPercentage) + "%", 1)
                            ]),
                            createBaseVNode("p", null, toDisplayString(project.description), 1),
                            createBaseVNode("div", _hoisted_5$1, [
                                createBaseVNode("span", null, [
                                    createVNode(unref(UserRound), { size: 13 }),
                                    createTextVNode(" " + toDisplayString(project.ownerName), 1)
                                ]),
                                createBaseVNode("span", null, [
                                    createVNode(unref(CircleCheck), { size: 13 }),
                                    createTextVNode(" " + toDisplayString(project.completedTaskCount) + "/" + toDisplayString(project.taskCount) + " task ", 1)
                                ]),
                                createBaseVNode("span", null, [
                                    createVNode(unref(CalendarDays), { size: 13 }),
                                    createTextVNode(" " + toDisplayString(project.dueDateLabel), 1)
                                ])
                            ]),
                            createBaseVNode("div", _hoisted_6$1, [
                                createBaseVNode("span", {
                                    style: normalizeStyle({ width: `${project.progressPercentage}%` })
                                }, null, 4)
                            ]),
                            createBaseVNode("div", _hoisted_7, [
                                createBaseVNode("div", _hoisted_8, [
                                    (openBlock(true), createElementBlock(Fragment, null, renderList(project.memberInitials, (member, index) => {
                                        return (openBlock(), createElementBlock("span", {
                                            key: `${member}-${index}`
                                        }, toDisplayString(member), 1));
                                    }), 128))
                                ]),
                                createBaseVNode("div", _hoisted_9, [
                                    createBaseVNode("button", {
                                        type: "button",
                                        "aria-label": "Xem dự án",
                                        onClick: withModifiers(($event) => (_ctx.$emit('view', project.id)), ["stop"])
                                    }, [
                                        createVNode(unref(Eye), { size: 16 })
                                    ], 8, _hoisted_10),
                                    (!__props.readOnly)
                                        ? (openBlock(), createElementBlock("button", {
                                            key: 0,
                                            type: "button",
                                            "aria-label": "Sửa dự án",
                                            onClick: withModifiers(($event) => (_ctx.$emit('edit', project.id)), ["stop"])
                                        }, [
                                            createVNode(unref(Pencil), { size: 16 })
                                        ], 8, _hoisted_11))
                                        : createCommentVNode("", true),
                                    (!__props.readOnly)
                                        ? (openBlock(), createElementBlock("button", {
                                            key: 1,
                                            type: "button",
                                            "aria-label": "Xóa dự án",
                                            onClick: withModifiers(($event) => (_ctx.$emit('delete', project.id)), ["stop"])
                                        }, [
                                            createVNode(unref(Trash2), { size: 16 })
                                        ], 8, _hoisted_12))
                                        : createCommentVNode("", true)
                                ])
                            ])
                        ])
                    ], 42, _hoisted_2$1));
                }), 128)),
                (__props.projects.length === 0)
                    ? (openBlock(), createElementBlock("div", _hoisted_13, " Không tìm thấy dự án phù hợp với bộ lọc hiện tại. "))
                    : createCommentVNode("", true)
            ]));
        };
    }
});
const _hoisted_1 = { class: "dashboard-scroll dashboard-scroll--embedded no-scrollbar" };
const _hoisted_2 = { class: "dashboard-main project-home-main no-scrollbar" };
const _hoisted_3 = {
    id: "projects",
    class: "project-workspace glass-card"
};
const _hoisted_4 = { class: "project-workspace__header" };
const _hoisted_5 = ["disabled"];
const _hoisted_6 = ["disabled"];
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'DashboardPage',
    setup(__props) {
        const { beginEditProject, createProject, createProjectOpen, deleteProject, editProjectDescription, editProjectName, filteredProjects, openCreateProject, projectBeingEditedId, projectCards, projectDescription, projectEndDate, projectFilter, projectName, projects, projectSort, saveProjectEdit, searchQuery, selectProject, selectedProject, summaryCards, } = useDashboardContext();
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                createBaseVNode("div", _hoisted_2, [
                    createVNode(_sfc_main$2, {
                        id: "overview",
                        cards: unref(summaryCards)
                    }, null, 8, ["cards"]),
                    createBaseVNode("section", _hoisted_3, [
                        createBaseVNode("div", _hoisted_4, [
                            _cache[12] || (_cache[12] = createBaseVNode("div", null, [
                                createBaseVNode("span", null, "Projects"),
                                createBaseVNode("h2", null, "Project portfolio")
                            ], -1)),
                            createBaseVNode("p", null, toDisplayString(unref(filteredProjects).length) + " of " + toDisplayString(unref(projects).length) + " projects", 1)
                        ]),
                        createVNode(_sfc_main$3, {
                            search: unref(searchQuery),
                            "onUpdate:search": _cache[0] || (_cache[0] = ($event) => (isRef(searchQuery) ? (searchQuery).value = $event : null)),
                            sort: unref(projectSort),
                            "onUpdate:sort": _cache[1] || (_cache[1] = ($event) => (isRef(projectSort) ? (projectSort).value = $event : null)),
                            filter: unref(projectFilter),
                            "onUpdate:filter": _cache[2] || (_cache[2] = ($event) => (isRef(projectFilter) ? (projectFilter).value = $event : null)),
                            "project-count": unref(filteredProjects).length,
                            onCreate: unref(openCreateProject)
                        }, null, 8, ["search", "sort", "filter", "project-count", "onCreate"]),
                        (unref(createProjectOpen))
                            ? (openBlock(), createElementBlock("form", {
                                key: 0,
                                class: "project-inline-form project-inline-form--stacked",
                                onSubmit: _cache[7] || (_cache[7] = withModifiers(
                                //@ts-ignore
                                (...args) => (unref(createProject) && unref(createProject)(...args)), ["prevent"]))
                            }, [
                                withDirectives(createBaseVNode("input", {
                                    "onUpdate:modelValue": _cache[3] || (_cache[3] = ($event) => (isRef(projectName) ? (projectName).value = $event : null)),
                                    type: "text",
                                    placeholder: "Project name"
                                }, null, 512), [
                                    [vModelText, unref(projectName)]
                                ]),
                                withDirectives(createBaseVNode("input", {
                                    "onUpdate:modelValue": _cache[4] || (_cache[4] = ($event) => (isRef(projectDescription) ? (projectDescription).value = $event : null)),
                                    type: "text",
                                    placeholder: "Short description"
                                }, null, 512), [
                                    [vModelText, unref(projectDescription)]
                                ]),
                                withDirectives(createBaseVNode("input", {
                                    "onUpdate:modelValue": _cache[5] || (_cache[5] = ($event) => (isRef(projectEndDate) ? (projectEndDate).value = $event : null)),
                                    type: "date"
                                }, null, 512), [
                                    [vModelText, unref(projectEndDate)]
                                ]),
                                createBaseVNode("button", {
                                    class: "primary-button primary-button--compact",
                                    type: "submit",
                                    disabled: !unref(projectName).trim()
                                }, " Create ", 8, _hoisted_5),
                                createBaseVNode("button", {
                                    class: "text-button",
                                    type: "button",
                                    onClick: _cache[6] || (_cache[6] = ($event) => (createProjectOpen.value = false))
                                }, "Cancel")
                            ], 32))
                            : createCommentVNode("", true),
                        (unref(projectBeingEditedId))
                            ? (openBlock(), createElementBlock("form", {
                                key: 1,
                                class: "project-inline-form project-inline-form--stacked",
                                onSubmit: _cache[11] || (_cache[11] = withModifiers(
                                //@ts-ignore
                                (...args) => (unref(saveProjectEdit) && unref(saveProjectEdit)(...args)), ["prevent"]))
                            }, [
                                withDirectives(createBaseVNode("input", {
                                    "onUpdate:modelValue": _cache[8] || (_cache[8] = ($event) => (isRef(editProjectName) ? (editProjectName).value = $event : null)),
                                    type: "text",
                                    "aria-label": "Project name"
                                }, null, 512), [
                                    [vModelText, unref(editProjectName)]
                                ]),
                                withDirectives(createBaseVNode("input", {
                                    "onUpdate:modelValue": _cache[9] || (_cache[9] = ($event) => (isRef(editProjectDescription) ? (editProjectDescription).value = $event : null)),
                                    type: "text",
                                    "aria-label": "Project description"
                                }, null, 512), [
                                    [vModelText, unref(editProjectDescription)]
                                ]),
                                createBaseVNode("button", {
                                    class: "primary-button primary-button--compact",
                                    type: "submit",
                                    disabled: !unref(editProjectName).trim()
                                }, " Save ", 8, _hoisted_6),
                                createBaseVNode("button", {
                                    class: "text-button",
                                    type: "button",
                                    onClick: _cache[10] || (_cache[10] = ($event) => (projectBeingEditedId.value = null))
                                }, "Cancel")
                            ], 32))
                            : createCommentVNode("", true),
                        createVNode(_sfc_main$1, {
                            projects: unref(projectCards),
                            "active-project-id": unref(selectedProject)?.id ?? null,
                            onView: unref(selectProject),
                            onEdit: unref(beginEditProject),
                            onDelete: unref(deleteProject)
                        }, null, 8, ["projects", "active-project-id", "onView", "onEdit", "onDelete"])
                    ])
                ])
            ]));
        };
    }
});
export { _sfc_main as default };
//# sourceMappingURL=DashboardPage.js.map
