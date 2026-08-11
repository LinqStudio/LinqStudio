// Reusable draggable splitter for panel layouts and resizable drawer targets.
window.initResizableSplitter = function(splitterId, orientation, firstId, secondId, targetId) {
    const splitter = document.getElementById(splitterId);
    const first = firstId ? document.getElementById(firstId) : null;
    const second = secondId ? document.getElementById(secondId) : null;
    const target = targetId ? document.getElementById(targetId) : null;
    
    if (!splitter || (orientation === 'horizontal' && (!first || !second))
        || (orientation === 'vertical' && !target)) {
        return false;
    }

    const container = target?.closest('.mud-drawer-container') || target?.parentElement;
    let isDragging = false;
    const minSize = 80;
    const minWidth = 180;
    const maxWidth = Math.min(520, window.innerWidth * 0.5);
    
    const onMouseDown = function(e) {
        isDragging = true;
        e.preventDefault();
        document.body.style.cursor = orientation === 'horizontal' ? 'row-resize' : 'col-resize';
        document.body.style.userSelect = 'none';
        splitter.classList.add('dragging');
    };
    
    const onMouseMove = function(e) {
        if (!isDragging) return;
        if (orientation === 'horizontal') {
            const containerRect = splitter.parentElement?.getBoundingClientRect();
            if (!containerRect || !first || !second) return;
            const newFirstSize = e.clientY - containerRect.top;
            const totalSize = containerRect.height;
            const splitterSize = splitter.offsetHeight;
            if (newFirstSize < minSize || newFirstSize > totalSize - minSize - splitterSize) return;
            first.style.height = newFirstSize + 'px';
            second.style.height = (totalSize - newFirstSize - splitterSize) + 'px';
        } else if (target && container) {
            const targetRect = target.getBoundingClientRect();
            const width = Math.max(minWidth, Math.min(maxWidth, e.clientX - targetRect.left));
            const widthValue = width + 'px';
            container.style.setProperty('--mud-drawer-width-left', widthValue);
            target.style.setProperty('--mud-drawer-width', widthValue);
        }
    };
    
    const onMouseUp = function() {
        if (!isDragging) return;
        isDragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        splitter.classList.remove('dragging');
    };
    
    splitter.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
    
    window._resizableSplitterCleanups = window._resizableSplitterCleanups || {};
    window._resizableSplitterCleanups[splitterId] = function() {
        splitter.removeEventListener('mousedown', onMouseDown);
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
        onMouseUp();
    };

    return true;
};

window.disposeResizableSplitter = function(splitterId) {
    if (window._resizableSplitterCleanups && window._resizableSplitterCleanups[splitterId]) {
        window._resizableSplitterCleanups[splitterId]();
        delete window._resizableSplitterCleanups[splitterId];
    }
};

// Draggable vertical splitter for the database explorer drawer.
window.initDrawerSplitter = function(splitterId, drawerId) {
    const splitter = document.getElementById(splitterId);
    const drawer = document.getElementById(drawerId);

    if (!splitter || !drawer) {
        return false;
    }

    const container = drawer.closest('.mud-drawer-container') || drawer.parentElement;
    if (!container) {
        return false;
    }

    let isDragging = false;
    const minWidth = 180;
    const maxWidth = Math.min(520, window.innerWidth * 0.5);

    const setWidth = function(width) {
        const clampedWidth = Math.max(minWidth, Math.min(maxWidth, width));
        const widthValue = clampedWidth + 'px';
        container.style.setProperty('--mud-drawer-width-left', widthValue);
        drawer.style.setProperty('--mud-drawer-width', widthValue);
    };

    const onMouseDown = function(e) {
        isDragging = true;
        e.preventDefault();
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        splitter.classList.add('dragging');
    };

    const onMouseMove = function(e) {
        if (!isDragging) return;
        const drawerRect = drawer.getBoundingClientRect();
        setWidth(e.clientX - drawerRect.left);
    };

    const onMouseUp = function() {
        if (!isDragging) return;
        isDragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        splitter.classList.remove('dragging');
    };

    splitter.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);

    window._drawerSplitterCleanups = window._drawerSplitterCleanups || {};
    window._drawerSplitterCleanups[splitterId] = function() {
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
        onMouseUp();
    };

    return true;
};

window.disposeDrawerSplitter = function(splitterId) {
    if (window._drawerSplitterCleanups && window._drawerSplitterCleanups[splitterId]) {
        window._drawerSplitterCleanups[splitterId]();
        delete window._drawerSplitterCleanups[splitterId];
    }
};

// Force Monaco editor to re-measure and re-layout its container.
// Called after a tab panel becomes visible again (KeepPanelsAlive hides via display:none).
// Passing no dimension to layout() causes Monaco to auto-read the container size.
window.monacoRelayout = function(editorId) {
    const container = document.getElementById(editorId);
    if (!container) return false;

    try {
        const editors = monaco.editor.getEditors();
        const editor = editors.find(e => {
            const node = e.getDomNode();
            return node && container.contains(node);
        });

        if (editor) {
            editor.layout();
            return true;
        }
    } catch (e) {
        console.warn('monacoRelayout: could not relayout editor for', editorId, e);
    }
    return false;
};

// Reset vertical scroll on .mud-tabs to prevent the tab header bar from being pushed
// behind the fixed app bar. MudBlazor may set scrollTop > 0 when switching panels
// (via scrollIntoView on the newly-active panel). The sticky CSS fix keeps the
// toolbar visible regardless, but this cleans up the scroll offset so panel content
// is not shifted.
window.resetMudTabsScroll = function() {
    const mudTabs = document.querySelector('.mud-tabs');
    if (mudTabs) mudTabs.scrollTop = 0;
};

// Copy text to clipboard using Clipboard API with execCommand fallback
window.copyToClipboard = function(text) {
    // Try the modern Clipboard API first (requires 'clipboard-write' permission or user activation)
    if (navigator.clipboard && navigator.clipboard.writeText) {
        return navigator.clipboard.writeText(text)
            .then(() => {
                console.log('copyToClipboard: Text copied via Clipboard API');
                return true;
            })
            .catch(error => {
                console.warn('copyToClipboard: Clipboard API failed, trying execCommand fallback:', error);
                return window._copyViaExecCommand(text);
            });
    }

    // Fallback: execCommand (deprecated but still works in Chromium for test environments)
    return Promise.resolve(window._copyViaExecCommand(text));
};

window._copyViaExecCommand = function(text) {
    try {
        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        textarea.style.top = '-9999px';
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();
        const success = document.execCommand('copy');
        document.body.removeChild(textarea);
        if (success) {
            console.log('copyToClipboard: Text copied via execCommand fallback');
        } else {
            console.error('copyToClipboard: execCommand fallback also failed');
        }
        return success;
    } catch (err) {
        console.error('copyToClipboard: All copy methods failed:', err);
        return false;
    }
};
