var HomeJs = (function () {

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
        $ApiRequest('Home/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('hm-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('hm-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('hm-open');
        }, 3200);
    }

    function openNav() {
        el('hm-nav').classList.add('hm-open');
        el('hm-scrim').classList.add('hm-open');
    }

    function closeNav() {
        el('hm-nav').classList.remove('hm-open');
        el('hm-scrim').classList.remove('hm-open');
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

    var ARGA = '';
    var ARGB = '';

    function arg(k) {
        return k ? (new URLSearchParams(location.search).get(k) || '') : '';
    }

    function applyTheme(theme) {
        el('hm-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('hm-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('sp-theme', next);
        } catch (e) {
        }
    }

    function demoBar() {
        el('hm-demo-bar').innerHTML = 'You are viewing your own demo copy of the Post. It resets after an hour. <a href="Admin">Open the newsroom</a>';
    }

    function hydrated(ok, accent, theme) {
        document.documentElement.classList.remove('hm-hydrating');
        if (!ok) {
            setCookie('sp_sbx', '', 0);
            return;
        }
        if (accent) {
            el('hm-site').style.setProperty('--hm-accent', accent);
        }
        var stored = '';
        try {
            stored = localStorage.getItem('sp-theme') || '';
        } catch (e) {
        }
        applyTheme(stored || theme);
        demoBar();
        times();
    }

    function sandboxed(id) {
        setCookie('sp_sbx', id, 3600);
        demoBar();
    }

    function today() {
        var t = el('hm-today');
        if (t) {
            t.textContent = new Date().toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
        }
    }

    function subscribe(id) {
        call('Subscribe', [
            { key: 'mod', vlu: id },
            { key: 'mail', vlu: val('hm-nl-' + id) },
            { key: 'sbx', vlu: getCookie('sp_sbx') }
        ]);
    }

    function subscribed(id) {
        var i = el('hm-nl-' + id);
        if (i) {
            i.value = '';
        }
        toast('Thanks – you are on the list');
    }

    function comment() {
        call('Comment', [
            { key: 'story', vlu: el('hm-cform').getAttribute('data-story') },
            { key: 'cname', vlu: val('hm-cname') },
            { key: 'ctext', vlu: val('hm-ctext') },
            { key: 'sbx', vlu: getCookie('sp_sbx') }
        ]);
        return false;
    }

    function commented() {
        el('hm-ctext').value = '';
    }

    function more(offset) {
        call('More', [
            { key: 'argA', vlu: arg(ARGA) },
            { key: 'argB', vlu: arg(ARGB) },
            { key: 'from', vlu: String(offset) },
            { key: 'sbx', vlu: getCookie('sp_sbx') }
        ]);
    }

    function appended() {
        times(el('hm-list'));
    }

    function playInline(b) {
        var box = b.parentNode;
        var img = box.querySelector('img');
        var v = document.createElement('video');
        v.className = 'hm-inv';
        v.controls = true;
        v.playsInline = true;
        v.preload = 'auto';
        if (img) {
            v.poster = img.getAttribute('src');
            v.style.height = img.offsetHeight + 'px';
        }
        var src = document.createElement('source');
        src.src = b.getAttribute('data-video');
        src.type = b.getAttribute('data-type');
        v.appendChild(src);
        var old = img ? (img.parentNode.tagName === 'A' && img.parentNode.parentNode === box ? img.parentNode : img) : null;
        if (old) {
            box.replaceChild(v, old);
        } else {
            box.insertBefore(v, box.firstChild);
        }
        box.removeChild(b);
        box.classList.add('hm-playing');
        var p = v.play();
        if (p && p.catch) {
            p.catch(function () {
            });
        }
    }

    function reveal() {
        var stored = '';
        try {
            stored = localStorage.getItem('sp-theme') || '';
        } catch (e) {
        }
        if (stored) {
            applyTheme(stored);
        }
        today();
        times();
        if (getCookie('sp_sbx')) {
            call('Hydrate', [{ key: 'sbx', vlu: getCookie('sp_sbx') }, { key: 'argA', vlu: arg(ARGA) }, { key: 'argB', vlu: arg(ARGB) }]);
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        document.addEventListener('play', function (e) {
            var list = document.querySelectorAll('video');
            for (var i = 0; i < list.length; i++) {
                if (list[i] !== e.target) {
                    list[i].pause();
                }
            }
        }, true);
        document.addEventListener('click', function (e) {
            var pb = e.target.closest ? e.target.closest('.hm-playb') : null;
            if (pb) {
                e.preventDefault();
                playInline(pb);
                return;
            }
            var a = e.target.closest ? e.target.closest('a[data-demo]') : null;
            if (a) {
                e.preventDefault();
                toast('Demo link – the social accounts are placeholders');
            }
        });
        document.addEventListener('submit', function (e) {
            var f = e.target;
            if (f && f.getAttribute && f.getAttribute('data-nl')) {
                e.preventDefault();
                subscribe(f.getAttribute('data-nl'));
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('hm-', 'hm-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
        setInterval(function () {
            times();
        }, 60000);
    }

    if (document.cookie.indexOf('sp_sbx=') >= 0) {
        document.documentElement.classList.add('hm-hydrating');
        setTimeout(function () {
            document.documentElement.classList.remove('hm-hydrating');
        }, 2500);
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        hydrated: hydrated,
        sandboxed: sandboxed,
        subscribed: subscribed,
        comment: comment,
        commented: commented,
        more: more,
        appended: appended
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', HomeJs.reveal);
} else {
    HomeJs.reveal();
}
