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

// Copy text to clipboard using Clipboard API
window.copyToClipboard = function(text) {
    // Fallback if clipboard API not available
    if (!navigator.clipboard) {
        console.warn('Clipboard API not available');
        return Promise.reject(new Error('Clipboard API not available'));
    }
    
    return navigator.clipboard.writeText(text)
        .then(() => {
            console.log('copyToClipboard: Text copied successfully');
            return true;
        })
        .catch(error => {
            console.error('copyToClipboard: Failed to copy to clipboard:', error);
            return false;
        });
};
