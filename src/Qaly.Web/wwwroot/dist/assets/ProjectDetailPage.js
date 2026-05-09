import { d as defineComponent, O as reactive, h as h$1, m as ref, P as getCurrentInstance, i as unref, v as watch, j as onMounted, C as nextTick, Q as onUnmounted, B as computed, R as toRefs, N as isRef, c as createElementBlock, a as createBaseVNode, g as createVNode, t as toDisplayString, n as normalizeClass, l as createCommentVNode, G as normalizeStyle, o as openBlock, z as withDirectives, S as vModelSelect, F as Fragment, b as renderList, x as createTextVNode, A as vModelText, M as withKeys, e as createBlock, w as withCtx, T as Transition, y as withModifiers } from './vendor-vue.js';
import { _ as _sfc_main$5, a as _export_sfc, u as useDashboardContext } from './main.js';
import { A as ArrowLeft, c as LayoutDashboard, m as UserPlus, k as Mail, n as Shield, j as Trash2, o as Plus, p as Search, q as FileText, P as Pencil, r as Clock, s as Square, t as Play, b as Send, d as ClipboardList, X, u as Ellipsis, v as MessageSquare } from './vendor-icons.js';
import './vendor-markdown.js';
import './vendor-realtime.js';
var an = Object.defineProperty;
var Pe = Object.getOwnPropertySymbols;
var yt = Object.prototype.hasOwnProperty, wt = Object.prototype.propertyIsEnumerable;
var bt = (t, e, n) => e in t ? an(t, e, { enumerable: true, configurable: true, writable: true, value: n }) : t[e] = n, fe = (t, e) => {
    for (var n in e || (e = {}))
        yt.call(e, n) && bt(t, n, e[n]);
    if (Pe)
        for (var n of Pe(e))
            wt.call(e, n) && bt(t, n, e[n]);
    return t;
};
var $e = (t, e) => {
    var n = {};
    for (var o in t)
        yt.call(t, o) && e.indexOf(o) < 0 && (n[o] = t[o]);
    if (t != null && Pe)
        for (var o of Pe(t))
            e.indexOf(o) < 0 && wt.call(t, o) && (n[o] = t[o]);
    return n;
};
const Ht = "[vue-draggable-plus]: ";
function mn(t) {
    console.warn(Ht + t);
}
function vn(t) {
    console.error(Ht + t);
}
function St(t, e, n) {
    return n >= 0 && n < t.length && t.splice(n, 0, t.splice(e, 1)[0]), t;
}
function bn(t) {
    return t.replace(/-(\w)/g, (e, n) => n ? n.toUpperCase() : "");
}
function yn(t) {
    return Object.keys(t).reduce((e, n) => (typeof t[n] != "undefined" && (e[bn(n)] = t[n]), e), {});
}
function Dt(t, e) {
    return Array.isArray(t) && t.splice(e, 1), t;
}
function _t(t, e, n) {
    return Array.isArray(t) && t.splice(e, 0, n), t;
}
function wn(t) {
    return typeof t == "undefined";
}
function En(t) {
    return typeof t == "string";
}
function Tt(t, e, n) {
    const o = t.children[n];
    t.insertBefore(e, o);
}
function Ke(t) {
    t.parentNode && t.parentNode.removeChild(t);
}
function Sn(t, e = document) {
    var o;
    let n = null;
    return typeof (e == null ? void 0 : e.querySelector) == "function" ? n = (o = e == null ? void 0 : e.querySelector) == null ? void 0 : o.call(e, t) : n = document.querySelector(t), n || mn(`Element not found: ${t}`), n;
}
function Dn(t, e, n = null) {
    return function (...o) {
        return t.apply(n, o), e.apply(n, o);
    };
}
function _n(t, e) {
    const n = fe({}, t);
    return Object.keys(e).forEach((o) => {
        n[o] ? n[o] = Dn(t[o], e[o]) : n[o] = e[o];
    }), n;
}
function Tn(t) {
    return t instanceof HTMLElement;
}
function Ct(t, e) {
    Object.keys(t).forEach((n) => {
        e(n, t[n]);
    });
}
function Cn(t) {
    return t.charCodeAt(0) === 111 && t.charCodeAt(1) === 110 && (t.charCodeAt(2) > 122 || t.charCodeAt(2) < 97);
}
const On = Object.assign;
/**!
 * Sortable 1.15.2
 * @author	RubaXa   <trash@rubaxa.org>
 * @author	owenm    <owen23355@gmail.com>
 * @license MIT
 */
