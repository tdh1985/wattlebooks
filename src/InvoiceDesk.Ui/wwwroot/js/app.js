// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

// small browser-side helpers; everything else lives in c#
window.invoicedesk = {
    init(ref) {
        this.ref = ref;
        const typing = (el) => el instanceof Element && (el.closest('input, textarea, select') || el.isContentEditable);
        document.addEventListener('keydown', (e) => {
            const mod = e.ctrlKey || e.metaKey;
            const key = e.key.toLowerCase();
            if (mod && !e.shiftKey && !e.altKey && (key === 'k' || key === 'n' || key === 's')) {
                e.preventDefault();
                ref.invokeMethodAsync('OnShortcut', key);
            } else if (e.key === '[' && !mod && !e.altKey && !typing(e.target)) {
                e.preventDefault();
                ref.invokeMethodAsync('OnShortcut', 'fold');
            } else if (e.key === '?' && !mod && !e.altKey && !typing(e.target)) {
                e.preventDefault();
                ref.invokeMethodAsync('OnShortcut', 'help');
            } else if (e.key === 'Escape') {
                // esc in a field with a suggestion list closes the list, not the drawer
                if (e.target instanceof Element && e.target.matches('input[list]')) return;
                // the shortcut service closes the dialog so the native close must not
                if (document.querySelector('dialog[open]')) e.preventDefault();
                ref.invokeMethodAsync('OnShortcut', 'escape');
            }
        });
        // a file dropped outside a drop zone would otherwise open in the webview
        const outsideDropZone = (e) => !(e.target instanceof Element && e.target.closest('input[type=file]'));
        document.addEventListener('dragover', (e) => {
            if (outsideDropZone(e)) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'none';
            }
        });
        document.addEventListener('drop', (e) => { if (outsideDropZone(e)) e.preventDefault(); });
        this.liftToasts();
    },

    // the top layer stacks in opening order, so reopening puts toasts on top
    liftToasts() {
        const toasts = document.querySelector('.toasts[popover]');
        if (!toasts) return;
        try {
            if (toasts.matches(':popover-open')) toasts.hidePopover();
            toasts.showPopover();
        } catch (e) { }
    },

    // lets the windows 11 backdrop show through the desk around the sheet
    setMica(on) {
        try { localStorage.setItem('invoicedesk-mica', on ? '1' : '0'); } catch (e) { }
        document.documentElement.classList.toggle('mica', on);
    },

    applyTheme(theme) {
        try { localStorage.setItem('invoicedesk-theme', theme); } catch (e) { }
        const media = matchMedia('(prefers-color-scheme: dark)');
        const paint = () => {
            const dark = theme === 'dark' || (theme === 'system' && media.matches);
            document.documentElement.dataset.theme = dark ? 'dark' : 'light';
            this.ref?.invokeMethodAsync('ThemeResolved', dark);
        };
        media.onchange = theme === 'system' ? paint : null;
        paint();
    },

    // arrows and enter belong to an open suggestion list, not the textarea
    suggestKeys(ref) {
        this.suggestRef = ref;
        if (this._suggestBound) return;
        this._suggestBound = true;
        document.addEventListener('keydown', (e) => {
            const el = e.target;
            if (!(el instanceof Element) || e.ctrlKey || e.altKey || e.metaKey || e.shiftKey) return;
            const state = el.getAttribute('data-suggest-open');
            if (!state || !this.suggestRef) return;
            // enter only picks once something is highlighted, otherwise it types a new line
            const ours = e.key === 'ArrowDown' || e.key === 'ArrowUp' || (e.key === 'Enter' && state === 'active');
            if (!ours) return;
            e.preventDefault();
            this.suggestRef.invokeMethodAsync('OnSuggestKey', e.key).catch(() => { });
        }, true);
    },

    // a file dragged over the money page anywhere shows the big drop target
    watchFileDrag(ref) {
        this.fileDragRef = ref;
        if (this._fileDragBound) return;
        this._fileDragBound = true;
        const hasFiles = (e) => e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files');
        document.addEventListener('dragenter', (e) => {
            if (!this.fileDragRef || !hasFiles(e)) return;
            if (e.target instanceof Element && e.target.closest('.dropzone, .drawer, .modal')) return;
            this.fileDragRef.invokeMethodAsync('ShowPageDrop').catch(() => { });
        });
        // no related target means the drag left the window altogether
        document.addEventListener('dragleave', (e) => {
            if (!this.fileDragRef || e.relatedTarget) return;
            this.fileDragRef.invokeMethodAsync('HidePageDrop').catch(() => { });
        });
    },

    unwatchFileDrag() {
        this.fileDragRef = null;
    },

    // a native modal dialog traps focus and makes the page behind it inert
    dialogs: new Map(),

    showDialog(el, key, ref, fields) {
        if (!el || el.open) return;
        for (const [k, d] of this.dialogs) if (!d.el.isConnected) this.dialogs.delete(k);
        this.dialogs.set(key, { el, opener: document.activeElement });
        // esc already reaches the shortcut service, one close is enough
        el.addEventListener('cancel', (e) => e.preventDefault());
        const outside = (e) => {
            const r = el.getBoundingClientRect();
            return e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom;
        };
        // a drag that starts inside and ends on the backdrop must not close it
        let fromBackdrop = false;
        el.addEventListener('pointerdown', (e) => { fromBackdrop = e.target === el && outside(e); });
        el.addEventListener('click', (e) => {
            if (fromBackdrop && e.target === el && outside(e)) ref.invokeMethodAsync('CloseFromBackdrop').catch(() => { });
            fromBackdrop = false;
        });
        el.showModal();
        this.liftToasts();
        const first = el.querySelector('[autofocus], [data-first]') ?? (fields ? el.querySelector(fields) : null);
        if (!first) return;
        first.focus();
        if (first.tagName === 'INPUT' && typeof first.select === 'function') first.select();
    },

    // runs after blazor has removed the dialog, so it goes by key
    closeDialog(key) {
        const d = this.dialogs.get(key);
        if (!d) return;
        this.dialogs.delete(key);
        if (d.el.open) d.el.close();
        if (d.opener instanceof HTMLElement && d.opener.isConnected) d.opener.focus();
    },

    focus(selector) {
        const el = document.querySelector(selector);
        if (!el) return;
        el.focus();
        if (typeof el.select === 'function' && el.tagName === 'INPUT') el.select();
    },

    // the page shrinks to fit narrow panes instead of overflowing them
    fitPreview(el) {
        if (!el || el._fit) return;
        const fit = () => {
            const doc = el.querySelector('.doc');
            if (!doc) return;
            const mm = parseFloat(getComputedStyle(doc).getPropertyValue('--doc-w')) || 210;
            // rounding keeps a4's scale from drifting off its old exact value
            const pageWidth = Math.round(mm * 96 / 25.4);
            const width = el.clientWidth - 48;
            el.style.setProperty('--scale', Math.max(0.3, Math.min(1, width / pageWidth)).toFixed(4));
        };
        el._fit = new ResizeObserver(fit);
        el._fit.observe(el);
        fit();
    },
};
