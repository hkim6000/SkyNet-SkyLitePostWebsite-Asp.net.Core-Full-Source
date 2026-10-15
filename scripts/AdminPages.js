var AdminPagesJs = (function () {

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
        $ApiRequest('AdminPages/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('pg-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('pg-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pg-open');
        }, 3200);
    }

    function openNav() {
        el('pg-nav').classList.add('pg-open');
        el('pg-scrim').classList.add('pg-open');
    }

    function closeNav() {
        el('pg-nav').classList.remove('pg-open');
        el('pg-scrim').classList.remove('pg-open');
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
        el('pg-modal').classList.add('pg-open');
    }

    function closeModal() {
        var m = el('pg-modal');
        if (m) {
            m.classList.remove('pg-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('pg-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('pg-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        if (askCheck) {
            var m = askCheck(x.i.value.trim());
            if (m) {
                x.e.textContent = m;
                x.i.focus();
                return;
            }
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'pg-ask';
        askBox.innerHTML = '<div class="pg-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="pg-err" role="alert"></em><div class="pg-askacts"><button type="button" class="pg-btn pg-btn-ghost pg-btn-sm"></button><button type="button" class="pg-btn pg-btn-sm"></button></div></div>';
        (el('pg-site') || document.body).appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () {
            askClose(false);
        });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () {
            x.e.textContent = '';
        });
        askBox.addEventListener('mousedown', function (e) {
            if (e.target === askBox) {
                askClose(false);
            }
        });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('pg-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) {
                    return n.offsetParent !== null;
                });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts();
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'pg-btn pg-btn-sm' + (o.danger ? ' pg-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('pg-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('pg-open');
        if (input) {
            x.i.focus();
            x.i.setSelectionRange(x.i.value.length, x.i.value.length);
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function leave(go) {
        ask({ title: 'Leave without saving?', text: 'Your changes since the last save will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, go);
    }

    var dirty = false;

    function load() {
        api('Load', [{ key: 'pk', vlu: qs('p') }]);
    }

    function loaded() {
        dirty = false;
        mark(false);
    }

    function mark(on) {
        dirty = on !== false;
        var d = el('pg-dirty');
        if (d) {
            d.textContent = dirty ? 'Unsaved changes' : '';
        }
    }

    function go(key) {
        if (dirty) {
            leave(function () {
                dirty = false;
                go(key);
            });
            return;
        }
        history.replaceState(null, '', 'AdminPages?p=' + encodeURIComponent(key));
        dirty = false;
        api('Load', [{ key: 'pk', vlu: key }]);
    }

    function pickStyle() {
        openModal();
    }

    function add(style) {
        closeModal();
        api('Card', [{ key: 'pk', vlu: val('pg-pk') }, { key: 'style', vlu: style }]);
    }

    function added(id) {
        mark();
        var b = document.querySelector('[data-id="' + id + '"]');
        if (b) {
            b.scrollIntoView({ behavior: 'smooth', block: 'center' });
            var t = b.querySelector('input,select');
            if (t) {
                t.focus({ preventScroll: true });
            }
        }
    }

    function move(btn, dir) {
        var b = btn.closest('.pg-blk');
        var c = el('pg-canvas');
        if (dir < 0 && b.previousElementSibling) {
            c.insertBefore(b, b.previousElementSibling);
        } else if (dir > 0 && b.nextElementSibling) {
            c.insertBefore(b.nextElementSibling, b);
        }
        mark();
        btn.focus();
    }

    function remove(btn) {
        var b = btn.closest('.pg-blk');
        b.parentNode.removeChild(b);
        mark();
    }

    function drag(e, btn) {
        var card = btn.closest('.pg-blk');
        var canvas = el('pg-canvas');
        e.preventDefault();
        card.classList.add('pg-dragging');
        function mv(ev) {
            var y = ev.clientY;
            if (y < 90) {
                window.scrollBy(0, -12);
            } else if (y > window.innerHeight - 50) {
                window.scrollBy(0, 12);
            }
            var others = Array.prototype.filter.call(canvas.children, function (x) {
                return x !== card;
            });
            var target = null;
            for (var i = 0; i < others.length; i++) {
                var r = others[i].getBoundingClientRect();
                if (y < r.top + r.height / 2) {
                    target = others[i];
                    break;
                }
            }
            if (target) {
                if (card.nextElementSibling !== target) {
                    canvas.insertBefore(card, target);
                }
            } else if (canvas.lastElementChild !== card) {
                canvas.appendChild(card);
            }
        }
        function up() {
            card.classList.remove('pg-dragging');
            document.removeEventListener('pointermove', mv);
            document.removeEventListener('pointerup', up);
            mark();
        }
        document.addEventListener('pointermove', mv);
        document.addEventListener('pointerup', up);
    }

    function src(sel) {
        var b = sel.closest('.pg-blk');
        mark();
        api('Pins', [{ key: 'mod', vlu: b.getAttribute('data-id') }, { key: 'source', vlu: sel.value }]);
    }

    function collect() {
        var out = [];
        var list = el('pg-canvas').querySelectorAll('.pg-blk');
        for (var i = 0; i < list.length; i++) {
            var b = list[i];
            var q = function (c) {
                var x = b.querySelector('.pg-f-' + c + ' input, .pg-f-' + c + ' select, .pg-f-' + c + ' textarea');
                return x ? x.value : '';
            };
            out.push({ id: b.getAttribute('data-id'), style: b.getAttribute('data-style'), title: q('title'), source: q('src') || 'all', count: parseInt(q('count') || '0', 10), pin: q('pin'), text: q('text') });
        }
        return out;
    }

    function save() {
        api('Save', [{ key: 'pk', vlu: val('pg-pk') }, { key: 'mods', vlu: JSON.stringify(collect()) }]);
    }

    function restore() {
        ask({ title: 'Restore the previous version?', text: 'The page goes back to how it was before your last save.', ok: 'Restore' }, function () {
            api('Restore', [{ key: 'pk', vlu: val('pg-pk') }]);
        });
    }

    document.addEventListener('input', function (e) {
        if (e.target.closest && e.target.closest('#pg-canvas')) {
            mark();
        }
    });

    window.addEventListener('beforeunload', function (e) {
        if (dirty) {
            e.preventDefault();
            e.returnValue = '';
        }
    });

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
            var id = e.target && e.target.id ? e.target.id.replace('pg-', 'pg-e-') : '';
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
        go: go,
        pickStyle: pickStyle,
        add: add,
        added: added,
        move: move,
        remove: remove,
        drag: drag,
        src: src,
        save: save,
        restore: restore
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminPagesJs.reveal);
} else {
    AdminPagesJs.reveal();
}
