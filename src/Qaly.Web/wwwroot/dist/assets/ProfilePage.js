import { u as useDashboardContext } from './main.js';
import { e as UserRound, h as Mail, S as ShieldCheck, g as CalendarDays } from './vendor-icons.js';
import { d as defineComponent, c as createElementBlock, a as createBaseVNode, t as toDisplayString, g as createVNode, i as unref, n as normalizeClass, B as computed, o as openBlock } from './vendor-vue.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
const _hoisted_1 = { class: "dashboard-scroll dashboard-scroll--embedded no-scrollbar" };
const _hoisted_2 = { class: "dashboard-main project-home-main no-scrollbar" };
const _hoisted_3 = { class: "profile-page glass-card" };
const _hoisted_4 = { class: "profile-page__header" };
const _hoisted_5 = { class: "profile-avatar profile-page__avatar" };
const _hoisted_6 = { class: "profile-page__grid" };
const _hoisted_7 = { class: "profile-page__item" };
const _hoisted_8 = { class: "profile-page__item" };
const _hoisted_9 = { class: "profile-page__item" };
const _hoisted_10 = { class: "profile-page__item" };
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'ProfilePage',
    setup(__props) {
        const { currentUser, displayRole, formatDate } = useDashboardContext();
        const user = computed(() => currentUser.value);
        const userInitials = computed(() => {
            const name = user.value?.fullName || user.value?.email || 'Qaly user';
            return name
                .split(' ')
                .filter(Boolean)
                .slice(0, 2)
                .map((part) => part[0]?.toUpperCase() ?? '')
                .join('');
        });
        return (_ctx, _cache) => {
            return (openBlock(), createElementBlock("div", _hoisted_1, [
                createBaseVNode("div", _hoisted_2, [
                    createBaseVNode("section", _hoisted_3, [
                        createBaseVNode("header", _hoisted_4, [
                            createBaseVNode("div", _hoisted_5, toDisplayString(userInitials.value), 1),
                            createBaseVNode("div", null, [
                                _cache[0] || (_cache[0] = createBaseVNode("span", null, "Profile", -1)),
                                _cache[1] || (_cache[1] = createBaseVNode("h2", null, "Trang cá nhân", -1)),
                                createBaseVNode("p", null, toDisplayString(user.value?.fullName || user.value?.email || 'Qaly user'), 1)
                            ])
                        ]),
                        createBaseVNode("div", _hoisted_6, [
                            createBaseVNode("article", _hoisted_7, [
                                createVNode(unref(UserRound), { size: 20 }),
                                createBaseVNode("div", null, [
                                    _cache[2] || (_cache[2] = createBaseVNode("span", null, "Họ tên", -1)),
                                    createBaseVNode("strong", null, toDisplayString(user.value?.fullName || user.value?.email || 'Qaly user'), 1)
                                ])
                            ]),
                            createBaseVNode("article", _hoisted_8, [
                                createVNode(unref(Mail), { size: 20 }),
                                createBaseVNode("div", null, [
                                    _cache[3] || (_cache[3] = createBaseVNode("span", null, "Email", -1)),
                                    createBaseVNode("strong", null, toDisplayString(user.value?.email ?? 'Chưa có email'), 1)
                                ])
                            ]),
                            createBaseVNode("article", _hoisted_9, [
                                createVNode(unref(ShieldCheck), { size: 20 }),
                                createBaseVNode("div", null, [
                                    _cache[4] || (_cache[4] = createBaseVNode("span", null, "Vai trò", -1)),
                                    createBaseVNode("strong", null, toDisplayString(unref(displayRole)(user.value?.role) || 'Member'), 1)
                                ])
                            ]),
                            createBaseVNode("article", _hoisted_10, [
                                createVNode(unref(CalendarDays), { size: 20 }),
                                createBaseVNode("div", null, [
                                    _cache[5] || (_cache[5] = createBaseVNode("span", null, "Ngày tham gia", -1)),
                                    createBaseVNode("strong", null, toDisplayString(user.value?.createdAt ? unref(formatDate)(user.value.createdAt) : 'No date'), 1)
                                ])
                            ])
                        ]),
                        createBaseVNode("div", {
                            class: normalizeClass(["profile-page__status", { 'is-active': user.value?.isActive }])
                        }, toDisplayString(user.value?.isActive ? 'Đang hoạt động' : 'Tạm khóa'), 3)
                    ])
                ])
            ]));
        };
    }
});
export { _sfc_main as default };
//# sourceMappingURL=ProfilePage.js.map
