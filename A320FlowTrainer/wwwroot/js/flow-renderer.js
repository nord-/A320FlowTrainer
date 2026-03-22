const FlowRenderer = (() => {
    let currentItems = [];
    let currentItemStatus = [];

    function showView(viewId) {
        document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
        document.getElementById(viewId).classList.add('active');
    }

    function showWelcome() {
        showView('view-welcome');
    }

    function showFlowActivation(data) {
        document.getElementById('flow-counter').textContent =
            `FLOW ${data.flowIndex + 1} / ${data.totalFlows}`;
        document.getElementById('activation-flow-name').textContent = data.flowName;

        const noteEl = document.getElementById('activation-flow-note');
        if (data.flowNote) {
            noteEl.textContent = data.flowNote;
            noteEl.style.display = '';
        } else {
            noteEl.style.display = 'none';
        }

        showView('view-activation');
    }

    function showFlow(data) {
        currentItems = data.items;
        currentItemStatus = [...data.itemStatus];

        document.getElementById('flow-name').textContent = data.flowName;
        updateProgress();
        renderItems(-1);
        showView('view-flow');
    }

    function renderItems(activeIndex) {
        const list = document.getElementById('item-list');
        list.innerHTML = '';

        currentItems.forEach((item, i) => {
            const status = currentItemStatus[i];
            const isActive = i === activeIndex;

            const row = document.createElement('div');
            row.className = 'item-row';
            row.id = `item-${i}`;
            if (isActive) row.classList.add('active');
            if (status === 'done') row.classList.add('done');

            const statusIcon = status === 'done' ? '&#10003;' :
                               isActive ? '&#9658;' :
                               status === 'skip' ? '&#183;' : '';

            row.innerHTML = `
                <span class="item-status ${status}">${statusIcon}</span>
                <span class="item-number">${i + 1}.</span>
                <span class="item-name">${escapeHtml(item.item)}</span>
                <span class="item-separator">&rarr;</span>
                <span class="item-response">${escapeHtml(item.response)}</span>
            `;

            list.appendChild(row);
        });

        // Scrolla till aktiv item
        if (activeIndex >= 0) {
            const activeRow = document.getElementById(`item-${activeIndex}`);
            if (activeRow) {
                activeRow.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    }

    function updateItem(data) {
        currentItemStatus[data.itemIndex] = data.status;
        if (data.itemStatusArray) {
            for (let i = 0; i < data.itemStatusArray.length; i++) {
                currentItemStatus[i] = data.itemStatusArray[i];
            }
        }
        updateProgress();
        renderItems(data.activeIndex);
    }

    function updateProgress() {
        const done = currentItemStatus.filter(s => s === 'done').length;
        const total = currentItems.length;
        document.getElementById('flow-progress').textContent = `[${done}/${total}]`;
    }

    function showComplete() {
        showView('view-complete');
    }

    function setMicState(listening) {
        const el = document.getElementById('mic-indicator');
        el.classList.toggle('on', listening);
        el.classList.toggle('off', !listening);
    }

    function setPaused(paused) {
        document.getElementById('pause-overlay').classList.toggle('hidden', !paused);
        const el = document.getElementById('pause-indicator');
        el.classList.toggle('on', paused);
        el.classList.toggle('off', !paused);
    }

    function showSpeechFeedback(text, score, matched) {
        const el = document.getElementById('speech-feedback');
        const color = matched ? 'var(--green)' : score >= 50 ? 'var(--amber)' : 'var(--text-dim)';
        el.style.color = color;
        el.textContent = `Heard: "${text}" ${score != null ? `(${score}%)` : ''}`;

        // Rensa efter 3 sekunder om inget nytt
        clearTimeout(el._timer);
        el._timer = setTimeout(() => { el.textContent = ''; }, 3000);
    }

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    return {
        showWelcome,
        showFlowActivation,
        showFlow,
        updateItem,
        showComplete,
        setMicState,
        setPaused,
        showSpeechFeedback
    };
})();
