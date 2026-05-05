import { d as defineComponent, c as createElementBlock, a as createBaseVNode, t as toDisplayString, i as unref, g as createVNode, G as isRef, x as withModifiers, y as withDirectives, z as vModelText, j as createCommentVNode, o as openBlock } from './vendor-vue.js';
import { _ as _sfc_main$2 } from './ProjectList.vue_vue_type_script_setup_true_lang.js';
import { _ as _sfc_main$1 } from './ProjectToolbar.vue_vue_type_script_setup_true_lang.js';
import { u as useDashboardContext } from './main.js';
import './vendor-icons.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
const _hoisted_1 = { class: "dashboard-scroll dashboard-scroll--embedded no-scrollbar" };
const _hoisted_2 = { class: "dashboard-main project-home-main no-scrollbar" };
const _hoisted_3 = { class: "project-workspace glass-card" };
const _hoisted_4 = { class: "project-workspace__header" };
const _hoisted_5 = ["disabled"];
const _hoisted_6 = ["disabled"];
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'ProjectsPage',
    setup(__props) {
        const { activeProjectCards, beginEditProject, createProject, createProjectOpen, deleteProject, editProjectDescription, editProjectName, openCreateProject, projectBeingEditedId, projectDescription, projectEndDate, projectFilter, projectName, projectSort, saveProjectEdit, searchQuery, selectProject, selectedProject, } = useDashboardContext();
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                createBaseVNode("div", _hoisted_2, [
                    createBaseVNode("section", _hoisted_3, [
                        createBaseVNode("div", _hoisted_4, [
                            _cache[12] || (_cache[12] = createBaseVNode("div", null, [
                                createBaseVNode("span", null, "Projects"),
                                createBaseVNode("h2", null, "Dự án")
                            ], -1)),
                            createBaseVNode("p", null, toDisplayString(unref(activeProjectCards).length) + " dự án đang hiển thị", 1)
                        ]),
                        createVNode(_sfc_main$1, {
                            search: unref(searchQuery),
                            "onUpdate:search": _cache[0] || (_cache[0] = ($event) => (isRef(searchQuery) ? (searchQuery).value = $event : null)),
                            sort: unref(projectSort),
                            "onUpdate:sort": _cache[1] || (_cache[1] = ($event) => (isRef(projectSort) ? (projectSort).value = $event : null)),
                            filter: unref(projectFilter),
                            "onUpdate:filter": _cache[2] || (_cache[2] = ($event) => (isRef(projectFilter) ? (projectFilter).value = $event : null)),
                            "project-count": unref(activeProjectCards).length,
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
                                    placeholder: "Tên dự án"
                                }, null, 512), [
                                    [vModelText, unref(projectName)]
                                ]),
                                withDirectives(createBaseVNode("input", {
                                    "onUpdate:modelValue": _cache[4] || (_cache[4] = ($event) => (isRef(projectDescription) ? (projectDescription).value = $event : null)),
                                    type: "text",
                                    placeholder: "Mô tả ngắn"
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
                                }, " Tạo ", 8, _hoisted_5),
                                createBaseVNode("button", {
                                    class: "text-button",
                                    type: "button",
                                    onClick: _cache[6] || (_cache[6] = ($event) => (createProjectOpen.value = false))
                                }, "Hủy")
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
                                    "aria-label": "Tên dự án"
                                }, null, 512), [
                                    [vModelText, unref(editProjectName)]
                                ]),
                                withDirectives(createBaseVNode("input", {
                                    "onUpdate:modelValue": _cache[9] || (_cache[9] = ($event) => (isRef(editProjectDescription) ? (editProjectDescription).value = $event : null)),
                                    type: "text",
                                    "aria-label": "Mô tả dự án"
                                }, null, 512), [
                                    [vModelText, unref(editProjectDescription)]
                                ]),
                                createBaseVNode("button", {
                                    class: "primary-button primary-button--compact",
                                    type: "submit",
                                    disabled: !unref(editProjectName).trim()
                                }, " Lưu ", 8, _hoisted_6),
                                createBaseVNode("button", {
                                    class: "text-button",
                                    type: "button",
                                    onClick: _cache[10] || (_cache[10] = ($event) => (projectBeingEditedId.value = null))
                                }, "Hủy")
                            ], 32))
                            : createCommentVNode("", true),
                        createVNode(_sfc_main$2, {
                            projects: unref(activeProjectCards),
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
//# sourceMappingURL=ProjectsPage.js.map
