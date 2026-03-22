(() => {
    let ws = null;
    let inputDevices = [];
    let serverOutputDevices = [];

    function connect() {
        const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
        ws = new WebSocket(`${protocol}//${location.host}/ws`);

        ws.onopen = () => {
            const params = new URLSearchParams(location.search);
            send({ type: 'ready', testMode: params.has('test') });
        };

        ws.onmessage = (e) => {
            try {
                const msg = JSON.parse(e.data);
                handleMessage(msg);
            } catch (err) {
                console.error('Bad message:', err);
            }
        };

        ws.onclose = () => {
            FlowRenderer.showConnecting();
            setTimeout(connect, 2000);
        };
    }

    function send(msg) {
        if (ws && ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify(msg));
        }
    }

    function handleMessage(msg) {
        switch (msg.type) {
            case 'init':
                FlowRenderer.showFlowList(msg.flows, msg.startupLog);
                inputDevices = msg.inputDevices || [];
                serverOutputDevices = msg.outputDevices || [];
                populateInputDevices(msg.currentInputDevice);
                populateOutputDevices();
                break;

            case 'showFlowList':
                FlowRenderer.showFlowList();
                break;

            case 'showFlow':
                FlowRenderer.showFlow(msg);
                break;

            case 'updateItem':
                FlowRenderer.updateItem(msg);
                break;

            case 'playAudio':
                AudioPlayer.play(msg.url, msg.fallbackText, () => {
                    send({ type: 'audioComplete', audioId: msg.audioId });
                });
                break;

            case 'listeningState':
                FlowRenderer.setMicState(msg.listening);
                break;

            case 'speechHeard':
                FlowRenderer.showSpeechFeedback(msg.text, msg.score, msg.matched);
                break;

            case 'paused':
                FlowRenderer.setPaused(msg.paused);
                break;

            case 'flowComplete':
                FlowRenderer.showComplete();
                break;

            case 'allComplete':
                FlowRenderer.showComplete();
                break;
        }
    }

    // Settings
    const settingsModal = document.getElementById('settings-modal');
    const inputSelect = document.getElementById('input-device');
    const outputSelect = document.getElementById('output-device');

    function populateInputDevices(currentId) {
        inputSelect.innerHTML = '';
        inputDevices.forEach(d => {
            const opt = document.createElement('option');
            opt.value = d.id;
            opt.textContent = d.name;
            if (d.id === currentId) opt.selected = true;
            inputSelect.appendChild(opt);
        });
    }

    async function populateOutputDevices() {
        // Prova att lasa upp browser device labels via getUserMedia
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            stream.getTracks().forEach(t => t.stop());
        } catch { }

        const browserDevices = await AudioPlayer.getOutputDevices();
        outputSelect.innerHTML = '';

        if (browserDevices.length > 0) {
            // Browser har fulla device labels
            browserDevices.forEach(d => {
                const opt = document.createElement('option');
                opt.value = d.id;
                opt.textContent = d.name;
                outputSelect.appendChild(opt);
            });
        } else {
            // Fallback: visa server-side enheter (enbart for display)
            serverOutputDevices.forEach(d => {
                const opt = document.createElement('option');
                opt.value = d.id;
                opt.textContent = d.name;
                outputSelect.appendChild(opt);
            });
        }
    }

    inputSelect.addEventListener('change', () => {
        send({ type: 'setInputDevice', deviceId: parseInt(inputSelect.value) });
    });

    outputSelect.addEventListener('change', () => {
        AudioPlayer.setOutputDevice(outputSelect.value);
    });

    document.getElementById('btn-settings').addEventListener('click', () => {
        settingsModal.classList.toggle('hidden');
    });

    document.getElementById('btn-close-settings').addEventListener('click', () => {
        settingsModal.classList.add('hidden');
    });

    settingsModal.addEventListener('click', (e) => {
        if (e.target === settingsModal) settingsModal.classList.add('hidden');
    });

    // Init
    FlowRenderer.init(send);
    KeyboardHandler.init(send);
    FlowRenderer.showConnecting();

    document.getElementById('btn-back-to-list').addEventListener('click', () => {
        FlowRenderer.showFlowList();
    });

    connect();
})();
