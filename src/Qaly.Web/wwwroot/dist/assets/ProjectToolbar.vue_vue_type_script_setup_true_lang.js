import { S as Search, X, n as SlidersHorizontal, P as Plus } from './vendor-icons.js';
import { d as defineComponent, c as createElementBlock, a as createBaseVNode, g as createVNode, i as unref, j as createCommentVNode, t as toDisplayString, o as openBlock } from './vendor-vue.js';
const _hoisted_1 = { class: "project-toolbar" };
const _hoisted_2 = {
    class: "project-search",
    "aria-label": "Tìm kiếm dự án"
};
const _hoisted_3 = ["value"];
const _hoisted_4 = { class: "project-toolbar__filters" };
const _hoisted_5 = ["value"];
const _hoisted_6 = ["value"];
const _hoisted_7 = { class: "project-toolbar__count" };
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'ProjectToolbar',
    props: {
        search: {},
        sort: {},
        filter: {},
        projectCount: {}
    },
    emits: ["update:search", "update:sort", "update:filter", "create"],
    setup(__props) {
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                createBaseVNode("label", _hoisted_2, [
                    createVNode(unref(Search), { size: 17 }),
                    createBaseVNode("input", {
                        value: __props.search,
                        type: "search",
                        placeholder: "Tìm dự án, chủ sở hữu, mô tả...",
                        onInput: _cache[0] || (_cache[0] = ($event) => (_ctx.$emit('update:search', $event.target.value)))
                    }, null, 40, _hoisted_3),
                    (__props.search)
                        ? (openBlock(), createElementBlock("button", {
                            key: 0,
                            type: "button",
                            "aria-label": "Xóa tìm kiếm",
                            onClick: _cache[1] || (_cache[1] = ($event) => (_ctx.$emit('update:search', '')))
                        }, [
                            createVNode(unref(X), { size: 15 })
                        ]))
                        : createCommentVNode("", true)
                ]),
                createBaseVNode("div", _hoisted_4, [
                    createBaseVNode("label", null, [
                        createVNode(unref(SlidersHorizontal), { size: 15 }),
                        createBaseVNode("select", {
                            value: __props.filter,
                            "aria-label": "Lọc dự án",
                            onChange: _cache[2] || (_cache[2] = ($event) => (_ctx.$emit('update:filter', $event.target.value)))
                        }, [...(_cache[5] || (_cache[5] = [
                                createBaseVNode("option", { value: "all" }, "Tất cả", -1),
                                createBaseVNode("option", { value: "active" }, "Đang chạy", -1),
                                createBaseVNode("option", { value: "planned" }, "Đã lên kế hoạch", -1),
                                createBaseVNode("option", { value: "at-risk" }, "Có rủi ro", -1)
                            ]))], 40, _hoisted_5)
                    ]),
                    createBaseVNode("select", {
                        value: __props.sort,
                        "aria-label": "Sắp xếp dự án",
                        onChange: _cache[3] || (_cache[3] = ($event) => (_ctx.$emit('update:sort', $event.target.value)))
                    }, [...(_cache[6] || (_cache[6] = [
                            createBaseVNode("option", { value: "recent" }, "Mới nhất", -1),
                            createBaseVNode("option", { value: "risk" }, "Rủi ro trước", -1),
                            createBaseVNode("option", { value: "progress" }, "Tiến độ cao", -1),
                            createBaseVNode("option", { value: "name" }, "Tên A-Z", -1)
                        ]))], 40, _hoisted_6),
                    createBaseVNode("button", {
                        class: "primary-button primary-button--compact",
                        type: "button",
                        onClick: _cache[4] || (_cache[4] = ($event) => (_ctx.$emit('create')))
                    }, [
                        createVNode(unref(Plus), { size: 16 }),
                        _cache[7] || (_cache[7] = createBaseVNode("span", null, "Tạo dự án", -1))
                    ])
                ]),
                createBaseVNode("span", _hoisted_7, toDisplayString(__props.projectCount) + " dự án", 1)
            ]));
        };
    }
});
export { _sfc_main as _ };
//# sourceMappingURL=ProjectToolbar.vue_vue_type_script_setup_true_lang.js.map
