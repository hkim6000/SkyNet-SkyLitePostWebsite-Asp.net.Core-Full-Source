var AdminDesksJs = (function () {

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
        $ApiRequest('AdminDesks/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('dp-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('dp-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('dp-open');
        }, 3200);
    }

    function openNav() {
        el('dp-nav').classList.add('dp-open');
        el('dp-scrim').classList.add('dp-open');
    }

    function closeNav() {
        el('dp-nav').classList.remove('dp-open');
        el('dp-scrim').classList.remove('dp-open');
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
        el('dp-modal').classList.add('dp-open');
    }

    function closeModal() {
        var m = el('dp-modal');
        if (m) {
            m.classList.remove('dp-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    var dirty = false;
    var seq = 0;
    var ICONS = {};

    function load() {
        api('Load', []);
    }

    function loaded() {
        dirty = false;
        mark(false);
        var t = el('dp-icons');
        if (t) {
            ICONS = JSON.parse(t.textContent);
        }
    }

    function mark(on) {
        dirty = on !== false;
        var d = el('dp-dirty');
        if (d) {
            d.textContent = dirty ? 'Unsaved changes' : '';
        }
    }

    function move(btn, dir) {
        var b = btn.closest(btn.closest('.dp-sr2') ? '.dp-sr2' : '.dp-dk');
        var c = b.parentNode;
        if (dir < 0 && b.previousElementSibling) {
            c.insertBefore(b, b.previousElementSibling);
        } else if (dir > 0 && b.nextElementSibling) {
            c.insertBefore(b.nextElementSibling, b);
        }
        mark();
        btn.focus();
    }

    function remove(btn) {
        var b = btn.closest(btn.closest('.dp-sr2') ? '.dp-sr2' : '.dp-dk');
        b.parentNode.removeChild(b);
        mark();
    }

    function drag(e, btn) {
        var item = btn.closest(btn.closest('.dp-sr2') ? '.dp-sr2' : '.dp-dk');
        var box = item.parentNode;
        e.preventDefault();
        item.classList.add('dp-dragging');
        function mv(ev) {
            var y = ev.clientY;
            if (y < 90) {
                window.scrollBy(0, -12);
            } else if (y > window.innerHeight - 50) {
                window.scrollBy(0, 12);
            }
            var others = Array.prototype.filter.call(box.children, function (x) {
                return x !== item;
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
                if (item.nextElementSibling !== target) {
                    box.insertBefore(item, target);
                }
            } else if (box.lastElementChild !== item) {
                box.appendChild(item);
            }
        }
        function up() {
            item.classList.remove('dp-dragging');
            document.removeEventListener('pointermove', mv);
            document.removeEventListener('pointerup', up);
            mark();
        }
        document.addEventListener('pointermove', mv);
        document.addEventListener('pointerup', up);
    }

    function secRow() {
        seq++;
        return '<div class="dp-sr2" data-key=""><button type="button" class="dp-ib dp-grip" aria-label="Drag to reorder" onpointerdown="AdminDesksJs.drag(event,this)">' + ICONS.drag + '</button>'
            + '<input type="text" maxlength="30" placeholder="Section name" aria-label="Section name" id="dp-ns-' + seq + '">'
            + '<span class="dp-key">new</span><span class="dp-cnt2">0 stories</span>'
            + '<button type="button" class="dp-ib dp-up2" aria-label="Move up" onclick="AdminDesksJs.move(this,-1)">' + ICONS.up + '</button>'
            + '<button type="button" class="dp-ib dp-del" aria-label="Delete section" onclick="AdminDesksJs.remove(this)">' + ICONS.trash + '</button></div>';
    }

    function addSection(btn) {
        var box = btn.closest('.dp-dk').querySelector('.dp-secs');
        box.insertAdjacentHTML('beforeend', secRow());
        box.lastElementChild.querySelector('input').focus();
        mark();
    }

    function addDesk() {
        seq++;
        var html = '<div class="dp-dk" data-key=""><div class="dp-dtool"><button type="button" class="dp-ib dp-grip" aria-label="Drag to reorder" onpointerdown="AdminDesksJs.drag(event,this)">' + ICONS.drag + '</button><b>New department</b><span class="dp-cnt2">0 stories</span>'
            + '<button type="button" class="dp-ib" aria-label="Move up" onclick="AdminDesksJs.move(this,-1)">' + ICONS.up + '</button><button type="button" class="dp-ib" aria-label="Move down" onclick="AdminDesksJs.move(this,1)">' + ICONS.down + '</button>'
            + '<button type="button" class="dp-ib dp-del" aria-label="Delete department" onclick="AdminDesksJs.remove(this)">' + ICONS.trash + '</button></div>'
            + '<div class="dp-dbody"><div class="dp-g2"><label class="dp-field" for="dp-nd-' + seq + '"><span>Name</span><input type="text" class="dp-dname" id="dp-nd-' + seq + '" maxlength="30"></label>'
            + '<label class="dp-field" for="dp-nb-' + seq + '"><span>Short description (shown on the department page)</span><input type="text" class="dp-dblurb" id="dp-nb-' + seq + '" maxlength="140"></label></div>'
            + '<div class="dp-secs">' + secRow() + '</div><button type="button" class="dp-addsec" onclick="AdminDesksJs.addSection(this)">+ Add section</button></div></div>';
        el('dp-desks').insertAdjacentHTML('beforeend', html);
        var d = el('dp-desks').lastElementChild;
        d.scrollIntoView({ behavior: 'smooth', block: 'center' });
        d.querySelector('.dp-dname').focus({ preventScroll: true });
        mark();
    }

    function collect() {
        var out = [];
        var desks = el('dp-desks').querySelectorAll('.dp-dk');
        for (var i = 0; i < desks.length; i++) {
            var d = desks[i];
            var secs = [];
            var rows = d.querySelectorAll('.dp-sr2');
            for (var j = 0; j < rows.length; j++) {
                secs.push({ key: rows[j].getAttribute('data-key'), name: rows[j].querySelector('input').value.trim() });
            }
            out.push({ key: d.getAttribute('data-key'), name: d.querySelector('.dp-dname').value.trim(), blurb: d.querySelector('.dp-dblurb').value.trim(), sections: secs });
        }
        return out;
    }

    function save() {
        var bad = document.querySelectorAll('#dp-desks input.dp-bad');
        for (var i = 0; i < bad.length; i++) {
            bad[i].classList.remove('dp-bad');
        }
        api('Save', [{ key: 'desks', vlu: JSON.stringify(collect()) }]);
    }

    function flag(i, j) {
        var d = el('dp-desks').querySelectorAll('.dp-dk')[i];
        if (!d) {
            return;
        }
        var x = j < 0 ? d.querySelector('.dp-dname') : d.querySelectorAll('.dp-sr2 input')[j];
        if (x) {
            x.classList.add('dp-bad');
            x.focus();
        }
    }

    document.addEventListener('input', function (e) {
        if (e.target.closest && e.target.closest('#dp-desks')) {
            mark();
            e.target.classList.remove('dp-bad');
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
            var id = e.target && e.target.id ? e.target.id.replace('dp-', 'dp-e-') : '';
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
        move: move,
        remove: remove,
        drag: drag,
        addSection: addSection,
        addDesk: addDesk,
        save: save,
        flag: flag
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminDesksJs.reveal);
} else {
    AdminDesksJs.reveal();
}
