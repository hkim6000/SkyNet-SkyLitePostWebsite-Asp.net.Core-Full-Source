var AdminCommentsJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminComments/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('cm-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('cm-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('cm-open');
        }, 3200);
    }

    function openNav() {
        el('cm-nav').classList.add('cm-open');
        el('cm-scrim').classList.add('cm-open');
    }

    function closeNav() {
        el('cm-nav').classList.remove('cm-open');
        el('cm-scrim').classList.remove('cm-open');
    }

    function ago(d) {
        var s = (Date.now() - d.getTime()) / 1000;
        if (s < 60) {
            return 'Just now';
        }
        if (s < 3600) {
            var m = Math.floor(s / 60);
            return m + (m === 1 ? ' min ago' : ' mins ago');
        }
        if (s < 86400) {
            var h = Math.floor(s / 3600);
            return h + (h === 1 ? ' hour ago' : ' hours ago');
        }
        return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    }

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'hm') {
                t.textContent = d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('sp_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('sp_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        setCookie('sp_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('cm-modal').classList.add('cm-open');
    }

    function closeModal() {
        var m = el('cm-modal');
        if (m) {
            m.classList.remove('cm-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    var range = 'all';
    var tabv = 'pending';
    var page = 1;
    var stimer = null;

    function load() {
        api('Load', []);
    }

    function loaded() {
        list(1);
    }

    function pad(n) {
        return (n < 10 ? '0' : '') + n;
    }

    function ymd(d) {
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
    }

    function chips() {
        var b = document.querySelectorAll('.cm-range .cm-chip');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('cm-act', b[i].getAttribute('data-r') === range);
        }
    }

    function setRange(r) {
        range = r;
        var t = new Date();
        var from = new Date(t.getFullYear(), t.getMonth(), t.getDate());
        if (r === '7') {
            from.setDate(from.getDate() - 6);
        } else if (r === '30') {
            from.setDate(from.getDate() - 29);
        }
        el('cm-from').value = r === 'all' ? '' : ymd(from);
        el('cm-to').value = r === 'all' ? '' : ymd(t);
        chips();
        list(1);
    }

    function custom() {
        range = 'custom';
        chips();
        list(1);
    }

    function isoDay(v, end) {
        if (!v) {
            return '';
        }
        var p = v.split('-');
        var d = new Date(parseInt(p[0], 10), parseInt(p[1], 10) - 1, parseInt(p[2], 10) + (end ? 1 : 0));
        return isNaN(d.getTime()) ? '' : d.toISOString();
    }

    function tab(t) {
        tabv = t;
        list(1);
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 300);
    }

    function list(pg) {
        page = pg || 1;
        api('Filter', [
            { key: 'tab', vlu: tabv },
            { key: 'cq', vlu: val('cm-cq') },
            { key: 'csec', vlu: val('cm-csec') },
            { key: 'dfrom', vlu: isoDay(val('cm-from'), false) },
            { key: 'dto', vlu: isoDay(val('cm-to'), true) },
            { key: 'pg', vlu: String(page) }
        ]);
    }

    function listed() {
        times(el('cm-clist'));
    }

    function act(id, a) {
        api('Moderate', [{ key: 'cid', vlu: id }, { key: 'act', vlu: a }]);
    }

    function moderated() {
        list(page);
    }

    function reveal() {
        if (!getCookie('sp_tok')) {
            location.replace('AdminLogin');
            return;
        }
        load();
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                closeModal();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('cm-', 'cm-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: closeModal,
        loaded: loaded,
        setRange: setRange,
        custom: custom,
        tab: tab,
        search: search,
        list: list,
        listed: listed,
        act: act,
        moderated: moderated
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminCommentsJs.reveal);
} else {
    AdminCommentsJs.reveal();
}
