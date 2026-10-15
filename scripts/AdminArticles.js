var AdminArticlesJs = (function () {

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
        $ApiRequest('AdminArticles/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ed-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ed-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ed-open');
        }, 3200);
    }

    function openNav() {
        el('ed-nav').classList.add('ed-open');
        el('ed-scrim').classList.add('ed-open');
    }

    function closeNav() {
        el('ed-nav').classList.remove('ed-open');
        el('ed-scrim').classList.remove('ed-open');
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
        el('ed-modal').classList.add('ed-open');
    }

    function closeModal() {
        var m = el('ed-modal');
        if (m) {
            m.classList.remove('ed-open');
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
        if (!askBox || !askBox.classList.contains('ed-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ed-open');
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
        askBox.className = 'ed-ask';
        askBox.innerHTML = '<div class="ed-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ed-err" role="alert"></em><div class="ed-askacts"><button type="button" class="ed-btn ed-btn-ghost ed-btn-sm"></button><button type="button" class="ed-btn ed-btn-sm"></button></div></div>';
        (el('ed-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('ed-open')) {
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
        x.b[1].className = 'ed-btn ed-btn-sm' + (o.danger ? ' ed-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ed-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ed-open');
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
    var stimer = null;
    var pickMode = 'cover';

    function state() {
        try {
            return JSON.parse(sessionStorage.getItem('sp-stories') || '{}') || {};
        } catch (e) {
            return {};
        }
    }

    function remember(pg) {
        try {
            sessionStorage.setItem('sp-stories', JSON.stringify({ f: val('ed-fv'), sec: val('ed-secf'), q: val('ed-q'), pg: pg || 1, sk: val('ed-sk'), sd: val('ed-sd'), fr: val('ed-frf') }));
        } catch (e) {
        }
    }

    function listArgs() {
        var st = state();
        return [{ key: 'filter', vlu: st.f || '' }, { key: 'secf', vlu: st.sec || '' }, { key: 'term', vlu: st.q || '' }, { key: 'pg', vlu: String(st.pg || 1) }, { key: 'sk', vlu: st.sk || '' }, { key: 'sd', vlu: st.sd || '' }, { key: 'frontf', vlu: st.fr || '' }];
    }

    function load() {
        if (qs('f')) {
            try {
                sessionStorage.setItem('sp-stories', JSON.stringify({ f: qs('f') }));
            } catch (e) {
            }
        }
        api('Load', [{ key: 'open', vlu: qs('id') }, { key: 'fresh', vlu: qs('new') }].concat(listArgs()));
    }

    function loaded() {
        times(el('ed-main'));
        stamps();
        dirty = false;
        var b = el('ed-body');
        if (b) {
            b.addEventListener('input', function () {
                dirty = true;
            });
        }
    }

    function list(pg) {
        pg = pg || 1;
        remember(pg);
        api('List', [{ key: 'filter', vlu: val('ed-fv') }, { key: 'secf', vlu: val('ed-secf') }, { key: 'term', vlu: val('ed-q') }, { key: 'pg', vlu: String(pg) }, { key: 'sk', vlu: val('ed-sk') }, { key: 'sd', vlu: val('ed-sd') }, { key: 'frontf', vlu: val('ed-frf') }]);
    }

    function sort(k) {
        var cur = val('ed-sk');
        var dir = k === 'updated' || k === 'published' ? 'desc' : 'asc';
        if (k === cur) {
            dir = val('ed-sd') === 'asc' ? 'desc' : 'asc';
        }
        el('ed-sk').value = k;
        el('ed-sd').value = k ? dir : '';
        list(1);
    }

    function sortSel(v) {
        var p = v.split(':');
        el('ed-sk').value = p[0] || '';
        el('ed-sd').value = p[1] || '';
        list(1);
    }

    var ptimer = null;

    function pickSearch() {
        clearTimeout(ptimer);
        ptimer = setTimeout(function () {
            pickPage(1);
        }, 300);
    }

    function pickPage(pg) {
        api('PickList', [{ key: 'pq', vlu: val('ed-pq') }, { key: 'pkind', vlu: val('ed-pkind') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function filter(f) {
        el('ed-fv').value = f;
        list(1);
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 300);
    }

    function stamps() {
        var list = document.querySelectorAll('#ed-list time[data-t="pt"]');
        for (var i = 0; i < list.length; i++) {
            var d = new Date(list[i].getAttribute('datetime'));
            if (!isNaN(d.getTime())) {
                list[i].textContent = d.toLocaleString(undefined, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
            }
        }
    }

    function listed() {
        times(el('ed-list'));
        stamps();
    }

    function edit(id) {
        if (dirty) {
            leave(function () {
                dirty = false;
                edit(id);
            });
            return;
        }
        api('Edit', [{ key: 'sid', vlu: id }]);
    }

    function opened(id) {
        history.replaceState(null, '', id ? 'AdminArticles?id=' + encodeURIComponent(id) : 'AdminArticles?new=1');
        window.scrollTo(0, 0);
        loaded();
    }

    function backToList() {
        if (dirty) {
            leave(function () {
                dirty = false;
                backToList();
            });
            return;
        }
        history.replaceState(null, '', 'AdminArticles');
        api('Load', listArgs());
    }

    function cmd(c, v) {
        el('ed-body').focus();
        document.execCommand(c, false, v || null);
        dirty = true;
    }

    function block(tag) {
        cmd('formatBlock', '<' + tag + '>');
    }

    function link() {
        var s = window.getSelection();
        var b = el('ed-body');
        var r = s && s.rangeCount && b.contains(s.getRangeAt(0).commonAncestorContainer) ? s.getRangeAt(0).cloneRange() : null;
        ask({ title: 'Add a link', text: r && !r.collapsed ? 'The selected words will link to this address.' : 'The address will be added as a link where the cursor is.', value: 'https://', place: 'https://', ok: 'Add link', check: function (u) {
            return /^https?:\/\/[^\s.]+\.\S+$/.test(u) ? '' : 'Enter a full web address starting with https://';
        } }, function (u) {
            b.focus();
            if (r) {
                var w = window.getSelection();
                w.removeAllRanges();
                w.addRange(r);
            }
            if (!r || r.collapsed) {
                document.execCommand('insertHTML', false, '<a href="' + u.replace(/"/g, '%22') + '">' + u.replace(/[<>&"]/g, '') + '</a>');
                dirty = true;
            } else {
                cmd('createLink', u);
            }
        });
    }

    function chooser(kind) {
        api('ChooseMedia', [{ key: 'pkind', vlu: kind }]);
    }

    function pickCover() {
        pickMode = 'cover';
        chooser('photo');
    }

    function pickInline() {
        pickMode = 'inline';
        var s = window.getSelection();
        window.__range = s && s.rangeCount ? s.getRangeAt(0) : null;
        chooser('photo');
    }

    function pickVideo() {
        pickMode = 'video';
        chooser('video');
    }

    function pickVid(src, poster, title) {
        closeModal();
        dirty = true;
        el('ed-video').value = src;
        el('ed-vcover').innerHTML = '<img src="' + poster + '" alt=""><span class="ed-play"></span>';
        if (!val('ed-vcaption')) {
            el('ed-vcaption').value = title;
        }
    }

    function noVideo() {
        el('ed-video').value = '';
        el('ed-vcover').innerHTML = 'No video';
        dirty = true;
    }

    function pick(src, title, thumb) {
        closeModal();
        dirty = true;
        if (pickMode === 'cover') {
            el('ed-image').value = src;
            el('ed-cover').innerHTML = '<img src="' + (thumb || src) + '" alt="">';
            if (!val('ed-caption')) {
                el('ed-caption').value = title;
            }
            return;
        }
        var b = el('ed-body');
        b.focus();
        if (window.__range) {
            var s = window.getSelection();
            s.removeAllRanges();
            s.addRange(window.__range);
        }
        document.execCommand('insertHTML', false, '<figure><img src="' + src + '" alt=""><figcaption>' + title.replace(/[<>&"]/g, '') + '</figcaption></figure><p><br></p>');
    }

    function noCover() {
        el('ed-image').value = '';
        el('ed-cover').innerHTML = 'No photo';
        dirty = true;
    }

    function status() {
        var s = val('ed-status');
        var w = el('ed-whenbox');
        if (w) {
            w.classList.toggle('ed-show', s === 'scheduled');
        }
    }

    function localToIso(v) {
        if (!v) {
            return '';
        }
        var d = new Date(v);
        return isNaN(d.getTime()) ? '' : d.toISOString();
    }

    function save(st) {
        if (st) {
            el('ed-status').value = st;
        }
        api('Save', [
            { key: 'sid', vlu: val('ed-sid') },
            { key: 'title', vlu: val('ed-title') },
            { key: 'dek', vlu: val('ed-dek') },
            { key: 'body', vlu: el('ed-body').innerHTML },
            { key: 'section', vlu: val('ed-section') },
            { key: 'image', vlu: val('ed-image') },
            { key: 'caption', vlu: val('ed-caption') },
            { key: 'video', vlu: val('ed-video') },
            { key: 'vcaption', vlu: val('ed-vcaption') },
            { key: 'tags', vlu: val('ed-tags') },
            { key: 'status', vlu: val('ed-status') },
            { key: 'when', vlu: localToIso(val('ed-when')) },
            { key: 'breaking', vlu: el('ed-breaking') && el('ed-breaking').checked ? '1' : '' },
            { key: 'front', vlu: el('ed-front') && el('ed-front').checked ? '1' : '' },
            { key: 'comments', vlu: el('ed-comments').checked ? '1' : '' }
        ]);
    }

    function setWhen() {
        var w = el('ed-when');
        if (w && w.getAttribute('data-iso')) {
            var d = new Date(w.getAttribute('data-iso'));
            if (!isNaN(d.getTime())) {
                var p = function (n) {
                    return (n < 10 ? '0' : '') + n;
                };
                w.value = d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + 'T' + p(d.getHours()) + ':' + p(d.getMinutes());
            }
        }
    }

    function saved() {
        dirty = false;
    }

    function del() {
        ask({ title: 'Delete this story?', text: '\u201c' + val('ed-title') + '\u201d and its reader comments will be removed. This cannot be undone.', ok: 'Delete story', danger: true }, function () {
            api('Delete', [{ key: 'sid', vlu: val('ed-sid') }]);
        });
    }

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
            var id = e.target && e.target.id ? e.target.id.replace('ed-', 'ed-e-') : '';
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
        sort: sort,
        sortSel: sortSel,
        pickSearch: pickSearch,
        pickPage: pickPage,
        pickVideo: pickVideo,
        pickVid: pickVid,
        noVideo: noVideo,
        filter: filter,
        search: search,
        listed: listed,
        edit: edit,
        opened: opened,
        backToList: backToList,
        cmd: cmd,
        block: block,
        link: link,
        pickCover: pickCover,
        pickInline: pickInline,
        pick: pick,
        noCover: noCover,
        status: status,
        save: save,
        saved: saved,
        setWhen: setWhen,
        del: del
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminArticlesJs.reveal);
} else {
    AdminArticlesJs.reveal();
}
