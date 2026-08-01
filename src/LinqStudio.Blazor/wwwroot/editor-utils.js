// Draggable vertical splitter for editor/results panels
window.initSplitter = function(splitterId, topId, bottomId) {
    const splitter = document.getElementById(splitterId);
    const top = document.getElementById(topId);
    const bottom = document.getElementById(bottomId);
    
    if (!splitter || !top || !bottom) {
        return false;
    }

    let isDragging = false;
    
    splitter.addEventListener('mousedown', function(e) {
        isDragging = true;
        e.preventDefault();
        document.body.style.cursor = 'row-resize';
        document.body.style.userSelect = 'none';
    });
    
    const onMouseMove = function(e) {
        if (!isDragging) return;
        const container = splitter.parentElement;
        if (!container) return;
        const containerRect = container.getBoundingClientRect();
        const newTopHeight = e.clientY - containerRect.top;
        const totalHeight = containerRect.height;
        const splitterHeight = splitter.offsetHeight;
        const minHeight = 80;
        if (newTopHeight < minHeight || newTopHeight > totalHeight - minHeight - splitterHeight) return;
        top.style.height = newTopHeight + 'px';
        bottom.style.height = (totalHeight - newTopHeight - splitterHeight) + 'px';
    };
    
    const onMouseUp = function() {
        if (isDragging) {
            isDragging = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            splitter.classList.remove('dragging');
        }
    };
    
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);

    splitter.addEventListener('mousedown', function() {
        splitter.classList.add('dragging');
    });
    
    window._splitterCleanups = window._splitterCleanups || {};
    window._splitterCleanups[splitterId] = function() {
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
        if (isDragging) {
            isDragging = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
        }
    };

    return true;
};

window.disposeSplitter = function(splitterId) {
    if (window._splitterCleanups && window._splitterCleanups[splitterId]) {
        window._splitterCleanups[splitterId]();
        delete window._splitterCleanups[splitterId];
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
