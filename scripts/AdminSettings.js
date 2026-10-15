var AdminSettingsJs = (function () {

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
        $ApiRequest('AdminSettings/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('st-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('st-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('st-open');
        }, 3200);
    }

    function openNav() {
        el('st-nav').classList.add('st-open');
        el('st-scrim').classList.add('st-open');
    }

    function closeNav() {
        el('st-nav').classList.remove('st-open');
        el('st-scrim').classList.remove('st-open');
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
        el('st-modal').classList.add('st-open');
    }

    function closeModal() {
        var m = el('st-modal');
        if (m) {
            m.classList.remove('st-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    var NETS = ["instagram", "youtube", "x", "facebook", "linkedin", "tiktok", "pinterest", "threads", "behance", "flickr", "vimeo"];

    function load() {
        api('Load', []);
    }

    function loaded() {
    }

    function save() {
        var list = [
            { key: 'sname', vlu: val('st-sname') },
            { key: 'stag', vlu: val('st-stag') },
            { key: 'saccent', vlu: val('st-saccent') },
            { key: 'stheme', vlu: val('st-stheme') },
            { key: 'semail', vlu: val('st-semail') },
            { key: 'sphone', vlu: val('st-sphone') },
            { key: 'saddr', vlu: val('st-saddr') },
            { key: 'sabout', vlu: val('st-sabout') },
            { key: 'scomments', vlu: el('st-scomments').checked ? '1' : '' }
        ];
        var soc = [];
        for (var i = 0; i < NETS.length; i++) {
            soc.push({ key: NETS[i], url: val('st-so-' + NETS[i]), show: el('st-sh-' + NETS[i]).checked });
        }
        list.push({ key: 'ssocial', vlu: JSON.stringify(soc) });
        api('Save', list);
        return false;
    }

    function color(v) {
        el('st-saccent').value = v;
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
            var id = e.target && e.target.id ? e.target.id.replace('st-', 'st-e-') : '';
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
        save: save,
        color: color
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminSettingsJs.reveal);
} else {
    AdminSettingsJs.reveal();
}