function Ot(t, e) {
    var n = Object.keys(t);
    if (Object.getOwnPropertySymbols) {
        var o = Object.getOwnPropertySymbols(t);
        e && (o = o.filter(function (r) {
            return Object.getOwnPropertyDescriptor(t, r).enumerable;
        })), n.push.apply(n, o);
    }
    return n;
}
function ne(t) {
    for (var e = 1; e < arguments.length; e++) {
        var n = arguments[e] != null ? arguments[e] : {};
        e % 2 ? Ot(Object(n), true).forEach(function (o) {
            In(t, o, n[o]);
        }) : Object.getOwnPropertyDescriptors ? Object.defineProperties(t, Object.getOwnPropertyDescriptors(n)) : Ot(Object(n)).forEach(function (o) {
            Object.defineProperty(t, o, Object.getOwnPropertyDescriptor(n, o));
        });
    }
    return t;
}
function Ye(t) {
    "@babel/helpers - typeof";
    return typeof Symbol == "function" && typeof Symbol.iterator == "symbol" ? Ye = function (e) {
        return typeof e;
    } : Ye = function (e) {
        return e && typeof Symbol == "function" && e.constructor === Symbol && e !== Symbol.prototype ? "symbol" : typeof e;
    }, Ye(t);
}
function In(t, e, n) {
    return e in t ? Object.defineProperty(t, e, {
        value: n,
        enumerable: true,
        configurable: true,
        writable: true
    }) : t[e] = n, t;
}
function ie() {
    return ie = Object.assign || function (t) {
        for (var e = 1; e < arguments.length; e++) {
            var n = arguments[e];
            for (var o in n)
                Object.prototype.hasOwnProperty.call(n, o) && (t[o] = n[o]);
        }
        return t;
    }, ie.apply(this, arguments);
}
function An(t, e) {
    if (t == null)
        return {};
    var n = {}, o = Object.keys(t), r, i;
    for (i = 0; i < o.length; i++)
        r = o[i], !(e.indexOf(r) >= 0) && (n[r] = t[r]);
    return n;
}
function xn(t, e) {
    if (t == null)
        return {};
    var n = An(t, e), o, r;
    if (Object.getOwnPropertySymbols) {
        var i = Object.getOwnPropertySymbols(t);
        for (r = 0; r < i.length; r++)
            o = i[r], !(e.indexOf(o) >= 0) && Object.prototype.propertyIsEnumerable.call(t, o) && (n[o] = t[o]);
    }
    return n;
}
var Nn = "1.15.2";
function re(t) {
    if (typeof window != "undefined" && window.navigator)
        return !!navigator.userAgent.match(t);
}
var ae = re(/(?:Trident.*rv[ :]?11\.|msie|iemobile|Windows Phone)/i), xe = re(/Edge/i), It = re(/firefox/i), Te = re(/safari/i) && !re(/chrome/i) && !re(/android/i), Lt = re(/iP(ad|od|hone)/i), Wt = re(/chrome/i) && re(/android/i), Gt = {
    capture: false,
    passive: false
};
function S(t, e, n) {
    t.addEventListener(e, n, !ae && Gt);
}
function E(t, e, n) {
    t.removeEventListener(e, n, !ae && Gt);
}
function We(t, e) {
    if (e) {
        if (e[0] === ">" && (e = e.substring(1)), t)
            try {
                if (t.matches)
                    return t.matches(e);
                if (t.msMatchesSelector)
                    return t.msMatchesSelector(e);
                if (t.webkitMatchesSelector)
                    return t.webkitMatchesSelector(e);
            }
            catch (n) {
                return false;
            }
        return false;
    }
}
function Pn(t) {
    return t.host && t !== document && t.host.nodeType ? t.host : t.parentNode;
}
function Q(t, e, n, o) {
    if (t) {
        n = n || document;
        do {
            if (e != null && (e[0] === ">" ? t.parentNode === n && We(t, e) : We(t, e)) || o && t === n)
                return t;
            if (t === n)
                break;
        } while (t = Pn(t));
    }
    return null;
}
var At = /\s+/g;
function V(t, e, n) {
    if (t && e)
        if (t.classList)
            t.classList[n ? "add" : "remove"](e);
        else {
            var o = (" " + t.className + " ").replace(At, " ").replace(" " + e + " ", " ");
            t.className = (o + (n ? " " + e : "")).replace(At, " ");
        }
}
function h(t, e, n) {
    var o = t && t.style;
    if (o) {
        if (n === void 0)
            return document.defaultView && document.defaultView.getComputedStyle ? n = document.defaultView.getComputedStyle(t, "") : t.currentStyle && (n = t.currentStyle), e === void 0 ? n : n[e];
        !(e in o) && e.indexOf("webkit") === -1 && (e = "-webkit-" + e), o[e] = n + (typeof n == "string" ? "" : "px");
    }
}
function ye(t, e) {
    var n = "";
    if (typeof t == "string")
        n = t;
    else
        do {
            var o = h(t, "transform");
            o && o !== "none" && (n = o + " " + n);
        } while (!e && (t = t.parentNode));
    var r = window.DOMMatrix || window.WebKitCSSMatrix || window.CSSMatrix || window.MSCSSMatrix;
    return r && new r(n);
}
function jt(t, e, n) {
    if (t) {
        var o = t.getElementsByTagName(e), r = 0, i = o.length;
        if (n)
            for (; r < i; r++)
                n(o[r], r);
        return o;
    }
    return [];
}
function te() {
    var t = document.scrollingElement;
    return t || document.documentElement;
}
function P(t, e, n, o, r) {
    if (!(!t.getBoundingClientRect && t !== window)) {
        var i, a, l, s, u, d, f;
        if (t !== window && t.parentNode && t !== te() ? (i = t.getBoundingClientRect(), a = i.top, l = i.left, s = i.bottom, u = i.right, d = i.height, f = i.width) : (a = 0, l = 0, s = window.innerHeight, u = window.innerWidth, d = window.innerHeight, f = window.innerWidth), (e || n) && t !== window && (r = r || t.parentNode, !ae))
            do
                if (r && r.getBoundingClientRect && (h(r, "transform") !== "none" || n && h(r, "position") !== "static")) {
                    var m = r.getBoundingClientRect();
                    a -= m.top + parseInt(h(r, "border-top-width")), l -= m.left + parseInt(h(r, "border-left-width")), s = a + i.height, u = l + i.width;
                    break;
                }
            while (r = r.parentNode);
        if (o && t !== window) {
            var y = ye(r || t), b = y && y.a, w = y && y.d;
            y && (a /= w, l /= b, f /= b, d /= w, s = a + d, u = l + f);
        }
        return {
            top: a,
            left: l,
            bottom: s,
            right: u,
            width: f,
            height: d
        };
    }
}
function xt(t, e, n) {
    for (var o = ce(t, true), r = P(t)[e]; o;) {
        var i = P(o)[n], a = void 0;
        if (a = r >= i, !a)
            return o;
        if (o === te())
            break;
        o = ce(o, false);
    }
    return false;
}
function we(t, e, n, o) {
    for (var r = 0, i = 0, a = t.children; i < a.length;) {
        if (a[i].style.display !== "none" && a[i] !== p.ghost && (o || a[i] !== p.dragged) && Q(a[i], n.draggable, t, false)) {
            if (r === e)
                return a[i];
            r++;
        }
        i++;
    }
    return null;
}
function ht(t, e) {
    for (var n = t.lastElementChild; n && (n === p.ghost || h(n, "display") === "none" || e && !We(n, e));)
        n = n.previousElementSibling;
    return n || null;
}
function J(t, e) {
    var n = 0;
    if (!t || !t.parentNode)
        return -1;
    for (; t = t.previousElementSibling;)
        t.nodeName.toUpperCase() !== "TEMPLATE" && t !== p.clone && (!e || We(t, e)) && n++;
    return n;
}
function Nt(t) {
    var e = 0, n = 0, o = te();
    if (t)
        do {
            var r = ye(t), i = r.a, a = r.d;
            e += t.scrollLeft * i, n += t.scrollTop * a;
        } while (t !== o && (t = t.parentNode));
    return [e, n];
}
function Mn(t, e) {
    for (var n in t)
        if (t.hasOwnProperty(n)) {
            for (var o in e)
                if (e.hasOwnProperty(o) && e[o] === t[n][o])
                    return Number(n);
        }
    return -1;
}
function ce(t, e) {
    if (!t || !t.getBoundingClientRect)
        return te();
    var n = t, o = false;
    do
        if (n.clientWidth < n.scrollWidth || n.clientHeight < n.scrollHeight) {
            var r = h(n);
            if (n.clientWidth < n.scrollWidth && (r.overflowX == "auto" || r.overflowX == "scroll") || n.clientHeight < n.scrollHeight && (r.overflowY == "auto" || r.overflowY == "scroll")) {
                if (!n.getBoundingClientRect || n === document.body)
                    return te();
                if (o || e)
                    return n;
                o = true;
            }
        }
    while (n = n.parentNode);
    return te();
}
function Fn(t, e) {
    if (t && e)
        for (var n in e)
            e.hasOwnProperty(n) && (t[n] = e[n]);
    return t;
}
function Je(t, e) {
    return Math.round(t.top) === Math.round(e.top) && Math.round(t.left) === Math.round(e.left) && Math.round(t.height) === Math.round(e.height) && Math.round(t.width) === Math.round(e.width);
}
var Ce;
function zt(t, e) {
    return function () {
        if (!Ce) {
            var n = arguments, o = this;
            n.length === 1 ? t.call(o, n[0]) : t.apply(o, n), Ce = setTimeout(function () {
                Ce = void 0;
            }, e);
        }
    };
}
function Rn() {
    clearTimeout(Ce), Ce = void 0;
}
function Ut(t, e, n) {
    t.scrollLeft += e, t.scrollTop += n;
}
function Vt(t) {
    var e = window.Polymer, n = window.jQuery || window.Zepto;
    return e && e.dom ? e.dom(t).cloneNode(true) : n ? n(t).clone(true)[0] : t.cloneNode(true);
}
function $t(t, e, n) {
    var o = {};
    return Array.from(t.children).forEach(function (r) {
        var i, a, l, s;
        if (!(!Q(r, e.draggable, t, false) || r.animated || r === n)) {
            var u = P(r);
            o.left = Math.min((i = o.left) !== null && i !== void 0 ? i : 1 / 0, u.left), o.top = Math.min((a = o.top) !== null && a !== void 0 ? a : 1 / 0, u.top), o.right = Math.max((l = o.right) !== null && l !== void 0 ? l : -1 / 0, u.right), o.bottom = Math.max((s = o.bottom) !== null && s !== void 0 ? s : -1 / 0, u.bottom);
        }
    }), o.width = o.right - o.left, o.height = o.bottom - o.top, o.x = o.left, o.y = o.top, o;
}
var q = "Sortable" + ( /* @__PURE__ */new Date()).getTime();
function Xn() {
    var t = [], e;
    return {
        captureAnimationState: function () {
            if (t = [], !!this.options.animation) {
                var o = [].slice.call(this.el.children);
                o.forEach(function (r) {
                    if (!(h(r, "display") === "none" || r === p.ghost)) {
                        t.push({
                            target: r,
                            rect: P(r)
                        });
                        var i = ne({}, t[t.length - 1].rect);
                        if (r.thisAnimationDuration) {
                            var a = ye(r, true);
                            a && (i.top -= a.f, i.left -= a.e);
                        }
                        r.fromRect = i;
                    }
                });
            }
        },
        addAnimationState: function (o) {
            t.push(o);
        },
        removeAnimationState: function (o) {
            t.splice(Mn(t, {
                target: o
            }), 1);
        },
        animateAll: function (o) {
            var r = this;
            if (!this.options.animation) {
                clearTimeout(e), typeof o == "function" && o();
                return;
            }
            var i = false, a = 0;
            t.forEach(function (l) {
                var s = 0, u = l.target, d = u.fromRect, f = P(u), m = u.prevFromRect, y = u.prevToRect, b = l.rect, w = ye(u, true);
                w && (f.top -= w.f, f.left -= w.e), u.toRect = f, u.thisAnimationDuration && Je(m, f) && !Je(d, f) && // Make sure animatingRect is on line between toRect & fromRect
                    (b.top - f.top) / (b.left - f.left) === (d.top - f.top) / (d.left - f.left) && (s = Bn(b, m, y, r.options)), Je(f, d) || (u.prevFromRect = d, u.prevToRect = f, s || (s = r.options.animation), r.animate(u, b, f, s)), s && (i = true, a = Math.max(a, s), clearTimeout(u.animationResetTimer), u.animationResetTimer = setTimeout(function () {
                    u.animationTime = 0, u.prevFromRect = null, u.fromRect = null, u.prevToRect = null, u.thisAnimationDuration = null;
                }, s), u.thisAnimationDuration = s);
            }), clearTimeout(e), i ? e = setTimeout(function () {
                typeof o == "function" && o();
            }, a) : typeof o == "function" && o(), t = [];
        },
        animate: function (o, r, i, a) {
            if (a) {
                h(o, "transition", ""), h(o, "transform", "");
                var l = ye(this.el), s = l && l.a, u = l && l.d, d = (r.left - i.left) / (s || 1), f = (r.top - i.top) / (u || 1);
                o.animatingX = !!d, o.animatingY = !!f, h(o, "transform", "translate3d(" + d + "px," + f + "px,0)"), this.forRepaintDummy = Yn(o), h(o, "transition", "transform " + a + "ms" + (this.options.easing ? " " + this.options.easing : "")), h(o, "transform", "translate3d(0,0,0)"), typeof o.animated == "number" && clearTimeout(o.animated), o.animated = setTimeout(function () {
                    h(o, "transition", ""), h(o, "transform", ""), o.animated = false, o.animatingX = false, o.animatingY = false;
                }, a);
            }
        }
    };
}
function Yn(t) {
    return t.offsetWidth;
}
function Bn(t, e, n, o) {
    return Math.sqrt(Math.pow(e.top - t.top, 2) + Math.pow(e.left - t.left, 2)) / Math.sqrt(Math.pow(e.top - n.top, 2) + Math.pow(e.left - n.left, 2)) * o.animation;
}
var ge = [], Ze = {
    initializeByDefault: true
}, Ne = {
    mount: function (e) {
        for (var n in Ze)
            Ze.hasOwnProperty(n) && !(n in e) && (e[n] = Ze[n]);
        ge.forEach(function (o) {
            if (o.pluginName === e.pluginName)
                throw "Sortable: Cannot mount plugin ".concat(e.pluginName, " more than once");
        }), ge.push(e);
    },
    pluginEvent: function (e, n, o) {
        var r = this;
        this.eventCanceled = false, o.cancel = function () {
            r.eventCanceled = true;
        };
        var i = e + "Global";
        ge.forEach(function (a) {
            n[a.pluginName] && (n[a.pluginName][i] && n[a.pluginName][i](ne({
                sortable: n
            }, o)), n.options[a.pluginName] && n[a.pluginName][e] && n[a.pluginName][e](ne({
                sortable: n
            }, o)));
        });
    },
    initializePlugins: function (e, n, o, r) {
        ge.forEach(function (l) {
            var s = l.pluginName;
            if (!(!e.options[s] && !l.initializeByDefault)) {
                var u = new l(e, n, e.options);
                u.sortable = e, u.options = e.options, e[s] = u, ie(o, u.defaults);
            }
        });
        for (var i in e.options)
            if (e.options.hasOwnProperty(i)) {
                var a = this.modifyOption(e, i, e.options[i]);
                typeof a != "undefined" && (e.options[i] = a);
            }
    },
    getEventProperties: function (e, n) {
        var o = {};
        return ge.forEach(function (r) {
            typeof r.eventProperties == "function" && ie(o, r.eventProperties.call(n[r.pluginName], e));
        }), o;
    },
    modifyOption: function (e, n, o) {
        var r;
        return ge.forEach(function (i) {
            e[i.pluginName] && i.optionListeners && typeof i.optionListeners[n] == "function" && (r = i.optionListeners[n].call(e[i.pluginName], o));
        }), r;
    }
};
function kn(t) {
    var e = t.sortable, n = t.rootEl, o = t.name, r = t.targetEl, i = t.cloneEl, a = t.toEl, l = t.fromEl, s = t.oldIndex, u = t.newIndex, d = t.oldDraggableIndex, f = t.newDraggableIndex, m = t.originalEvent, y = t.putSortable, b = t.extraEventProperties;
    if (e = e || n && n[q], !!e) {
        var w, L = e.options, G = "on" + o.charAt(0).toUpperCase() + o.substr(1);
        window.CustomEvent && !ae && !xe ? w = new CustomEvent(o, {
            bubbles: true,
            cancelable: true
        }) : (w = document.createEvent("Event"), w.initEvent(o, true, true)), w.to = a || n, w.from = l || n, w.item = r || n, w.clone = i, w.oldIndex = s, w.newIndex = u, w.oldDraggableIndex = d, w.newDraggableIndex = f, w.originalEvent = m, w.pullMode = y ? y.lastPutMode : void 0;
        var R = ne(ne({}, b), Ne.getEventProperties(o, e));
        for (var j in R)
            w[j] = R[j];
        n && n.dispatchEvent(w), L[G] && L[G].call(e, w);
    }
}
var Hn = ["evt"], z = function (e, n) {
    var o = arguments.length > 2 && arguments[2] !== void 0 ? arguments[2] : {}, r = o.evt, i = xn(o, Hn);
    Ne.pluginEvent.bind(p)(e, n, ne({
        dragEl: c,
        parentEl: O,
        ghostEl: g,
        rootEl: T,
        nextEl: pe,
        lastDownEl: Be,
        cloneEl: C,
        cloneHidden: ue,
        dragStarted: Se,
        putSortable: Y,
        activeSortable: p.active,
        originalEvent: r,
        oldIndex: be,
        oldDraggableIndex: Oe,
        newIndex: $,
        newDraggableIndex: se,
        hideGhostForTarget: Zt,
        unhideGhostForTarget: Qt,
        cloneNowHidden: function () {
            ue = true;
        },
        cloneNowShown: function () {
            ue = false;
        },
        dispatchSortableEvent: function (l) {
            W({
                sortable: n,
                name: l,
                originalEvent: r
            });
        }
    }, i));
};
function W(t) {
    kn(ne({
        putSortable: Y,
        cloneEl: C,
        targetEl: c,
        rootEl: T,
        oldIndex: be,
        oldDraggableIndex: Oe,
        newIndex: $,
        newDraggableIndex: se
    }, t));
}
var c, O, g, T, pe, Be, C, ue, be, $, Oe, se, Me, Y, ve = false, Ge = false, je = [], de, Z, Qe, et, Pt, Mt, Se, me, Ie, Ae = false, Fe = false, ke, H, tt = [], lt = false, ze = [], Ve = typeof document != "undefined", Re = Lt, Ft = xe || ae ? "cssFloat" : "float", Ln = Ve && !Wt && !Lt && "draggable" in document.createElement("div"), qt = function () {
    if (Ve) {
        if (ae)
            return false;
        var t = document.createElement("x");
        return t.style.cssText = "pointer-events:auto", t.style.pointerEvents === "auto";
    }
}(), Kt = function (e, n) {
    var o = h(e), r = parseInt(o.width) - parseInt(o.paddingLeft) - parseInt(o.paddingRight) - parseInt(o.borderLeftWidth) - parseInt(o.borderRightWidth), i = we(e, 0, n), a = we(e, 1, n), l = i && h(i), s = a && h(a), u = l && parseInt(l.marginLeft) + parseInt(l.marginRight) + P(i).width, d = s && parseInt(s.marginLeft) + parseInt(s.marginRight) + P(a).width;
    if (o.display === "flex")
        return o.flexDirection === "column" || o.flexDirection === "column-reverse" ? "vertical" : "horizontal";
    if (o.display === "grid")
        return o.gridTemplateColumns.split(" ").length <= 1 ? "vertical" : "horizontal";
    if (i && l.float && l.float !== "none") {
        var f = l.float === "left" ? "left" : "right";
        return a && (s.clear === "both" || s.clear === f) ? "vertical" : "horizontal";
    }
    return i && (l.display === "block" || l.display === "flex" || l.display === "table" || l.display === "grid" || u >= r && o[Ft] === "none" || a && o[Ft] === "none" && u + d > r) ? "vertical" : "horizontal";
}, Wn = function (e, n, o) {
    var r = o ? e.left : e.top, i = o ? e.right : e.bottom, a = o ? e.width : e.height, l = o ? n.left : n.top, s = o ? n.right : n.bottom, u = o ? n.width : n.height;
    return r === l || i === s || r + a / 2 === l + u / 2;
}, Gn = function (e, n) {
    var o;
    return je.some(function (r) {
        var i = r[q].options.emptyInsertThreshold;
        if (!(!i || ht(r))) {
            var a = P(r), l = e >= a.left - i && e <= a.right + i, s = n >= a.top - i && n <= a.bottom + i;
            if (l && s)
                return o = r;
        }
    }), o;
}, Jt = function (e) {
    function n(i, a) {
        return function (l, s, u, d) {
            var f = l.options.group.name && s.options.group.name && l.options.group.name === s.options.group.name;
            if (i == null && (a || f))
                return true;
            if (i == null || i === false)
                return false;
            if (a && i === "clone")
                return i;
            if (typeof i == "function")
                return n(i(l, s, u, d), a)(l, s, u, d);
            var m = (a ? l : s).options.group.name;
            return i === true || typeof i == "string" && i === m || i.join && i.indexOf(m) > -1;
        };
    }
    var o = {}, r = e.group;
    (!r || Ye(r) != "object") && (r = {
        name: r
    }), o.name = r.name, o.checkPull = n(r.pull, true), o.checkPut = n(r.put), o.revertClone = r.revertClone, e.group = o;
}, Zt = function () {
    !qt && g && h(g, "display", "none");
}, Qt = function () {
    !qt && g && h(g, "display", "");
};
Ve && !Wt && document.addEventListener("click", function (t) {
    if (Ge)
        return t.preventDefault(), t.stopPropagation && t.stopPropagation(), t.stopImmediatePropagation && t.stopImmediatePropagation(), Ge = false, false;
}, true);
var he = function (e) {
    if (c) {
        e = e.touches ? e.touches[0] : e;
        var n = Gn(e.clientX, e.clientY);
        if (n) {
            var o = {};
            for (var r in e)
                e.hasOwnProperty(r) && (o[r] = e[r]);
            o.target = o.rootEl = n, o.preventDefault = void 0, o.stopPropagation = void 0, n[q]._onDragOver(o);
        }
    }
}, jn = function (e) {
    c && c.parentNode[q]._isOutsideThisEl(e.target);
};
function p(t, e) {
    if (!(t && t.nodeType && t.nodeType === 1))
        throw "Sortable: `el` must be an HTMLElement, not ".concat({}.toString.call(t));
    this.el = t, this.options = e = ie({}, e), t[q] = this;
    var n = {
        group: null,
        sort: true,
        disabled: false,
        store: null,
        handle: null,
        draggable: /^[uo]l$/i.test(t.nodeName) ? ">li" : ">*",
        swapThreshold: 1,
        // percentage; 0 <= x <= 1
        invertSwap: false,
        // invert always
        invertedSwapThreshold: null,
        // will be set to same as swapThreshold if default
        removeCloneOnHide: true,
        direction: function () {
            return Kt(t, this.options);
        },
        ghostClass: "sortable-ghost",
        chosenClass: "sortable-chosen",
        dragClass: "sortable-drag",
        ignore: "a, img",
        filter: null,
        preventOnFilter: true,
        animation: 0,
        easing: null,
        setData: function (a, l) {
            a.setData("Text", l.textContent);
        },
        dropBubble: false,
        dragoverBubble: false,
        dataIdAttr: "data-id",
        delay: 0,
        delayOnTouchOnly: false,
        touchStartThreshold: (Number.parseInt ? Number : window).parseInt(window.devicePixelRatio, 10) || 1,
        forceFallback: false,
        fallbackClass: "sortable-fallback",
        fallbackOnBody: false,
        fallbackTolerance: 0,
        fallbackOffset: {
            x: 0,
            y: 0
        },
        supportPointer: p.supportPointer !== false && "PointerEvent" in window && !Te,
        emptyInsertThreshold: 5
    };
    Ne.initializePlugins(this, t, n);
    for (var o in n)
        !(o in e) && (e[o] = n[o]);
    Jt(e);
    for (var r in this)
        r.charAt(0) === "_" && typeof this[r] == "function" && (this[r] = this[r].bind(this));
    this.nativeDraggable = e.forceFallback ? false : Ln, this.nativeDraggable && (this.options.touchStartThreshold = 1), e.supportPointer ? S(t, "pointerdown", this._onTapStart) : (S(t, "mousedown", this._onTapStart), S(t, "touchstart", this._onTapStart)), this.nativeDraggable && (S(t, "dragover", this), S(t, "dragenter", this)), je.push(this.el), e.store && e.store.get && this.sort(e.store.get(this) || []), ie(this, Xn());
}
p.prototype = /** @lends Sortable.prototype */
    {
        constructor: p,
        _isOutsideThisEl: function (e) {
            !this.el.contains(e) && e !== this.el && (me = null);
        },
        _getDirection: function (e, n) {
            return typeof this.options.direction == "function" ? this.options.direction.call(this, e, n, c) : this.options.direction;
        },
        _onTapStart: function (e) {
            if (e.cancelable) {
                var n = this, o = this.el, r = this.options, i = r.preventOnFilter, a = e.type, l = e.touches && e.touches[0] || e.pointerType && e.pointerType === "touch" && e, s = (l || e).target, u = e.target.shadowRoot && (e.path && e.path[0] || e.composedPath && e.composedPath()[0]) || s, d = r.filter;
                if (Zn(o), !c && !(/mousedown|pointerdown/.test(a) && e.button !== 0 || r.disabled) && !u.isContentEditable && !(!this.nativeDraggable && Te && s && s.tagName.toUpperCase() === "SELECT") && (s = Q(s, r.draggable, o, false), !(s && s.animated) && Be !== s)) {
                    if (be = J(s), Oe = J(s, r.draggable), typeof d == "function") {
                        if (d.call(this, e, s, this)) {
                            W({
                                sortable: n,
                                rootEl: u,
                                name: "filter",
                                targetEl: s,
                                toEl: o,
                                fromEl: o
                            }), z("filter", n, {
                                evt: e
                            }), i && e.cancelable && e.preventDefault();
                            return;
                        }
                    }
                    else if (d && (d = d.split(",").some(function (f) {
                        if (f = Q(u, f.trim(), o, false), f)
                            return W({
                                sortable: n,
                                rootEl: f,
                                name: "filter",
                                targetEl: s,
                                fromEl: o,
                                toEl: o
                            }), z("filter", n, {
                                evt: e
                            }), true;
                    }), d)) {
                        i && e.cancelable && e.preventDefault();
                        return;
                    }
                    r.handle && !Q(u, r.handle, o, false) || this._prepareDragStart(e, l, s);
                }
            }
        },
        _prepareDragStart: function (e, n, o) {
            var r = this, i = r.el, a = r.options, l = i.ownerDocument, s;
            if (o && !c && o.parentNode === i) {
                var u = P(o);
                if (T = i, c = o, O = c.parentNode, pe = c.nextSibling, Be = o, Me = a.group, p.dragged = c, de = {
                    target: c,
                    clientX: (n || e).clientX,
                    clientY: (n || e).clientY
                }, Pt = de.clientX - u.left, Mt = de.clientY - u.top, this._lastX = (n || e).clientX, this._lastY = (n || e).clientY, c.style["will-change"] = "all", s = function () {
                    if (z("delayEnded", r, {
                        evt: e
                    }), p.eventCanceled) {
                        r._onDrop();
                        return;
                    }
                    r._disableDelayedDragEvents(), !It && r.nativeDraggable && (c.draggable = true), r._triggerDragStart(e, n), W({
                        sortable: r,
                        name: "choose",
                        originalEvent: e
                    }), V(c, a.chosenClass, true);
                }, a.ignore.split(",").forEach(function (d) {
                    jt(c, d.trim(), nt);
                }), S(l, "dragover", he), S(l, "mousemove", he), S(l, "touchmove", he), S(l, "mouseup", r._onDrop), S(l, "touchend", r._onDrop), S(l, "touchcancel", r._onDrop), It && this.nativeDraggable && (this.options.touchStartThreshold = 4, c.draggable = true), z("delayStart", this, {
                    evt: e
                }), a.delay && (!a.delayOnTouchOnly || n) && (!this.nativeDraggable || !(xe || ae))) {
                    if (p.eventCanceled) {
                        this._onDrop();
                        return;
                    }
                    S(l, "mouseup", r._disableDelayedDrag), S(l, "touchend", r._disableDelayedDrag), S(l, "touchcancel", r._disableDelayedDrag), S(l, "mousemove", r._delayedDragTouchMoveHandler), S(l, "touchmove", r._delayedDragTouchMoveHandler), a.supportPointer && S(l, "pointermove", r._delayedDragTouchMoveHandler), r._dragStartTimer = setTimeout(s, a.delay);
                }
                else
                    s();
            }
        },
        _delayedDragTouchMoveHandler: function (e) {
            var n = e.touches ? e.touches[0] : e;
            Math.max(Math.abs(n.clientX - this._lastX), Math.abs(n.clientY - this._lastY)) >= Math.floor(this.options.touchStartThreshold / (this.nativeDraggable && window.devicePixelRatio || 1)) && this._disableDelayedDrag();
        },
        _disableDelayedDrag: function () {
            c && nt(c), clearTimeout(this._dragStartTimer), this._disableDelayedDragEvents();
        },
        _disableDelayedDragEvents: function () {
            var e = this.el.ownerDocument;
            E(e, "mouseup", this._disableDelayedDrag), E(e, "touchend", this._disableDelayedDrag), E(e, "touchcancel", this._disableDelayedDrag), E(e, "mousemove", this._delayedDragTouchMoveHandler), E(e, "touchmove", this._delayedDragTouchMoveHandler), E(e, "pointermove", this._delayedDragTouchMoveHandler);
        },
        _triggerDragStart: function (e, n) {
            n = n || e.pointerType == "touch" && e, !this.nativeDraggable || n ? this.options.supportPointer ? S(document, "pointermove", this._onTouchMove) : n ? S(document, "touchmove", this._onTouchMove) : S(document, "mousemove", this._onTouchMove) : (S(c, "dragend", this), S(T, "dragstart", this._onDragStart));
            try {
                document.selection ? He(function () {
                    document.selection.empty();
                }) : window.getSelection().removeAllRanges();
            }
            catch (o) {
            }
        },
        _dragStarted: function (e, n) {
            if (ve = false, T && c) {
                z("dragStarted", this, {
                    evt: n
                }), this.nativeDraggable && S(document, "dragover", jn);
                var o = this.options;
                !e && V(c, o.dragClass, false), V(c, o.ghostClass, true), p.active = this, e && this._appendGhost(), W({
                    sortable: this,
                    name: "start",
                    originalEvent: n
                });
            }
            else
                this._nulling();
        },
        _emulateDragOver: function () {
            if (Z) {
                this._lastX = Z.clientX, this._lastY = Z.clientY, Zt();
                for (var e = document.elementFromPoint(Z.clientX, Z.clientY), n = e; e && e.shadowRoot && (e = e.shadowRoot.elementFromPoint(Z.clientX, Z.clientY), e !== n);)
                    n = e;
                if (c.parentNode[q]._isOutsideThisEl(e), n)
                    do {
                        if (n[q]) {
                            var o = void 0;
                            if (o = n[q]._onDragOver({
                                clientX: Z.clientX,
                                clientY: Z.clientY,
                                target: e,
                                rootEl: n
                            }), o && !this.options.dragoverBubble)
                                break;
                        }
                        e = n;
                    } while (n = n.parentNode);
                Qt();
            }
        },
        _onTouchMove: function (e) {
            if (de) {
                var n = this.options, o = n.fallbackTolerance, r = n.fallbackOffset, i = e.touches ? e.touches[0] : e, a = g && ye(g, true), l = g && a && a.a, s = g && a && a.d, u = Re && H && Nt(H), d = (i.clientX - de.clientX + r.x) / (l || 1) + (u ? u[0] - tt[0] : 0) / (l || 1), f = (i.clientY - de.clientY + r.y) / (s || 1) + (u ? u[1] - tt[1] : 0) / (s || 1);
                if (!p.active && !ve) {
                    if (o && Math.max(Math.abs(i.clientX - this._lastX), Math.abs(i.clientY - this._lastY)) < o)
                        return;
                    this._onDragStart(e, true);
                }
                if (g) {
                    a ? (a.e += d - (Qe || 0), a.f += f - (et || 0)) : a = {
                        a: 1,
                        b: 0,
                        c: 0,
                        d: 1,
                        e: d,
                        f
                    };
                    var m = "matrix(".concat(a.a, ",").concat(a.b, ",").concat(a.c, ",").concat(a.d, ",").concat(a.e, ",").concat(a.f, ")");
                    h(g, "webkitTransform", m), h(g, "mozTransform", m), h(g, "msTransform", m), h(g, "transform", m), Qe = d, et = f, Z = i;
                }
                e.cancelable && e.preventDefault();
            }
        },
        _appendGhost: function () {
            if (!g) {
                var e = this.options.fallbackOnBody ? document.body : T, n = P(c, true, Re, true, e), o = this.options;
                if (Re) {
                    for (H = e; h(H, "position") === "static" && h(H, "transform") === "none" && H !== document;)
                        H = H.parentNode;
                    H !== document.body && H !== document.documentElement ? (H === document && (H = te()), n.top += H.scrollTop, n.left += H.scrollLeft) : H = te(), tt = Nt(H);
                }
                g = c.cloneNode(true), V(g, o.ghostClass, false), V(g, o.fallbackClass, true), V(g, o.dragClass, true), h(g, "transition", ""), h(g, "transform", ""), h(g, "box-sizing", "border-box"), h(g, "margin", 0), h(g, "top", n.top), h(g, "left", n.left), h(g, "width", n.width), h(g, "height", n.height), h(g, "opacity", "0.8"), h(g, "position", Re ? "absolute" : "fixed"), h(g, "zIndex", "100000"), h(g, "pointerEvents", "none"), p.ghost = g, e.appendChild(g), h(g, "transform-origin", Pt / parseInt(g.style.width) * 100 + "% " + Mt / parseInt(g.style.height) * 100 + "%");
            }
        },
        _onDragStart: function (e, n) {
            var o = this, r = e.dataTransfer, i = o.options;
            if (z("dragStart", this, {
                evt: e
            }), p.eventCanceled) {
                this._onDrop();
                return;
            }
            z("setupClone", this), p.eventCanceled || (C = Vt(c), C.removeAttribute("id"), C.draggable = false, C.style["will-change"] = "", this._hideClone(), V(C, this.options.chosenClass, false), p.clone = C), o.cloneId = He(function () {
                z("clone", o), !p.eventCanceled && (o.options.removeCloneOnHide || T.insertBefore(C, c), o._hideClone(), W({
                    sortable: o,
                    name: "clone"
                }));
            }), !n && V(c, i.dragClass, true), n ? (Ge = true, o._loopId = setInterval(o._emulateDragOver, 50)) : (E(document, "mouseup", o._onDrop), E(document, "touchend", o._onDrop), E(document, "touchcancel", o._onDrop), r && (r.effectAllowed = "move", i.setData && i.setData.call(o, r, c)), S(document, "drop", o), h(c, "transform", "translateZ(0)")), ve = true, o._dragStartId = He(o._dragStarted.bind(o, n, e)), S(document, "selectstart", o), Se = true, Te && h(document.body, "user-select", "none");
        },
        // Returns true - if no further action is needed (either inserted or another condition)
        _onDragOver: function (e) {
            var n = this.el, o = e.target, r, i, a, l = this.options, s = l.group, u = p.active, d = Me === s, f = l.sort, m = Y || u, y, b = this, w = false;
            if (lt)
                return;
            function L(ee, Ee) {
                z(ee, b, ne({
                    evt: e,
                    isOwner: d,
                    axis: y ? "vertical" : "horizontal",
                    revert: a,
                    dragRect: r,
                    targetRect: i,
                    canSort: f,
                    fromSortable: m,
                    target: o,
                    completed: R,
                    onMove: function (vt, rn) {
                        return Xe(T, n, c, r, vt, P(vt), e, rn);
                    },
                    changed: j
                }, Ee));
            }
            function G() {
                L("dragOverAnimationCapture"), b.captureAnimationState(), b !== m && m.captureAnimationState();
            }
            function R(ee) {
                return L("dragOverCompleted", {
                    insertion: ee
                }), ee && (d ? u._hideClone() : u._showClone(b), b !== m && (V(c, Y ? Y.options.ghostClass : u.options.ghostClass, false), V(c, l.ghostClass, true)), Y !== b && b !== p.active ? Y = b : b === p.active && Y && (Y = null), m === b && (b._ignoreWhileAnimating = o), b.animateAll(function () {
                    L("dragOverAnimationComplete"), b._ignoreWhileAnimating = null;
                }), b !== m && (m.animateAll(), m._ignoreWhileAnimating = null)), (o === c && !c.animated || o === n && !o.animated) && (me = null), !l.dragoverBubble && !e.rootEl && o !== document && (c.parentNode[q]._isOutsideThisEl(e.target), !ee && he(e)), !l.dragoverBubble && e.stopPropagation && e.stopPropagation(), w = true;
            }
            function j() {
                $ = J(c), se = J(c, l.draggable), W({
                    sortable: b,
                    name: "change",
                    toEl: n,
                    newIndex: $,
                    newDraggableIndex: se,
                    originalEvent: e
                });
            }
            if (e.preventDefault !== void 0 && e.cancelable && e.preventDefault(), o = Q(o, l.draggable, n, true), L("dragOver"), p.eventCanceled)
                return w;
            if (c.contains(e.target) || o.animated && o.animatingX && o.animatingY || b._ignoreWhileAnimating === o)
                return R(false);
            if (Ge = false, u && !l.disabled && (d ? f || (a = O !== T) : Y === this || (this.lastPutMode = Me.checkPull(this, u, c, e)) && s.checkPut(this, u, c, e))) {
                if (y = this._getDirection(e, o) === "vertical", r = P(c), L("dragOverValid"), p.eventCanceled)
                    return w;
                if (a)
                    return O = T, G(), this._hideClone(), L("revert"), p.eventCanceled || (pe ? T.insertBefore(c, pe) : T.appendChild(c)), R(true);
                var B = ht(n, l.draggable);
                if (!B || $n(e, y, this) && !B.animated) {
                    if (B === c)
                        return R(false);
                    if (B && n === e.target && (o = B), o && (i = P(o)), Xe(T, n, c, r, o, i, e, !!o) !== false)
                        return G(), B && B.nextSibling ? n.insertBefore(c, B.nextSibling) : n.appendChild(c), O = n, j(), R(true);
                }
                else if (B && Vn(e, y, this)) {
                    var X = we(n, 0, l, true);
                    if (X === c)
                        return R(false);
                    if (o = X, i = P(o), Xe(T, n, c, r, o, i, e, false) !== false)
                        return G(), n.insertBefore(c, X), O = n, j(), R(true);
                }
                else if (o.parentNode === n) {
                    i = P(o);
                    var K = 0, oe, le = c.parentNode !== n, k = !Wn(c.animated && c.toRect || r, o.animated && o.toRect || i, y), v = y ? "top" : "left", D = xt(o, "top", "top") || xt(c, "top", "top"), A = D ? D.scrollTop : void 0;
                    me !== o && (oe = i[v], Ae = false, Fe = !k && l.invertSwap || le), K = qn(e, o, i, y, k ? 1 : l.swapThreshold, l.invertedSwapThreshold == null ? l.swapThreshold : l.invertedSwapThreshold, Fe, me === o);
                    var I;
                    if (K !== 0) {
                        var _ = J(c);
                        do
                            _ -= K, I = O.children[_];
                        while (I && (h(I, "display") === "none" || I === g));
                    }
                    if (K === 0 || I === o)
                        return R(false);
                    me = o, Ie = K;
                    var x = o.nextElementSibling, M = false;
                    M = K === 1;
                    var F = Xe(T, n, c, r, o, i, e, M);
                    if (F !== false)
                        return (F === 1 || F === -1) && (M = F === 1), lt = true, setTimeout(Un, 30), G(), M && !x ? n.appendChild(c) : o.parentNode.insertBefore(c, M ? x : o), D && Ut(D, 0, A - D.scrollTop), O = c.parentNode, oe !== void 0 && !Fe && (ke = Math.abs(oe - P(o)[v])), j(), R(true);
                }
                if (n.contains(c))
                    return R(false);
            }
            return false;
        },
        _ignoreWhileAnimating: null,
        _offMoveEvents: function () {
            E(document, "mousemove", this._onTouchMove), E(document, "touchmove", this._onTouchMove), E(document, "pointermove", this._onTouchMove), E(document, "dragover", he), E(document, "mousemove", he), E(document, "touchmove", he);
        },
        _offUpEvents: function () {
            var e = this.el.ownerDocument;
            E(e, "mouseup", this._onDrop), E(e, "touchend", this._onDrop), E(e, "pointerup", this._onDrop), E(e, "touchcancel", this._onDrop), E(document, "selectstart", this);
        },
        _onDrop: function (e) {
            var n = this.el, o = this.options;
            if ($ = J(c), se = J(c, o.draggable), z("drop", this, {
                evt: e
            }), O = c && c.parentNode, $ = J(c), se = J(c, o.draggable), p.eventCanceled) {
                this._nulling();
                return;
            }
            ve = false, Fe = false, Ae = false, clearInterval(this._loopId), clearTimeout(this._dragStartTimer), st(this.cloneId), st(this._dragStartId), this.nativeDraggable && (E(document, "drop", this), E(n, "dragstart", this._onDragStart)), this._offMoveEvents(), this._offUpEvents(), Te && h(document.body, "user-select", ""), h(c, "transform", ""), e && (Se && (e.cancelable && e.preventDefault(), !o.dropBubble && e.stopPropagation()), g && g.parentNode && g.parentNode.removeChild(g), (T === O || Y && Y.lastPutMode !== "clone") && C && C.parentNode && C.parentNode.removeChild(C), c && (this.nativeDraggable && E(c, "dragend", this), nt(c), c.style["will-change"] = "", Se && !ve && V(c, Y ? Y.options.ghostClass : this.options.ghostClass, false), V(c, this.options.chosenClass, false), W({
                sortable: this,
                name: "unchoose",
                toEl: O,
                newIndex: null,
                newDraggableIndex: null,
                originalEvent: e
            }), T !== O ? ($ >= 0 && (W({
                rootEl: O,
                name: "add",
                toEl: O,
                fromEl: T,
                originalEvent: e
            }), W({
                sortable: this,
                name: "remove",
                toEl: O,
                originalEvent: e
            }), W({
                rootEl: O,
                name: "sort",
                toEl: O,
                fromEl: T,
                originalEvent: e
            }), W({
                sortable: this,
                name: "sort",
                toEl: O,
                originalEvent: e
            })), Y && Y.save()) : $ !== be && $ >= 0 && (W({
                sortable: this,
                name: "update",
                toEl: O,
                originalEvent: e
            }), W({
                sortable: this,
                name: "sort",
                toEl: O,
                originalEvent: e
            })), p.active && (($ == null || $ === -1) && ($ = be, se = Oe), W({
                sortable: this,
                name: "end",
                toEl: O,
                originalEvent: e
            }), this.save()))), this._nulling();
        },
        _nulling: function () {
            z("nulling", this), T = c = O = g = pe = C = Be = ue = de = Z = Se = $ = se = be = Oe = me = Ie = Y = Me = p.dragged = p.ghost = p.clone = p.active = null, ze.forEach(function (e) {
                e.checked = true;
            }), ze.length = Qe = et = 0;
        },
        handleEvent: function (e) {
            switch (e.type) {
                case "drop":
                case "dragend":
                    this._onDrop(e);
                    break;
                case "dragenter":
                case "dragover":
                    c && (this._onDragOver(e), zn(e));
                    break;
                case "selectstart":
                    e.preventDefault();
                    break;
            }
        },
        /**
         * Serializes the item into an array of string.
         * @returns {String[]}
         */
        toArray: function () {
            for (var e = [], n, o = this.el.children, r = 0, i = o.length, a = this.options; r < i; r++)
                n = o[r], Q(n, a.draggable, this.el, false) && e.push(n.getAttribute(a.dataIdAttr) || Jn(n));
            return e;
        },
        /**
         * Sorts the elements according to the array.
         * @param  {String[]}  order  order of the items
         */
        sort: function (e, n) {
            var o = {}, r = this.el;
            this.toArray().forEach(function (i, a) {
                var l = r.children[a];
                Q(l, this.options.draggable, r, false) && (o[i] = l);
            }, this), n && this.captureAnimationState(), e.forEach(function (i) {
                o[i] && (r.removeChild(o[i]), r.appendChild(o[i]));
            }), n && this.animateAll();
        },
        /**
         * Save the current sorting
         */
        save: function () {
            var e = this.options.store;
            e && e.set && e.set(this);
        },
        /**
         * For each element in the set, get the first element that matches the selector by testing the element itself and traversing up through its ancestors in the DOM tree.
         * @param   {HTMLElement}  el
         * @param   {String}       [selector]  default: `options.draggable`
         * @returns {HTMLElement|null}
         */
        closest: function (e, n) {
            return Q(e, n || this.options.draggable, this.el, false);
        },
        /**
         * Set/get option
         * @param   {string} name
         * @param   {*}      [value]
         * @returns {*}
         */
        option: function (e, n) {
            var o = this.options;
            if (n === void 0)
                return o[e];
            var r = Ne.modifyOption(this, e, n);
            typeof r != "undefined" ? o[e] = r : o[e] = n, e === "group" && Jt(o);
        },
        /**
         * Destroy
         */
        destroy: function () {
            z("destroy", this);
            var e = this.el;
            e[q] = null, E(e, "mousedown", this._onTapStart), E(e, "touchstart", this._onTapStart), E(e, "pointerdown", this._onTapStart), this.nativeDraggable && (E(e, "dragover", this), E(e, "dragenter", this)), Array.prototype.forEach.call(e.querySelectorAll("[draggable]"), function (n) {
                n.removeAttribute("draggable");
            }), this._onDrop(), this._disableDelayedDragEvents(), je.splice(je.indexOf(this.el), 1), this.el = e = null;
        },
        _hideClone: function () {
            if (!ue) {
                if (z("hideClone", this), p.eventCanceled)
                    return;
                h(C, "display", "none"), this.options.removeCloneOnHide && C.parentNode && C.parentNode.removeChild(C), ue = true;
            }
        },
        _showClone: function (e) {
            if (e.lastPutMode !== "clone") {
                this._hideClone();
                return;
            }
            if (ue) {
                if (z("showClone", this), p.eventCanceled)
                    return;
                c.parentNode == T && !this.options.group.revertClone ? T.insertBefore(C, c) : pe ? T.insertBefore(C, pe) : T.appendChild(C), this.options.group.revertClone && this.animate(c, C), h(C, "display", ""), ue = false;
            }
        }
    };
