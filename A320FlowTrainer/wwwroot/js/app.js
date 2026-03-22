(() => {
    let ws = null;

    function connect() {
        const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
        ws = new WebSocket(`${protocol}//${location.host}/ws`);

        ws.onopen = () => {
            document.querySelector('.connecting').textContent = 'Connected!';
            setTimeout(() => {
                const params = new URLSearchParams(location.search);
                send({ type: 'ready', testMode: params.has('test') });
            }, 300);
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
            document.querySelector('.connecting').textContent = 'Disconnected. Refreshing...';
            setTimeout(() => location.reload(), 2000);
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
                FlowRenderer.showWelcome();
                break;

            case 'showFlowActivation':
                FlowRenderer.showFlowActivation(msg);
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
                break;

            case 'allComplete':
                FlowRenderer.showComplete();
                break;
        }
    }

    // Init
    KeyboardHandler.init(send);
    FlowRenderer.showWelcome();
    connect();
})();
