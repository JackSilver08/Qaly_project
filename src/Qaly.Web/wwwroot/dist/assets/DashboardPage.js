import { d as defineComponent, c as createElementBlock, F as Fragment, b as renderList, o as openBlock, n as normalizeClass, a as createBaseVNode, e as createBlock, f as resolveDynamicComponent, t as toDisplayString, g as createVNode, i as unref, G as isRef, x as withModifiers, y as withDirectives, z as vModelText, j as createCommentVNode } from './vendor-vue.js';
import { U as Users, b as ClipboardList, F as FolderKanban } from './vendor-icons.js';
import { _ as _sfc_main$3 } from './ProjectList.vue_vue_type_script_setup_true_lang.js';
import { _ as _sfc_main$2 } from './ProjectToolbar.vue_vue_type_script_setup_true_lang.js';
import { u as useDashboardContext } from './main.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
const _hoisted_1$1 = {
    class: "summary-card-grid",
    "aria-label": "Tổng quan dự án"
};
const _hoisted_2$1 = { class: "summary-card__icon" };
const _sfc_main$1 = /*@__PURE__*/ defineComponent({
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
            return (openBlock(), createElementBlock("section", _hoisted_1$1, [
                (openBlock(true), createElementBlock(Fragment, null, renderList(__props.cards, (card) => {
                    return (openBlock(), createElementBlock("article", {
                        key: card.key,
                        class: normalizeClass(["summary-card glass-card", `summary-card--${card.tone}`])
                    }, [
                        createBaseVNode("div", _hoisted_2$1, [
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
                    createVNode(_sfc_main$1, {
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
                        createVNode(_sfc_main$2, {
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
                        createVNode(_sfc_main$3, {
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
