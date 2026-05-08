import { i as CalendarDays, h as UserRound } from './vendor-icons.js';
import { d as defineComponent, o as openBlock, c as createElementBlock, a as createBaseVNode, t as toDisplayString, n as normalizeClass, g as createVNode, i as unref, x as createTextVNode, F as Fragment, b as renderList, l as createCommentVNode, e as createBlock } from './vendor-vue.js';
import { u as useDashboardContext } from './main.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
const _hoisted_1$2 = { class: "task-list-item__main" };
const _hoisted_2$2 = { class: "task-list-item__title" };
const _hoisted_3$1 = { class: "task-list-item__meta" };
const _hoisted_4$1 = { class: "task-list-item__reporter" };
const _hoisted_5 = { class: "task-list-item__status" };
const _sfc_main$2 = /*@__PURE__*/ defineComponent({
    __name: 'TaskItem',
    props: {
        task: {}
    },
    emits: ["view"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("article", {
                class: "task-list-item",
                onClick: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('view', __props.task.projectId, __props.task.id)))
            }, [
                createBaseVNode("div", _hoisted_1$2, [
                    createBaseVNode("div", _hoisted_2$2, [
                        createBaseVNode("strong", null, toDisplayString(__props.task.title), 1),
                        createBaseVNode("span", {
                            class: normalizeClass(`priority priority--${__props.task.priority.toLowerCase()}`)
                        }, toDisplayString(__props.task.priority), 3)
                    ]),
                    createBaseVNode("div", _hoisted_3$1, [
                        createBaseVNode("span", null, toDisplayString(__props.task.projectName), 1),
                        createBaseVNode("span", null, [
                            createVNode(unref(CalendarDays), { size: 14 }),
                            createTextVNode(" " + toDisplayString(__props.task.assignedAtLabel), 1)
                        ]),
                        createBaseVNode("span", {
                            class: normalizeClass({ 'project-risk': __props.task.isOverdue })
                        }, "Deadline " + toDisplayString(__props.task.dueDateLabel), 3)
                    ])
                ]),
                createBaseVNode("div", _hoisted_4$1, [
                    createBaseVNode("span", null, toDisplayString(__props.task.reporterInitials), 1),
                    createBaseVNode("div", null, [
                        _cache[1] || (_cache[1] = createBaseVNode("small", null, "Người giao", -1)),
                        createBaseVNode("strong", null, toDisplayString(__props.task.reporterName), 1)
                    ])
                ]),
                createBaseVNode("div", _hoisted_5, [
                    createVNode(unref(UserRound), { size: 15 }),
                    createBaseVNode("span", null, toDisplayString(__props.task.statusLabel), 1)
                ])
            ]));
        };
    }
});
const _hoisted_1$1 = { class: "task-list-shell" };
const _hoisted_2$1 = {
    key: 0,
    class: "empty-state"
};
const _sfc_main$1 = /*@__PURE__*/ defineComponent({
    __name: 'TaskList',
    props: {
        tasks: {}
    },
    emits: ["view"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1$1, [
                (openBlock(true), createElementBlock(Fragment, null, renderList(__props.tasks, (task) => {
                    return (openBlock(), createBlock(_sfc_main$2, {
                        key: task.id,
                        task: task,
                        onView: _cache[0] || (_cache[0] = (projectId, taskId) => _ctx.$emit('view', projectId, taskId))
                    }, null, 8, ["task"]));
                }), 128)),
                (__props.tasks.length === 0)
                    ? (openBlock(), createElementBlock("div", _hoisted_2$1, " Chưa có nhiệm vụ nào được giao cho bạn. "))
                    : createCommentVNode("", true)
            ]));
        };
    }
});
const _hoisted_1 = { class: "dashboard-scroll dashboard-scroll--embedded no-scrollbar" };
const _hoisted_2 = { class: "dashboard-main project-home-main no-scrollbar" };
const _hoisted_3 = { class: "project-workspace glass-card" };
const _hoisted_4 = { class: "project-workspace__header" };
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'TasksPage',
    setup(__props) {
        const { assignedTaskCards, openTask, } = useDashboardContext();
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                createBaseVNode("div", _hoisted_2, [
                    createBaseVNode("section", _hoisted_3, [
                        createBaseVNode("div", _hoisted_4, [
                            _cache[0] || (_cache[0] = createBaseVNode("div", null, [
                                createBaseVNode("span", null, "Tasks"),
                                createBaseVNode("h2", null, "Nhiệm vụ của tôi")
                            ], -1)),
                            createBaseVNode("p", null, toDisplayString(unref(assignedTaskCards).length) + " nhiệm vụ được giao", 1)
                        ]),
                        createVNode(_sfc_main$1, {
                            tasks: unref(assignedTaskCards),
                            onView: unref(openTask)
                        }, null, 8, ["tasks", "onView"])
                    ])
                ])
            ]));
        };
    }
});
export { _sfc_main as default };
//# sourceMappingURL=TasksPage.js.map
