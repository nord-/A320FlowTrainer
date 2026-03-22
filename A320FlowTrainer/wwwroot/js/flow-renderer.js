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
            const iconClass = entry.level === 'ok' ? 'mdi-check-circle-outline' :
                              entry.level === 'warn' ? 'mdi-alert-outline' :
                              entry.level === 'error' ? 'mdi-close-circle-outline' : 'mdi-information-outline';
            line.innerHTML = `<span class="mdi ${iconClass}"></span> ${escapeHtml(entry.message)}`;
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
                    <button class="btn-play" title="Start flow"><span class="mdi mdi-play"></span></button>
                    <button class="btn-expand" title="Show items"><span class="mdi mdi-chevron-down"></span></button>
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

            const table = document.createElement('table');
            table.className = 'flow-card-table';
            flow.items.forEach((item, j) => {
                const tr = document.createElement('tr');
                tr.innerHTML = `
                    <td class="fci-name">${escapeHtml(item.item)}</td>
                    <td class="fci-response">${escapeHtml(item.response)}</td>
                `;
                table.appendChild(tr);
            });
            details.appendChild(table);

            card.appendChild(header);
            card.appendChild(details);

            // Expand/collapse
            header.querySelector('.btn-expand').addEventListener('click', (e) => {
                e.stopPropagation();
                card.classList.toggle('expanded');
                const icon = header.querySelector('.btn-expand .mdi');
                icon.className = card.classList.contains('expanded') ? 'mdi mdi-chevron-up' : 'mdi mdi-chevron-down';
            });

            // Play button
            header.querySelector('.btn-play').addEventListener('click', (e) => {
                e.stopPropagation();
                if (sendFn) sendFn({ type: 'startFlow', flowIndex: i });
            });

            // Click header to expand too
            header.querySelector('.flow-card-info').addEventListener('click', () => {
                card.classList.toggle('expanded');
                const icon = header.querySelector('.btn-expand .mdi');
                icon.className = card.classList.contains('expanded') ? 'mdi mdi-chevron-up' : 'mdi mdi-chevron-down';
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
        const container = document.getElementById('item-list');
        container.innerHTML = '';

        const table = document.createElement('table');
        table.className = 'item-table';

        currentItems.forEach((item, i) => {
            const status = currentItemStatus[i];
            const isActive = i === activeIndex;

            const tr = document.createElement('tr');
            tr.className = 'item-row';
            tr.id = `item-${i}`;
            if (isActive) tr.classList.add('active');
            if (status === 'done') tr.classList.add('done');

            const statusIcon = status === 'done' ? '<span class="mdi mdi-check"></span>' :
                               isActive ? '<span class="mdi mdi-play"></span>' :
                               status === 'skip' ? '<span class="mdi mdi-minus"></span>' : '';

            tr.innerHTML = `
                <td class="item-status ${status}">${statusIcon}</td>
                <td class="item-name">${escapeHtml(item.item)}</td>
                <td class="item-response">${escapeHtml(item.response)}</td>
            `;

            table.appendChild(tr);
        });

        container.appendChild(table);

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
