var AdminMediaJs = (function () {

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
        $ApiRequest('AdminMedia/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('md-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('md-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('md-open');
        }, 3200);
    }

    function openNav() {
        el('md-nav').classList.add('md-open');
        el('md-scrim').classList.add('md-open');
    }

    function closeNav() {
        el('md-nav').classList.remove('md-open');
        el('md-scrim').classList.remove('md-open');
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
        el('md-modal').classList.add('md-open');
    }

    function closeModal() {
        var m = el('md-modal');
        if (m) {
            m.classList.remove('md-open');
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
        if (!askBox || !askBox.classList.contains('md-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('md-open');
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
        askBox.className = 'md-ask';
        askBox.innerHTML = '<div class="md-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="md-err" role="alert"></em><div class="md-askacts"><button type="button" class="md-btn md-btn-ghost md-btn-sm"></button><button type="button" class="md-btn md-btn-sm"></button></div></div>';
        (el('md-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('md-open')) {
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
        x.b[1].className = 'md-btn md-btn-sm' + (o.danger ? ' md-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('md-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('md-open');
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

    var page = 1;
    var kind = '';
    var stimer = null;
    var full = '';
    var thumb = '';
    var vfile = null;
    var vposter = '';
    var vsecs = 0;
    var vuid = '';
    var vtotal = 0;
    var CHUNK = 2 * 1024 * 1024;

    function load() {
        api('Load', []);
    }

    function loaded() {
        full = '';
        thumb = '';
    }

    function list(pg) {
        page = pg || 1;
        api('List', [{ key: 'mq', vlu: val('md-mq') }, { key: 'mby', vlu: val('md-mby') }, { key: 'mkind', vlu: kind }, { key: 'pg', vlu: String(page) }]);
    }

    function setKind(k) {
        kind = k;
        var b = document.querySelectorAll('.md-kinds .md-chip');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('md-act', b[i].getAttribute('data-k') === k);
        }
        list(1);
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 300);
    }

    function shrink(img, max, type, q) {
        var w = img.naturalWidth;
        var h = img.naturalHeight;
        var r = Math.min(1, max / Math.max(w, h));
        var c = document.createElement('canvas');
        c.width = Math.round(w * r);
        c.height = Math.round(h * r);
        c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
        var d = c.toDataURL(type, q);
        return d.indexOf('data:' + type) === 0 ? d : c.toDataURL('image/jpeg', q);
    }

    function esc(t) {
        return String(t).replace(/[&<>"]/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[ch];
        });
    }

    function mb(n) {
        return (n / 1048576).toFixed(n < 10485760 ? 1 : 0) + ' MB';
    }

    function clock(sec) {
        return Math.floor(sec / 60) + ':' + pad2(sec % 60);
    }

    function pad2(n) {
        return (n < 10 ? '0' : '') + n;
    }

    function setBox(boxId, inputId, html) {
        var inp = el(inputId);
        var box = el(boxId);
        box.innerHTML = html;
        box.appendChild(inp);
        box.classList.remove('md-isdone');
    }

    function loadImg(src, ok, bad) {
        var img = new Image();
        img.onload = function () {
            ok(img);
        };
        img.onerror = bad;
        img.src = src;
    }

    function file(input) {
        var f = input.files && input.files[0];
        el('md-e-file').textContent = '';
        full = '';
        thumb = '';
        if (!f) {
            return;
        }
        if (!/^image\/(jpeg|png|webp)$/.test(f.type)) {
            el('md-e-file').textContent = 'Use a JPG, PNG or WebP image.';
            return;
        }
        if (f.size > 30 * 1024 * 1024) {
            el('md-e-file').textContent = 'The image must be under 30 MB.';
            return;
        }
        var label = esc(f.name) + ' &middot; ' + mb(f.size);
        setBox('md-prev', 'md-file', '<span>Reading&hellip;</span><span class="md-finfo">' + label + '</span>');
        if (!val('md-mtitle')) {
            el('md-mtitle').value = f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ');
        }
        var done = function (img) {
            full = shrink(img, 1600, 'image/webp', 0.86);
            thumb = shrink(img, 720, 'image/webp', 0.8);
            setBox('md-prev', 'md-file', '<img src="' + thumb + '" alt=""><span class="md-finfo">' + label + ' &middot; ' + img.naturalWidth + '&times;' + img.naturalHeight + '</span>');
        };
        var fail = function () {
            setBox('md-prev', 'md-file', '<span>Choose an image<br>JPG, PNG or WebP</span><span class="md-finfo">' + label + '</span>');
            el('md-e-file').textContent = 'That image could not be read.';
        };
        var viaReader = function () {
            var r = new FileReader();
            r.onload = function () {
                loadImg(String(r.result), done, fail);
            };
            r.onerror = fail;
            r.readAsDataURL(f);
        };
        var url = '';
        try {
            url = URL.createObjectURL(f);
        } catch (e) {
            url = '';
        }
        if (!url) {
            viaReader();
            return;
        }
        loadImg(url, function (img) {
            done(img);
            URL.revokeObjectURL(url);
        }, function () {
            URL.revokeObjectURL(url);
            viaReader();
        });
    }

    function upload() {
        if (!full) {
            el('md-e-file').textContent = 'Choose an image first.';
            return;
        }
        api('Upload', [{ key: 'full', vlu: full }, { key: 'thumb', vlu: thumb }, { key: 'mtitle', vlu: val('md-mtitle') }, { key: 'credit', vlu: val('md-credit') }]);
    }

    function markDone(boxId, inputId) {
        var box = el(boxId);
        var info = box.querySelector('.md-finfo');
        if (info) {
            info.innerHTML = '&#10003; Uploaded &middot; ' + info.innerHTML;
        }
        box.classList.add('md-isdone');
        el(inputId).value = '';
    }

    function uploaded() {
        full = '';
        thumb = '';
        markDone('md-prev', 'md-file');
        el('md-mtitle').value = '';
        list(1);
    }

    function capture(f, src, label, retry) {
        var v = document.createElement('video');
        var finished = false;
        var timer = null;
        var stop = function (ok) {
            if (finished) {
                return;
            }
            finished = true;
            clearTimeout(timer);
            if (src.indexOf('blob:') === 0) {
                URL.revokeObjectURL(src);
            }
            if (!ok && retry && f.size <= 25 * 1024 * 1024) {
                var r = new FileReader();
                r.onload = function () {
                    capture(f, String(r.result), label, false);
                };
                r.readAsDataURL(f);
                return;
            }
            if (!ok) {
                setBox('md-vprev', 'md-vfile', '<span>' + esc(f.name) + '<br>No preview in this browser</span><span class="md-finfo">' + label + (vsecs ? ' &middot; ' + clock(vsecs) : '') + '</span>');
            }
        };
        v.muted = true;
        v.preload = 'auto';
        v.playsInline = true;
        v.addEventListener('loadedmetadata', function () {
            vsecs = isFinite(v.duration) ? Math.round(v.duration) : 0;
            v.currentTime = Math.min(1, (v.duration || 2) / 2);
        });
        v.addEventListener('seeked', function () {
            try {
                var r = Math.min(1, 1280 / v.videoWidth);
                var c = document.createElement('canvas');
                c.width = Math.round(v.videoWidth * r);
                c.height = Math.round(v.videoHeight * r);
                c.getContext('2d').drawImage(v, 0, 0, c.width, c.height);
                vposter = c.toDataURL('image/jpeg', 0.82);
                setBox('md-vprev', 'md-vfile', '<img src="' + vposter + '" alt=""><span class="md-play"></span><span class="md-finfo">' + label + (vsecs ? ' &middot; ' + clock(vsecs) : '') + '</span>');
                stop(true);
            } catch (e) {
                vposter = '';
                stop(false);
            }
        });
        v.addEventListener('error', function () {
            stop(false);
        });
        timer = setTimeout(function () {
            stop(false);
        }, 8000);
        v.src = src;
    }

    function vpick(input) {
        var f = input.files && input.files[0];
        var max = parseInt(input.getAttribute('data-max'), 10);
        el('md-e-vfile').textContent = '';
        vfile = null;
        vposter = '';
        vsecs = 0;
        if (!f) {
            return;
        }
        if (!/^video\/(mp4|webm)$/.test(f.type) && !/\.(mp4|webm)$/i.test(f.name)) {
            el('md-e-vfile').textContent = 'Use an MP4 or WebM video.';
            return;
        }
        if (f.size > max) {
            el('md-e-vfile').textContent = 'The video must be under ' + Math.round(max / 1048576) + ' MB.';
            return;
        }
        vfile = f;
        if (!val('md-vtitle')) {
            el('md-vtitle').value = f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ');
        }
        var label = esc(f.name) + ' &middot; ' + mb(f.size);
        setBox('md-vprev', 'md-vfile', '<span>Reading&hellip;</span><span class="md-finfo">' + label + '</span>');
        var url = '';
        try {
            url = URL.createObjectURL(f);
        } catch (e) {
            url = '';
        }
        if (url) {
            capture(f, url, label, true);
        } else if (f.size <= 25 * 1024 * 1024) {
            var r = new FileReader();
            r.onload = function () {
                capture(f, String(r.result), label, false);
            };
            r.readAsDataURL(f);
        }
    }

    function hex() {
        var a = new Uint8Array(16);
        (window.crypto || window.msCrypto).getRandomValues(a);
        var s = '';
        for (var i = 0; i < a.length; i++) {
            s += (a[i] < 16 ? '0' : '') + a[i].toString(16);
        }
        return s;
    }

    function vupload() {
        if (!vfile) {
            el('md-e-vfile').textContent = 'Choose a video first.';
            return;
        }
        if (val('md-vtitle').length < 2) {
            el('md-e-vtitle').textContent = 'Give the video a title.';
            return;
        }
        vuid = hex();
        vtotal = Math.ceil(vfile.size / CHUNK);
        el('md-vbtn').disabled = true;
        el('md-vprog').classList.add('md-on');
        send(0);
    }

    function send(i) {
        var r = new FileReader();
        r.onload = function () {
            var d = String(r.result);
            api('Chunk', [{ key: 'uid', vlu: vuid }, { key: 'idx', vlu: String(i) }, { key: 'total', vlu: String(vtotal) }, { key: 'part', vlu: d.substring(d.indexOf(',') + 1) }]);
        };
        r.onerror = function () {
            chunkFail('That file could not be read.');
        };
        r.readAsDataURL(vfile.slice(i * CHUNK, (i + 1) * CHUNK));
    }

    function chunkOk(i) {
        el('md-vbar').style.width = Math.round((i + 1) / vtotal * 100) + '%';
        if (i + 1 < vtotal) {
            send(i + 1);
            return;
        }
        api('VideoDone', [{ key: 'uid', vlu: vuid }, { key: 'total', vlu: String(vtotal) }, { key: 'vtitle', vlu: val('md-vtitle') }, { key: 'vcredit', vlu: val('md-vcredit') }, { key: 'poster', vlu: vposter }, { key: 'secs', vlu: String(vsecs) }]);
    }

    function chunkFail(msg) {
        el('md-e-vfile').textContent = msg;
        el('md-vbtn').disabled = false;
        el('md-vprog').classList.remove('md-on');
        el('md-vbar').style.width = '0';
    }

    function vdone() {
        vfile = null;
        vposter = '';
        markDone('md-vprev', 'md-vfile');
        el('md-vtitle').value = '';
        chunkFail('');
        list(1);
    }

    function preview(src, poster) {
        el('md-mbody').innerHTML = '<div class="md-pv"><video controls autoplay playsinline poster="' + poster + '" src="' + src + '"></video></div>';
        openModal();
    }

    function closePreview() {
        var v = document.querySelector('#md-mbody video');
        if (v) {
            v.pause();
        }
    }

    function removed() {
        list(page);
    }

    function del(id, btn) {
        var card = btn && btn.closest ? btn.closest('.md-mi') : null;
        var t = card && card.querySelector('b') ? card.querySelector('b').textContent : '';
        ask({ title: 'Delete from the library?', text: (t ? '\u201c' + t + '\u201d will be removed. ' : '') + 'This cannot be undone.', ok: 'Delete', danger: true }, function () {
            api('Remove', [{ key: 'mid', vlu: id }]);
        });
    }

    document.addEventListener('click', function (e) {
        if (e.target && (e.target.id === 'md-modal' || (e.target.closest && e.target.closest('.md-mclose')))) {
            closePreview();
        }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            closePreview();
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
            var id = e.target && e.target.id ? e.target.id.replace('md-', 'md-e-') : '';
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
        setKind: setKind,
        search: search,
        file: file,
        upload: upload,
        uploaded: uploaded,
        vpick: vpick,
        vupload: vupload,
        chunkOk: chunkOk,
        chunkFail: chunkFail,
        vdone: vdone,
        preview: preview,
        removed: removed,
        del: del
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminMediaJs.reveal);
} else {
    AdminMediaJs.reveal();
}
