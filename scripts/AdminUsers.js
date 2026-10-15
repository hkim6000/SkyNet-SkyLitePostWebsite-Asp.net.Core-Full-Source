var AdminUsersJs = (function () {

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
        $ApiRequest('AdminUsers/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('us-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('us-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('us-open');
        }, 3200);
    }

    function openNav() {
        el('us-nav').classList.add('us-open');
        el('us-scrim').classList.add('us-open');
    }

    function closeNav() {
        el('us-nav').classList.remove('us-open');
        el('us-scrim').classList.remove('us-open');
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
        el('us-modal').classList.add('us-open');
    }

    function closeModal() {
        var m = el('us-modal');
        if (m) {
            m.classList.remove('us-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    var page = 1;
    var stimer = null;

    function load() {
        api('Load', []);
    }

    function loaded() {
    }

    function list(pg) {
        page = pg || 1;
        api('List', [{ key: 'uq', vlu: val('us-uq') }, { key: 'ud', vlu: val('us-ud') }, { key: 'pg', vlu: String(page) }]);
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 300);
    }

    function edit(id) {
        api('Form', [{ key: 'uid', vlu: id }]);
    }

    function formed() {
        openModal();
        admin();
        var f = el('us-u-name');
        if (f) {
            f.focus();
        }
    }

    function admin() {
        var a = el('us-u-admin');
        var g = el('us-gridwrap');
        if (a && g) {
            g.classList.toggle('us-dim', a.checked);
        }
    }

    function tick(box) {
        var sec = box.getAttribute('data-sec');
        var l = box.getAttribute('data-l');
        var row = document.querySelectorAll('input[data-sec="' + sec + '"]');
        if (l === 'P' && box.checked) {
            for (var i = 0; i < row.length; i++) {
                row[i].checked = true;
            }
        }
        if (l !== 'V' && box.checked) {
            document.querySelector('input[data-sec="' + sec + '"][data-l="V"]').checked = true;
        }
        if (l === 'V' && !box.checked) {
            for (var j = 0; j < row.length; j++) {
                row[j].checked = false;
            }
        }
        if (l !== 'P' && !box.checked) {
            document.querySelector('input[data-sec="' + sec + '"][data-l="P"]').checked = false;
        }
    }

    function desk(box) {
        var list = document.querySelectorAll('input[data-desk="' + box.getAttribute('data-for') + '"]');
        for (var i = 0; i < list.length; i++) {
            list[i].checked = box.checked;
        }
    }

    function perms() {
        var out = {};
        var list = document.querySelectorAll('#us-gridwrap input[data-sec]');
        for (var i = 0; i < list.length; i++) {
            if (list[i].checked) {
                var s = list[i].getAttribute('data-sec');
                out[s] = (out[s] || '') + list[i].getAttribute('data-l');
            }
        }
        return JSON.stringify(out);
    }

    function save() {
        api('SaveUser', [
            { key: 'uid', vlu: val('us-u-id') },
            { key: 'uname', vlu: val('us-u-name') },
            { key: 'ulogin', vlu: val('us-u-login') },
            { key: 'utitle', vlu: val('us-u-title') },
            { key: 'udept', vlu: val('us-u-dept') },
            { key: 'uemail', vlu: val('us-u-email') },
            { key: 'ubio', vlu: val('us-u-bio') },
            { key: 'upass', vlu: el('us-u-pass').value },
            { key: 'uadmin', vlu: el('us-u-admin').checked ? '1' : '' },
            { key: 'ufront', vlu: el('us-u-front').checked ? '1' : '' },
            { key: 'uactive', vlu: el('us-u-active').checked ? '1' : '' },
            { key: 'uperms', vlu: perms() }
        ]);
        return false;
    }

    function saved() {
        closeModal();
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
            var id = e.target && e.target.id ? e.target.id.replace('us-', 'us-e-') : '';
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
        list: list,
        search: search,
        edit: edit,
        formed: formed,
        admin: admin,
        tick: tick,
        desk: desk,
        save: save,
        saved: saved
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminUsersJs.reveal);
} else {
    AdminUsersJs.reveal();
}