function zn(t) {
    t.dataTransfer && (t.dataTransfer.dropEffect = "move"), t.cancelable && t.preventDefault();
}
function Xe(t, e, n, o, r, i, a, l) {
    var s, u = t[q], d = u.options.onMove, f;
    return window.CustomEvent && !ae && !xe ? s = new CustomEvent("move", {
        bubbles: true,
        cancelable: true
    }) : (s = document.createEvent("Event"), s.initEvent("move", true, true)), s.to = e, s.from = t, s.dragged = n, s.draggedRect = o, s.related = r || e, s.relatedRect = i || P(e), s.willInsertAfter = l, s.originalEvent = a, t.dispatchEvent(s), d && (f = d.call(u, s, a)), f;
}
function nt(t) {
    t.draggable = false;
}
function Un() {
    lt = false;
}
function Vn(t, e, n) {
    var o = P(we(n.el, 0, n.options, true)), r = $t(n.el, n.options, g), i = 10;
    return e ? t.clientX < r.left - i || t.clientY < o.top && t.clientX < o.right : t.clientY < r.top - i || t.clientY < o.bottom && t.clientX < o.left;
}
function $n(t, e, n) {
    var o = P(ht(n.el, n.options.draggable)), r = $t(n.el, n.options, g), i = 10;
    return e ? t.clientX > r.right + i || t.clientY > o.bottom && t.clientX > o.left : t.clientY > r.bottom + i || t.clientX > o.right && t.clientY > o.top;
}
function qn(t, e, n, o, r, i, a, l) {
    var s = o ? t.clientY : t.clientX, u = o ? n.height : n.width, d = o ? n.top : n.left, f = o ? n.bottom : n.right, m = false;
    if (!a) {
        if (l && ke < u * r) {
            if (!Ae && (Ie === 1 ? s > d + u * i / 2 : s < f - u * i / 2) && (Ae = true), Ae)
                m = true;
            else if (Ie === 1 ? s < d + ke : s > f - ke)
                return -Ie;
        }
        else if (s > d + u * (1 - r) / 2 && s < f - u * (1 - r) / 2)
            return Kn(e);
    }
    return m = m || a, m && (s < d + u * i / 2 || s > f - u * i / 2) ? s > d + u / 2 ? 1 : -1 : 0;
}
function Kn(t) {
    return J(c) < J(t) ? 1 : -1;
}
function Jn(t) {
    for (var e = t.tagName + t.className + t.src + t.href + t.textContent, n = e.length, o = 0; n--;)
        o += e.charCodeAt(n);
    return o.toString(36);
}
function Zn(t) {
    ze.length = 0;
    for (var e = t.getElementsByTagName("input"), n = e.length; n--;) {
        var o = e[n];
        o.checked && ze.push(o);
    }
}
function He(t) {
    return setTimeout(t, 0);
}
function st(t) {
    return clearTimeout(t);
}
Ve && S(document, "touchmove", function (t) {
    (p.active || ve) && t.cancelable && t.preventDefault();
});
p.utils = {
    on: S,
    off: E,
    css: h,
    find: jt,
    is: function (e, n) {
        return !!Q(e, n, e, false);
    },
    extend: Fn,
    throttle: zt,
    closest: Q,
    toggleClass: V,
    clone: Vt,
    index: J,
    nextTick: He,
    cancelNextTick: st,
    detectDirection: Kt,
    getChild: we
};
p.get = function (t) {
    return t[q];
};
p.mount = function () {
    for (var t = arguments.length, e = new Array(t), n = 0; n < t; n++)
        e[n] = arguments[n];
    e[0].constructor === Array && (e = e[0]), e.forEach(function (o) {
        if (!o.prototype || !o.prototype.constructor)
            throw "Sortable: Mounted plugin must be a constructor function, not ".concat({}.toString.call(o));
        o.utils && (p.utils = ne(ne({}, p.utils), o.utils)), Ne.mount(o);
    });
};
p.create = function (t, e) {
    return new p(t, e);
};
p.version = Nn;
var N = [], De, ut, ct = false, ot, rt, Ue, _e;
function Qn() {
    function t() {
        this.defaults = {
            scroll: true,
            forceAutoScrollFallback: false,
            scrollSensitivity: 30,
            scrollSpeed: 10,
            bubbleScroll: true
        };
        for (var e in this)
            e.charAt(0) === "_" && typeof this[e] == "function" && (this[e] = this[e].bind(this));
    }
    return t.prototype = {
        dragStarted: function (n) {
            var o = n.originalEvent;
            this.sortable.nativeDraggable ? S(document, "dragover", this._handleAutoScroll) : this.options.supportPointer ? S(document, "pointermove", this._handleFallbackAutoScroll) : o.touches ? S(document, "touchmove", this._handleFallbackAutoScroll) : S(document, "mousemove", this._handleFallbackAutoScroll);
        },
        dragOverCompleted: function (n) {
            var o = n.originalEvent;
            !this.options.dragOverBubble && !o.rootEl && this._handleAutoScroll(o);
        },
        drop: function () {
            this.sortable.nativeDraggable ? E(document, "dragover", this._handleAutoScroll) : (E(document, "pointermove", this._handleFallbackAutoScroll), E(document, "touchmove", this._handleFallbackAutoScroll), E(document, "mousemove", this._handleFallbackAutoScroll)), Rt(), Le(), Rn();
        },
        nulling: function () {
            Ue = ut = De = ct = _e = ot = rt = null, N.length = 0;
        },
        _handleFallbackAutoScroll: function (n) {
            this._handleAutoScroll(n, true);
        },
        _handleAutoScroll: function (n, o) {
            var r = this, i = (n.touches ? n.touches[0] : n).clientX, a = (n.touches ? n.touches[0] : n).clientY, l = document.elementFromPoint(i, a);
            if (Ue = n, o || this.options.forceAutoScrollFallback || xe || ae || Te) {
                it(n, this.options, l, o);
                var s = ce(l, true);
                ct && (!_e || i !== ot || a !== rt) && (_e && Rt(), _e = setInterval(function () {
                    var u = ce(document.elementFromPoint(i, a), true);
                    u !== s && (s = u, Le()), it(n, r.options, u, o);
                }, 10), ot = i, rt = a);
            }
            else {
                if (!this.options.bubbleScroll || ce(l, true) === te()) {
                    Le();
                    return;
                }
                it(n, this.options, ce(l, false), false);
            }
        }
    }, ie(t, {
        pluginName: "scroll",
        initializeByDefault: true
    });
}
function Le() {
    N.forEach(function (t) {
        clearInterval(t.pid);
    }), N = [];
}
function Rt() {
    clearInterval(_e);
}
var it = zt(function (t, e, n, o) {
    if (e.scroll) {
        var r = (t.touches ? t.touches[0] : t).clientX, i = (t.touches ? t.touches[0] : t).clientY, a = e.scrollSensitivity, l = e.scrollSpeed, s = te(), u = false, d;
        ut !== n && (ut = n, Le(), De = e.scroll, d = e.scrollFn, De === true && (De = ce(n, true)));
        var f = 0, m = De;
        do {
            var y = m, b = P(y), w = b.top, L = b.bottom, G = b.left, R = b.right, j = b.width, B = b.height, X = void 0, K = void 0, oe = y.scrollWidth, le = y.scrollHeight, k = h(y), v = y.scrollLeft, D = y.scrollTop;
            y === s ? (X = j < oe && (k.overflowX === "auto" || k.overflowX === "scroll" || k.overflowX === "visible"), K = B < le && (k.overflowY === "auto" || k.overflowY === "scroll" || k.overflowY === "visible")) : (X = j < oe && (k.overflowX === "auto" || k.overflowX === "scroll"), K = B < le && (k.overflowY === "auto" || k.overflowY === "scroll"));
            var A = X && (Math.abs(R - r) <= a && v + j < oe) - (Math.abs(G - r) <= a && !!v), I = K && (Math.abs(L - i) <= a && D + B < le) - (Math.abs(w - i) <= a && !!D);
            if (!N[f])
                for (var _ = 0; _ <= f; _++)
                    N[_] || (N[_] = {});
            (N[f].vx != A || N[f].vy != I || N[f].el !== y) && (N[f].el = y, N[f].vx = A, N[f].vy = I, clearInterval(N[f].pid), (A != 0 || I != 0) && (u = true, N[f].pid = setInterval(function () {
                o && this.layer === 0 && p.active._onTouchMove(Ue);
                var x = N[this.layer].vy ? N[this.layer].vy * l : 0, M = N[this.layer].vx ? N[this.layer].vx * l : 0;
                typeof d == "function" && d.call(p.dragged.parentNode[q], M, x, t, Ue, N[this.layer].el) !== "continue" || Ut(N[this.layer].el, M, x);
            }.bind({
                layer: f
            }), 24))), f++;
        } while (e.bubbleScroll && m !== s && (m = ce(m, false)));
        ct = u;
    }
}, 30), en = function (e) {
    var n = e.originalEvent, o = e.putSortable, r = e.dragEl, i = e.activeSortable, a = e.dispatchSortableEvent, l = e.hideGhostForTarget, s = e.unhideGhostForTarget;
    if (n) {
        var u = o || i;
        l();
        var d = n.changedTouches && n.changedTouches.length ? n.changedTouches[0] : n, f = document.elementFromPoint(d.clientX, d.clientY);
        s(), u && !u.el.contains(f) && (a("spill"), this.onSpill({
            dragEl: r,
            putSortable: o
        }));
    }
};
function pt() {
}
pt.prototype = {
    startIndex: null,
    dragStart: function (e) {
        var n = e.oldDraggableIndex;
        this.startIndex = n;
    },
    onSpill: function (e) {
        var n = e.dragEl, o = e.putSortable;
        this.sortable.captureAnimationState(), o && o.captureAnimationState();
        var r = we(this.sortable.el, this.startIndex, this.options);
        r ? this.sortable.el.insertBefore(n, r) : this.sortable.el.appendChild(n), this.sortable.animateAll(), o && o.animateAll();
    },
    drop: en
};
ie(pt, {
    pluginName: "revertOnSpill"
});
function gt() {
}
gt.prototype = {
    onSpill: function (e) {
        var n = e.dragEl, o = e.putSortable, r = o || this.sortable;
        r.captureAnimationState(), n.parentNode && n.parentNode.removeChild(n), r.animateAll();
    },
    drop: en
};
ie(gt, {
    pluginName: "removeOnSpill"
});
p.mount(new Qn());
p.mount(gt, pt);
function eo(t) {
    return t == null ? t : JSON.parse(JSON.stringify(t));
}
function to(t) {
    getCurrentInstance() && onUnmounted(t);
}
function no(t) {
    getCurrentInstance() ? onMounted(t) : nextTick(t);
}
let tn = null, nn = null;
function Xt(t = null, e = null) {
    tn = t, nn = e;
}
function oo() {
    return {
        data: tn,
        clonedData: nn
    };
}
const Yt = Symbol("cloneElement");
function on(...t) {
    var le, k;
    const e = (le = getCurrentInstance()) == null ? void 0 : le.proxy;
    let n = null;
    const o = t[0];
    let [, r, i] = t;
    Array.isArray(unref(r)) || (i = r, r = null);
    let a = null;
    const { immediate: l = true, clone: s = eo, forceFallback: u, fallbackOnBody: d, customUpdate: f } = (k = unref(i)) != null ? k : {};
    function m(v) {
        var F;
        const { from: D, oldIndex: A, item: I } = v, _ = Array.from(D.childNodes);
        n = u && !d ? _.slice(0, -1) : _;
        const x = unref((F = unref(r)) == null ? void 0 : F[A]), M = s(x);
        Xt(x, M), I[Yt] = M;
    }
    function y(v) {
        const D = v.item[Yt];
        if (!wn(D)) {
            if (Ke(v.item), isRef(r)) {
                const A = [...unref(r)];
                r.value = _t(A, v.newDraggableIndex, D);
                return;
            }
            _t(unref(r), v.newDraggableIndex, D);
        }
    }
    function b(v) {
        const { from: D, item: A, oldIndex: I, oldDraggableIndex: _, pullMode: x, clone: M } = v;
        if (Tt(D, A, I), x === "clone") {
            Ke(M);
            return;
        }
        if (isRef(r)) {
            const F = [...unref(r)];
            r.value = Dt(F, _);
            return;
        }
        Dt(unref(r), _);
    }
    function w(v) {
        if (f) {
            f(v);
            return;
        }
        const { from: D, item: A, oldIndex: I, oldDraggableIndex: _, newDraggableIndex: x } = v;
        if (Ke(A), Tt(D, A, I), isRef(r)) {
            const M = [...unref(r)];
            r.value = St(M, _, x);
            return;
        }
        St(unref(r), _, x);
    }
    function L(v) {
        const { newIndex: D, oldIndex: A, from: I, to: _ } = v;
        let x = null;
        const M = D === A && I === _;
        try {
            if (M) {
                let F = null;
                n == null || n.some((ee, Ee) => {
                    if (F && (n == null ? void 0 : n.length) !== _.childNodes.length)
                        return I.insertBefore(F, ee.nextSibling), !0;
                    const mt = _.childNodes[Ee];
                    F = _ == null ? void 0 : _.replaceChild(ee, mt);
                });
            }
        }
        catch (F) {
            x = F;
        }
        finally {
            n = null;
        }
        nextTick(() => {
            if (Xt(), x)
                throw x;
        });
    }
    const G = {
        onUpdate: w,
        onStart: m,
        onAdd: y,
        onRemove: b,
        onEnd: L
    };
    function R(v) {
        const D = unref(o);
        return v || (v = En(D) ? Sn(D, e == null ? void 0 : e.$el) : D), v && !Tn(v) && (v = v.$el), v || vn("Root element not found"), v;
    }
    function j() {
        var I;
        const _ = (I = unref(i)) != null ? I : {}, { immediate: v, clone: D } = _, A = $e(_, ["immediate", "clone"]);
        return Ct(A, (x, M) => {
            Cn(x) && (A[x] = (F, ...ee) => {
                const Ee = oo();
                return On(F, Ee), M(F, ...ee);
            });
        }), _n(r === null ? {} : G, A);
    }
    const B = (v) => {
        v = R(v), a && X.destroy(), a = new p(v, j());
    };
    watch(() => i, () => {
        a && Ct(j(), (v, D) => {
            a == null || a.option(v, D);
        });
    }, { deep: true });
    const X = {
        option: (v, D) => a == null ? void 0 : a.option(v, D),
        destroy: () => {
            a == null || a.destroy(), a = null;
        },
        save: () => a == null ? void 0 : a.save(),
        toArray: () => a == null ? void 0 : a.toArray(),
        closest: (...v) => a == null ? void 0 : a.closest(...v)
    }, K = () => X == null ? void 0 : X.option("disabled", true), oe = () => X == null ? void 0 : X.option("disabled", false);
    return no(() => {
        l && B();
    }), to(X.destroy), fe({ start: B, pause: K, resume: oe }, X);
}
const ft = [
    "update",
    "start",
    "add",
    "remove",
    "choose",
    "unchoose",
    "end",
    "sort",
    "filter",
    "clone",
    "move",
    "change"
], ro = [
    "clone",
    "animation",
    "ghostClass",
    "group",
    "sort",
    "disabled",
    "store",
    "handle",
    "draggable",
    "swapThreshold",
    "invertSwap",
    "invertedSwapThreshold",
    "removeCloneOnHide",
    "direction",
    "chosenClass",
    "dragClass",
    "ignore",
    "filter",
    "preventOnFilter",
    "easing",
    "setData",
    "dropBubble",
    "dragoverBubble",
    "dataIdAttr",
    "delay",
    "delayOnTouchOnly",
    "touchStartThreshold",
    "forceFallback",
    "fallbackClass",
    "fallbackOnBody",
    "fallbackTolerance",
    "fallbackOffset",
    "supportPointer",
    "emptyInsertThreshold",
    "scroll",
    "forceAutoScrollFallback",
    "scrollSensitivity",
    "scrollSpeed",
    "bubbleScroll",
    "modelValue",
    "tag",
    "target",
    "customUpdate",
    ...ft.map((t) => `on${t.replace(/^\S/, (e) => e.toUpperCase())}`)
], lo = defineComponent({
    name: "VueDraggable",
    model: {
        prop: "modelValue",
        event: "update:modelValue"
    },
    props: ro,
    emits: ["update:modelValue", ...ft],
    setup(t, { slots: e, emit: n, expose: o, attrs: r }) {
        const i = ft.reduce((d, f) => {
            const m = `on${f.replace(/^\S/, (y) => y.toUpperCase())}`;
            return d[m] = (...y) => n(f, ...y), d;
        }, {}), a = computed(() => {
            const y = toRefs(t), { modelValue: d } = y, f = $e(y, ["modelValue"]), m = Object.entries(f).reduce((b, [w, L]) => {
                const G = unref(L);
                return G !== void 0 && (b[w] = G), b;
            }, {});
            return fe(fe({}, i), yn(fe(fe({}, r), m)));
        }), l = computed({
            get: () => t.modelValue,
            set: (d) => n("update:modelValue", d)
        }), s = ref(), u = reactive(on(t.target || s, l, a));
        return o(u), () => {
            var d;
            return h$1(t.tag || "div", { ref: s }, (d = e == null ? void 0 : e.default) == null ? void 0 : d.call(e, u));
        };
    }
});
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
const ProjectDetailHeader = /*#__PURE__*/ _export_sfc(_sfc_main$4, [['__scopeId', "data-v-bf397d7c"]]);
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
const ProjectWikiTab = /*#__PURE__*/ _export_sfc(_sfc_main$1, [['__scopeId', "data-v-4525fcbe"]]);
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
const _hoisted_9 = { class: "board-actions" };
const _hoisted_10 = { class: "search-box" };
const _hoisted_11 = ["value"];
const _hoisted_12 = ["value"];
const _hoisted_13 = { class: "kanban-board" };
const _hoisted_14 = { class: "kanban-column__header" };
const _hoisted_15 = { class: "count-badge" };
const _hoisted_16 = ["data-id", "onClick"];
const _hoisted_17 = { class: "kanban-card__top" };
const _hoisted_18 = ["onDblclick"];
const _hoisted_19 = { class: "task-card-actions" };
const _hoisted_20 = {
    key: 0,
    class: "task-menu-dropdown"
};
const _hoisted_21 = ["onClick"];
const _hoisted_22 = {
    key: 0,
    class: "dropdown-content glass-card"
};
const _hoisted_23 = ["onClick"];
const _hoisted_24 = ["onClick"];
const _hoisted_25 = { class: "assignee-text" };
const _hoisted_26 = { class: "kanban-card__meta" };
const _hoisted_27 = { class: "meta-item" };
const _hoisted_28 = {
    key: 0,
    class: "overdue-tag"
};
const _hoisted_29 = {
    key: 0,
    class: "empty-column-placeholder"
};
const _hoisted_30 = { class: "task-detail-panel glass-card" };
const _hoisted_31 = { class: "panel-heading" };
const _hoisted_32 = {
    key: 0,
    class: "task-id-badge"
};
const _hoisted_33 = {
    key: 0,
    class: "comment-list"
};
const _hoisted_34 = { class: "time-tracking-section" };
const _hoisted_35 = { class: "section-header" };
const _hoisted_36 = { class: "timer-display glass-card" };
const _hoisted_37 = {
    key: 0,
    class: "timer-active"
};
const _hoisted_38 = {
    key: 1,
    class: "timer-idle"
};
const _hoisted_39 = {
    key: 0,
    class: "manual-log-form glass-card"
};
const _hoisted_40 = { class: "form-row" };
const _hoisted_41 = {
    key: 0,
    class: "entry-history"
};
const _hoisted_42 = { class: "minutes-badge" };
const _hoisted_43 = { class: "time-date" };
const _hoisted_44 = { class: "attachment-section glass-card" };
const _hoisted_45 = { class: "section-header" };
const _hoisted_46 = { class: "upload-pill" };
const _hoisted_47 = { class: "attachment-grid" };
const _hoisted_48 = { class: "file-info" };
const _hoisted_49 = { class: "truncate" };
const _hoisted_50 = ["onClick"];
const _hoisted_51 = { class: "discussion-section" };
const _hoisted_52 = { class: "comments-scroll" };
const _hoisted_53 = { class: "bubble-top" };
const _hoisted_54 = { class: "bubble-time" };
const _hoisted_55 = ["onClick"];
const _hoisted_56 = ["disabled"];
const _hoisted_57 = {
    key: 1,
    class: "empty-state-panel"
};
const _hoisted_58 = {
    key: 3,
    class: "tab-pane reveal"
};
const _hoisted_59 = {
    key: 4,
    class: "tab-pane reveal"
};
const _sfc_main = /*@__PURE__*/ defineComponent({
    __name: 'ProjectDetailPage',
    setup(__props) {
        const { activeProjectTab, activeTaskMenu, addManualTimeEntry, addMember, attachments, beginEditTask, closeProjectDetails, comments, createTask, createTaskOpen, currentUser, deleteAttachment, deleteComment, deleteTask, displayStatus, formatDate, formatFileSize, formatTime, isProjectAdmin, isTaskOverdue, moveTask, newComment, newTaskAssigneeId, newTaskDescription, newTaskDueDate, newTaskPriority, newTaskTitle, nextStatuses, openChatWithPrompt, priorities, quickEditTaskTitle, removeMember, selectTaskInProject, selectedProject, selectedProjectMembers, selectedProjectStats, selectedTask, statusTone, statusColumns, submitComment, tabs, tasksByStatus, toggleTaskMenu, updateMemberRole, uploadAttachment, users, taskSearchQuery, taskBeingQuickEditedId, timeEntries, activeTimer, startTimer, stopTimer, } = useDashboardContext();
        const quickEditTitle = ref('');
        const manualMinutes = ref(0);
        const manualNote = ref('');
        const showManualForm = ref(false);
        function startQuickEdit(task) {
            taskBeingQuickEditedId.value = task.id;
            quickEditTitle.value = task.title;
        }
        async function saveQuickEdit() {
            if (!taskBeingQuickEditedId.value)
                return;
            const saved = await quickEditTaskTitle(taskBeingQuickEditedId.value, quickEditTitle.value);
            if (saved)
                taskBeingQuickEditedId.value = null;
        }
        async function submitManualEntry() {
            if (!selectedTask.value || manualMinutes.value <= 0)
                return;
            const saved = await addManualTimeEntry(selectedTask.value.id, manualMinutes.value, manualNote.value);
            if (saved) {
                manualMinutes.value = 0;
                manualNote.value = '';
                showManualForm.value = false;
            }
        }
        const onDragEnd = async (evt, status) => {
            const taskId = evt.item.getAttribute('data-id');
            const task = tasksByStatus(status).find(t => t.id === taskId);
            if (task && evt.to !== evt.from) {
                const newStatus = evt.to.getAttribute('data-status');
                await moveTask(task, newStatus);
            }
        };
        // Keyboard Shortcuts
        const handleKeyDown = (e) => {
            if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement)
                return;
            if (e.key.toLowerCase() === 'n') {
                e.preventDefault();
                createTaskOpen.value = true;
            }
            if (e.key === 'Escape') {
                createTaskOpen.value = false;
                showManualForm.value = false;
                activeTaskMenu.value = null;
            }
        };
        onMounted(() => window.addEventListener('keydown', handleKeyDown));
        onUnmounted(() => window.removeEventListener('keydown', handleKeyDown));
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
                                    _cache[21] || (_cache[21] = createBaseVNode("div", null, [
                                        createBaseVNode("span", null, "Tasks"),
                                        createBaseVNode("h2", null, "Board")
                                    ], -1)),
                                    createBaseVNode("div", _hoisted_9, [
                                        createBaseVNode("div", _hoisted_10, [
                                            createVNode(unref(Search), { size: 16 }),
                                            withDirectives(createBaseVNode("input", {
                                                "onUpdate:modelValue": _cache[1] || (_cache[1] = ($event) => (isRef(taskSearchQuery) ? (taskSearchQuery).value = $event : null)),
                                                type: "text",
                                                placeholder: "Tìm task (N: mới)..."
                                            }, null, 512), [
                                                [vModelText, unref(taskSearchQuery)]
                                            ])
                                        ]),
                                        createBaseVNode("button", {
                                            class: "primary-button primary-button--compact",
                                            type: "button",
                                            onClick: _cache[2] || (_cache[2] = ($event) => (createTaskOpen.value = !unref(createTaskOpen)))
                                        }, [
                                            createVNode(unref(Plus), { size: 16 }),
                                            _cache[20] || (_cache[20] = createBaseVNode("span", null, "Task", -1))
                                        ])
                                    ])
                                ]),
                                createVNode(Transition, { name: "expand" }, {
                                    default: withCtx(() => [
                                        (unref(createTaskOpen))
                                            ? (openBlock(), createElementBlock("form", {
                                                key: 0,
                                                class: "task-create-form glass-card",
                                                onSubmit: _cache[8] || (_cache[8] = withModifiers(
                                                //@ts-ignore
                                                (...args) => (unref(createTask) && unref(createTask)(...args)), ["prevent"]))
                                            }, [
                                                withDirectives(createBaseVNode("input", {
                                                    "onUpdate:modelValue": _cache[3] || (_cache[3] = ($event) => (isRef(newTaskTitle) ? (newTaskTitle).value = $event : null)),
                                                    type: "text",
                                                    placeholder: "Task title",
                                                    required: ""
                                                }, null, 512), [
                                                    [vModelText, unref(newTaskTitle)]
                                                ]),
                                                withDirectives(createBaseVNode("input", {
                                                    "onUpdate:modelValue": _cache[4] || (_cache[4] = ($event) => (isRef(newTaskDescription) ? (newTaskDescription).value = $event : null)),
                                                    type: "text",
                                                    placeholder: "Description"
                                                }, null, 512), [
                                                    [vModelText, unref(newTaskDescription)]
                                                ]),
                                                withDirectives(createBaseVNode("select", {
                                                    "onUpdate:modelValue": _cache[5] || (_cache[5] = ($event) => (isRef(newTaskPriority) ? (newTaskPriority).value = $event : null))
                                                }, [
                                                    (openBlock(true), createElementBlock(Fragment, null, renderList(unref(priorities), (priority) => {
                                                        return (openBlock(), createElementBlock("option", {
                                                            key: priority,
                                                            value: priority
                                                        }, toDisplayString(priority), 9, _hoisted_11));
                                                    }), 128))
                                                ], 512), [
                                                    [vModelSelect, unref(newTaskPriority)]
                                                ]),
                                                withDirectives(createBaseVNode("select", {
                                                    "onUpdate:modelValue": _cache[6] || (_cache[6] = ($event) => (isRef(newTaskAssigneeId) ? (newTaskAssigneeId).value = $event : null))
                                                }, [
                                                    _cache[22] || (_cache[22] = createBaseVNode("option", { value: "" }, "Unassigned", -1)),
                                                    (openBlock(true), createElementBlock(Fragment, null, renderList(unref(users), (user) => {
                                                        return (openBlock(), createElementBlock("option", {
                                                            key: user.id,
                                                            value: user.id
                                                        }, toDisplayString(user.fullName), 9, _hoisted_12));
                                                    }), 128))
                                                ], 512), [
                                                    [vModelSelect, unref(newTaskAssigneeId)]
                                                ]),
                                                withDirectives(createBaseVNode("input", {
                                                    "onUpdate:modelValue": _cache[7] || (_cache[7] = ($event) => (isRef(newTaskDueDate) ? (newTaskDueDate).value = $event : null)),
                                                    type: "date"
                                                }, null, 512), [
                                                    [vModelText, unref(newTaskDueDate)]
                                                ]),
                                                _cache[23] || (_cache[23] = createBaseVNode("button", {
                                                    class: "primary-button primary-button--compact",
                                                    type: "submit"
                                                }, "Create", -1))
                                            ], 32))
                                            : createCommentVNode("", true)
                                    ]),
                                    _: 1
                                }),
                                createBaseVNode("div", _hoisted_13, [
                                    (openBlock(true), createElementBlock(Fragment, null, renderList(unref(statusColumns), (status) => {
                                        return (openBlock(), createElementBlock("section", {
                                            key: status,
                                            class: "kanban-column"
                                        }, [
                                            createBaseVNode("div", _hoisted_14, [
                                                createBaseVNode("strong", null, toDisplayString(unref(displayStatus)(status)), 1),
                                                createBaseVNode("span", _hoisted_15, toDisplayString(unref(tasksByStatus)(status).length), 1)
                                            ]),
                                            createVNode(unref(lo), {
                                                modelValue: unref(selectedProject).tasks,
                                                "onUpdate:modelValue": _cache[11] || (_cache[11] = ($event) => ((unref(selectedProject).tasks) = $event)),
                                                animation: 200,
                                                group: "tasks",
                                                "ghost-class": "ghost-card",
                                                "drag-class": "dragging-card",
                                                class: "kanban-column__list",
                                                "data-status": status,
                                                onEnd: (evt) => onDragEnd(evt, status)
                                            }, {
                                                default: withCtx(() => [
                                                    (openBlock(true), createElementBlock(Fragment, null, renderList(unref(tasksByStatus)(status), (task) => {
                                                        return (openBlock(), createElementBlock("article", {
                                                            key: task.id,
                                                            class: normalizeClass(["kanban-card draggable-item", { 'is-selected': unref(selectedTask)?.id === task.id }]),
                                                            "data-id": task.id,
                                                            onClick: ($event) => (unref(selectTaskInProject)(task.id))
                                                        }, [
                                                            createBaseVNode("div", _hoisted_17, [
                                                                (unref(taskBeingQuickEditedId) === task.id)
                                                                    ? withDirectives((openBlock(), createElementBlock("input", {
                                                                        key: 0,
                                                                        "onUpdate:modelValue": _cache[9] || (_cache[9] = ($event) => ((quickEditTitle).value = $event)),
                                                                        type: "text",
                                                                        class: "quick-edit-input",
                                                                        onBlur: saveQuickEdit,
                                                                        onKeyup: withKeys(saveQuickEdit, ["enter"]),
                                                                        onClick: _cache[10] || (_cache[10] = withModifiers(() => { }, ["stop"]))
                                                                    }, null, 544)), [
                                                                        [vModelText, quickEditTitle.value]
                                                                    ])
                                                                    : (openBlock(), createElementBlock("strong", {
                                                                        key: 1,
                                                                        onDblclick: withModifiers(($event) => (startQuickEdit(task)), ["stop"])
                                                                    }, toDisplayString(task.title), 41, _hoisted_18)),
                                                                createBaseVNode("div", _hoisted_19, [
                                                                    createBaseVNode("span", {
                                                                        class: normalizeClass(`priority priority--${task.priority.toLowerCase()}`)
                                                                    }, toDisplayString(task.priority), 3),
                                                                    (unref(isProjectAdmin))
                                                                        ? (openBlock(), createElementBlock("div", _hoisted_20, [
                                                                            createBaseVNode("button", {
                                                                                class: "icon-button icon-button--small",
                                                                                type: "button",
                                                                                onClick: withModifiers(($event) => (unref(toggleTaskMenu)(task.id)), ["stop"])
                                                                            }, [
                                                                                createVNode(unref(Ellipsis), { size: 14 })
                                                                            ], 8, _hoisted_21),
                                                                            (unref(activeTaskMenu) === task.id)
                                                                                ? (openBlock(), createElementBlock("div", _hoisted_22, [
                                                                                    createBaseVNode("button", {
                                                                                        type: "button",
                                                                                        onClick: withModifiers(($event) => (unref(beginEditTask)(task)), ["stop"])
                                                                                    }, "Sửa", 8, _hoisted_23),
                                                                                    createBaseVNode("button", {
                                                                                        type: "button",
                                                                                        style: { "color": "var(--peach-500)" },
                                                                                        onClick: withModifiers(($event) => (unref(deleteTask)(task.id)), ["stop"])
                                                                                    }, "Xóa", 8, _hoisted_24)
                                                                                ]))
                                                                                : createCommentVNode("", true)
                                                                        ]))
                                                                        : createCommentVNode("", true)
                                                                ])
                                                            ]),
                                                            createBaseVNode("p", _hoisted_25, toDisplayString(task.assigneeName || 'Unassigned') + " • " + toDisplayString(unref(formatDate)(task.dueDate)), 1),
                                                            createBaseVNode("div", _hoisted_26, [
                                                                createBaseVNode("span", _hoisted_27, [
                                                                    createVNode(unref(MessageSquare), { size: 12 }),
                                                                    createTextVNode(" " + toDisplayString(task.commentCount), 1)
                                                                ]),
                                                                (unref(isTaskOverdue)(task))
                                                                    ? (openBlock(), createElementBlock("span", _hoisted_28, "Overdue"))
                                                                    : createCommentVNode("", true)
                                                            ])
                                                        ], 10, _hoisted_16));
                                                    }), 128)),
                                                    (unref(tasksByStatus)(status).length === 0)
                                                        ? (openBlock(), createElementBlock("div", _hoisted_29, "Drop task here"))
                                                        : createCommentVNode("", true)
                                                ]),
                                                _: 2
                                            }, 1032, ["modelValue", "data-status", "onEnd"])
                                        ]));
                                    }), 128))
                                ])
                            ]),
                            createBaseVNode("section", _hoisted_30, [
                                createBaseVNode("div", _hoisted_31, [
                                    createBaseVNode("div", null, [
                                        _cache[24] || (_cache[24] = createBaseVNode("span", null, "Task detail", -1)),
                                        createBaseVNode("h2", null, toDisplayString(unref(selectedTask)?.title ?? 'No task selected'), 1)
                                    ]),
                                    (unref(selectedTask))
                                        ? (openBlock(), createElementBlock("div", _hoisted_32, "#" + toDisplayString(unref(selectedTask).id.slice(0, 4)), 1))
                                        : createCommentVNode("", true)
                                ]),
                                (unref(selectedTask))
                                    ? (openBlock(), createElementBlock("div", _hoisted_33, [
                                        createBaseVNode("div", _hoisted_34, [
                                            createBaseVNode("div", _hoisted_35, [
                                                createVNode(unref(Clock), { size: 16 }),
                                                _cache[25] || (_cache[25] = createBaseVNode("strong", null, "Activity Log", -1))
                                            ]),
                                            createBaseVNode("div", _hoisted_36, [
                                                (unref(activeTimer))
                                                    ? (openBlock(), createElementBlock("div", _hoisted_37, [
                                                        _cache[28] || (_cache[28] = createBaseVNode("div", { class: "timer-pulse" }, null, -1)),
                                                        createBaseVNode("span", null, [
                                                            _cache[26] || (_cache[26] = createTextVNode("Ghi giờ: ", -1)),
                                                            createBaseVNode("strong", null, toDisplayString(unref(activeTimer).taskTitle), 1)
                                                        ]),
                                                        createBaseVNode("button", {
                                                            class: "stop-pill",
                                                            onClick: _cache[12] || (_cache[12] = ($event) => (unref(stopTimer)(unref(activeTimer).id)))
                                                        }, [
                                                            createVNode(unref(Square), {
                                                                size: 14,
                                                                fill: "currentColor"
                                                            }),
                                                            _cache[27] || (_cache[27] = createTextVNode(" Stop ", -1))
                                                        ])
                                                    ]))
                                                    : (openBlock(), createElementBlock("div", _hoisted_38, [
                                                        createBaseVNode("button", {
                                                            class: "start-pill",
                                                            onClick: _cache[13] || (_cache[13] = ($event) => (unref(startTimer)(unref(selectedTask).id)))
                                                        }, [
                                                            createVNode(unref(Play), {
                                                                size: 14,
                                                                fill: "currentColor"
                                                            }),
                                                            _cache[29] || (_cache[29] = createTextVNode(" Start ", -1))
                                                        ]),
                                                        createBaseVNode("button", {
                                                            class: "ghost-pill",
                                                            onClick: _cache[14] || (_cache[14] = ($event) => (showManualForm.value = !showManualForm.value))
                                                        }, "Manual")
                                                    ]))
                                            ]),
                                            createVNode(Transition, { name: "fade" }, {
                                                default: withCtx(() => [
                                                    (showManualForm.value)
                                                        ? (openBlock(), createElementBlock("div", _hoisted_39, [
                                                            createBaseVNode("div", _hoisted_40, [
                                                                withDirectives(createBaseVNode("input", {
                                                                    "onUpdate:modelValue": _cache[15] || (_cache[15] = ($event) => ((manualMinutes).value = $event)),
                                                                    type: "number",
                                                                    placeholder: "Min"
                                                                }, null, 512), [
                                                                    [
                                                                        vModelText,
                                                                        manualMinutes.value,
                                                                        void 0,
                                                                        { number: true }
                                                                    ]
                                                                ]),
                                                                withDirectives(createBaseVNode("input", {
                                                                    "onUpdate:modelValue": _cache[16] || (_cache[16] = ($event) => ((manualNote).value = $event)),
                                                                    type: "text",
                                                                    placeholder: "Ghi chú..."
                                                                }, null, 512), [
                                                                    [vModelText, manualNote.value]
                                                                ]),
                                                                createBaseVNode("button", {
                                                                    class: "primary-button primary-button--compact",
                                                                    onClick: submitManualEntry
                                                                }, "Log")
                                                            ])
                                                        ]))
                                                        : createCommentVNode("", true)
                                                ]),
                                                _: 1
                                            }),
                                            (unref(timeEntries).length > 0)
                                                ? (openBlock(), createElementBlock("div", _hoisted_41, [
                                                    (openBlock(true), createElementBlock(Fragment, null, renderList(unref(timeEntries).slice(0, 3), (entry) => {
                                                        return (openBlock(), createElementBlock("div", {
                                                            key: entry.id,
                                                            class: "entry-row"
                                                        }, [
                                                            createBaseVNode("span", null, toDisplayString(entry.userName), 1),
                                                            createBaseVNode("span", _hoisted_42, toDisplayString(entry.manualMinutes || entry.totalMinutes) + "m", 1),
                                                            createBaseVNode("span", _hoisted_43, toDisplayString(unref(formatDate)(entry.startedAt)), 1)
                                                        ]));
                                                    }), 128))
                                                ]))
                                                : createCommentVNode("", true)
                                        ]),
                                        createBaseVNode("div", _hoisted_44, [
                                            createBaseVNode("div", _hoisted_45, [
                                                _cache[31] || (_cache[31] = createBaseVNode("strong", null, "Attachments", -1)),
                                                createBaseVNode("label", _hoisted_46, [
                                                    createBaseVNode("input", {
                                                        type: "file",
                                                        onChange: _cache[17] || (_cache[17] =
                                                            //@ts-ignore
                                                            (...args) => (unref(uploadAttachment) && unref(uploadAttachment)(...args)))
                                                    }, null, 32),
                                                    _cache[30] || (_cache[30] = createBaseVNode("span", null, "+ Add", -1))
                                                ])
                                            ]),
                                            createBaseVNode("div", _hoisted_47, [
                                                (openBlock(true), createElementBlock(Fragment, null, renderList(unref(attachments), (attachment) => {
                                                    return (openBlock(), createElementBlock("article", {
                                                        key: attachment.id,
                                                        class: "file-chip"
                                                    }, [
                                                        createBaseVNode("div", _hoisted_48, [
                                                            createBaseVNode("strong", _hoisted_49, toDisplayString(attachment.fileName), 1),
                                                            createBaseVNode("span", null, toDisplayString(unref(formatFileSize)(attachment.fileSize)), 1)
                                                        ]),
                                                        createBaseVNode("button", {
                                                            class: "close-pill",
                                                            onClick: ($event) => (unref(deleteAttachment)(attachment))
                                                        }, [
                                                            createVNode(unref(X), { size: 12 })
                                                        ], 8, _hoisted_50)
                                                    ]));
                                                }), 128))
                                            ])
                                        ]),
                                        createBaseVNode("div", _hoisted_51, [
                                            _cache[32] || (_cache[32] = createBaseVNode("div", { class: "section-header" }, [
                                                createBaseVNode("strong", null, "Discussion")
                                            ], -1)),
                                            createBaseVNode("div", _hoisted_52, [
                                                (openBlock(true), createElementBlock(Fragment, null, renderList(unref(comments), (comment) => {
                                                    return (openBlock(), createElementBlock("article", {
                                                        key: comment.id,
                                                        class: "comment-bubble"
                                                    }, [
                                                        createBaseVNode("div", _hoisted_53, [
                                                            createBaseVNode("strong", null, toDisplayString(comment.authorName), 1),
                                                            createBaseVNode("span", _hoisted_54, toDisplayString(unref(formatTime)(comment.createdAt)), 1)
                                                        ]),
                                                        createBaseVNode("p", null, toDisplayString(comment.content), 1),
                                                        (unref(isProjectAdmin) || comment.authorId === unref(currentUser)?.id)
                                                            ? (openBlock(), createElementBlock("button", {
                                                                key: 0,
                                                                class: "bubble-delete",
                                                                onClick: ($event) => (unref(deleteComment)(comment.id))
                                                            }, "Delete", 8, _hoisted_55))
                                                            : createCommentVNode("", true)
                                                    ]));
                                                }), 128))
                                            ]),
                                            createBaseVNode("form", {
                                                class: "comment-input-area",
                                                onSubmit: _cache[19] || (_cache[19] = withModifiers(
                                                //@ts-ignore
                                                (...args) => (unref(submitComment) && unref(submitComment)(...args)), ["prevent"]))
                                            }, [
                                                withDirectives(createBaseVNode("input", {
                                                    "onUpdate:modelValue": _cache[18] || (_cache[18] = ($event) => (isRef(newComment) ? (newComment).value = $event : null)),
                                                    type: "text",
                                                    placeholder: "Type a message..."
                                                }, null, 512), [
                                                    [vModelText, unref(newComment)]
                                                ]),
                                                createBaseVNode("button", {
                                                    class: "send-pill",
                                                    type: "submit",
                                                    disabled: !unref(newComment).trim()
                                                }, [
                                                    createVNode(unref(Send), { size: 16 })
                                                ], 8, _hoisted_56)
                                            ], 32)
                                        ])
                                    ]))
                                    : (openBlock(), createElementBlock("div", _hoisted_57, [
                                        createVNode(unref(ClipboardList), { size: 48 }),
                                        _cache[33] || (_cache[33] = createBaseVNode("p", null, "Select a task to see details", -1))
                                    ]))
                            ])
                        ]))
                        : createCommentVNode("", true),
                    (unref(activeProjectTab) === 'members')
                        ? (openBlock(), createElementBlock("div", _hoisted_58, [
                            createVNode(ProjectMembersTab, {
                                members: unref(selectedProjectMembers),
                                users: unref(users),
                                "is-admin": unref(isProjectAdmin),
                                onAdd: unref(addMember),
                                onRemove: unref(removeMember),
                                onUpdateRole: unref(updateMemberRole)
                            }, null, 8, ["members", "users", "is-admin", "onAdd", "onRemove", "onUpdateRole"])
                        ]))
                        : createCommentVNode("", true),
                    (unref(activeProjectTab) === 'wiki')
                        ? (openBlock(), createElementBlock("div", _hoisted_59, [
                            createVNode(ProjectWikiTab, {
                                "project-name": unref(selectedProject)?.name ?? '',
                                "is-admin": unref(isProjectAdmin)
                            }, null, 8, ["project-name", "is-admin"])
                        ]))
                        : createCommentVNode("", true)
                ])
            ]));
        };
    }
});
const ProjectDetailPage = /*#__PURE__*/ _export_sfc(_sfc_main, [['__scopeId', "data-v-acc246dc"]]);
export { ProjectDetailPage as default };
//# sourceMappingURL=ProjectDetailPage.js.map
