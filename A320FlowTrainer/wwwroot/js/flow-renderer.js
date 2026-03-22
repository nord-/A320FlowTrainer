const FlowRenderer = (() => {
    let allFlows = [];
    let currentItems = [];
    let currentItemStatus = [];
    let sendFn = null;

    function init(send) {
        sendFn = send;
    }

    function showView(viewId) {
        document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
        document.getElementById(viewId).classList.add('active');
    }

    function showConnecting() {
        showView('view-connecting');
    }

    function showFlowList(flows, startupLog) {
        if (flows) allFlows = flows;
        if (startupLog) renderStartupLog(startupLog);
        renderFlowList();
        showView('view-flow-list');
    }

    function renderStartupLog(entries) {
        const container = document.getElementById('startup-log');
        container.innerHTML = '';
        entries.forEach(entry => {
            const line = document.createElement('div');
            line.className = `log-entry log-${entry.level}`;
            const icon = entry.level === 'ok' ? '\u2713' :
                         entry.level === 'warn' ? '\u26A0' :
                         entry.level === 'error' ? '\u2717' : '\u2022';
            line.textContent = `${icon} ${entry.message}`;
            container.appendChild(line);
        });
    }

    function renderFlowList() {
        const list = document.getElementById('flow-list');
        list.innerHTML = '';

        allFlows.forEach((flow, i) => {
            const card = document.createElement('div');
            card.className = 'flow-card';

            const header = document.createElement('div');
            header.className = 'flow-card-header';
            header.innerHTML = `
                <div class="flow-card-info">
                    <span class="flow-card-number">${i + 1}</span>
                    <span class="flow-card-name">${escapeHtml(flow.name)}</span>
                    <span class="flow-card-count">${flow.items.length} items</span>
                </div>
                <div class="flow-card-actions">
                    <button class="btn-play" title="Start flow">&#9654;</button>
                    <button class="btn-expand" title="Show items">&#9660;</button>
                </div>
            `;

            const details = document.createElement('div');
            details.className = 'flow-card-details';

            if (flow.note) {
                const note = document.createElement('div');
                note.className = 'flow-card-note';
                note.textContent = flow.note;
                details.appendChild(note);
            }

            const itemTable = document.createElement('div');
            itemTable.className = 'flow-card-items';
            flow.items.forEach((item, j) => {
                const row = document.createElement('div');
                row.className = 'flow-card-item';
                row.innerHTML = `
                    <span class="fci-number">${j + 1}.</span>
                    <span class="fci-name">${escapeHtml(item.item)}</span>
                    <span class="fci-sep">&rarr;</span>
                    <span class="fci-response">${escapeHtml(item.response)}</span>
                `;
                itemTable.appendChild(row);
            });
            details.appendChild(itemTable);

            card.appendChild(header);
            card.appendChild(details);

            // Expand/collapse
            header.querySelector('.btn-expand').addEventListener('click', (e) => {
                e.stopPropagation();
                card.classList.toggle('expanded');
                const btn = header.querySelector('.btn-expand');
                btn.innerHTML = card.classList.contains('expanded') ? '&#9650;' : '&#9660;';
            });

            // Play button
            header.querySelector('.btn-play').addEventListener('click', (e) => {
                e.stopPropagation();
                if (sendFn) sendFn({ type: 'startFlow', flowIndex: i });
            });

            // Click header to expand too
            header.querySelector('.flow-card-info').addEventListener('click', () => {
                card.classList.toggle('expanded');
                const btn = header.querySelector('.btn-expand');
                btn.innerHTML = card.classList.contains('expanded') ? '&#9650;' : '&#9660;';
            });

            list.appendChild(card);
        });
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

        clearTimeout(el._timer);
        el._timer = setTimeout(() => { el.textContent = ''; }, 3000);
    }

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    return {
        init,
        showConnecting,
        showFlowList,
        showFlow,
        updateItem,
        showComplete,
        setMicState,
        setPaused,
        showSpeechFeedback
    };
})();
