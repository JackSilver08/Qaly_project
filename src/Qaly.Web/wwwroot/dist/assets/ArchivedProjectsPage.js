import { _ as _sfc_main$1 } from './ProjectList.vue_vue_type_script_setup_true_lang.js';
import { u as useDashboardContext } from './main.js';
import { d as defineComponent, c as createElementBlock, a as createBaseVNode, t as toDisplayString, i as unref, g as createVNode, o as openBlock } from './vendor-vue.js';
import './vendor-icons.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
const _hoisted_1 = { class: "dashboard-scroll dashboard-scroll--embedded no-scrollbar" };
const _hoisted_2 = { class: "dashboard-main project-home-main no-scrollbar" };
const _hoisted_3 = { class: "project-workspace glass-card" };
const _hoisted_4 = { class: "project-workspace__header" };
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'ArchivedProjectsPage',
    setup(__props) {
        const { archivedProjectCards, selectProject, selectedProject, } = useDashboardContext();
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                createBaseVNode("div", _hoisted_2, [
                    createBaseVNode("section", _hoisted_3, [
                        createBaseVNode("div", _hoisted_4, [
                            _cache[0] || (_cache[0] = createBaseVNode("div", null, [
                                createBaseVNode("span", null, "Archived"),
                                createBaseVNode("h2", null, "Dự án đã lưu trữ")
                            ], -1)),
                            createBaseVNode("p", null, toDisplayString(unref(archivedProjectCards).length) + " dự án read-only", 1)
                        ]),
                        createVNode(_sfc_main$1, {
                            projects: unref(archivedProjectCards),
                            "active-project-id": unref(selectedProject)?.id ?? null,
                            "read-only": "",
                            onView: unref(selectProject)
                        }, null, 8, ["projects", "active-project-id", "onView"])
                    ])
                ])
            ]));
        };
    }
});
export { _sfc_main as default };
//# sourceMappingURL=ArchivedProjectsPage.js.map
