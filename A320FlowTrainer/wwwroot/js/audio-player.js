const AudioPlayer = (() => {
    const audio = new Audio();
    let onComplete = null;

    audio.addEventListener('ended', () => {
        if (onComplete) onComplete();
    });

    audio.addEventListener('error', () => {
        if (onComplete) onComplete();
    });

    function play(url, fallbackText, completeCb) {
        onComplete = completeCb;

        if (url) {
            audio.src = url;
            audio.play().catch(() => {
                if (fallbackText) {
                    speakText(fallbackText, completeCb);
                } else if (onComplete) {
                    onComplete();
                }
            });
        } else if (fallbackText) {
            speakText(fallbackText, completeCb);
        } else if (onComplete) {
            onComplete();
        }
    }

    function speakText(text, completeCb) {
        onComplete = null;
        if ('speechSynthesis' in window) {
            const utterance = new SpeechSynthesisUtterance(text);
            utterance.rate = 1.0;
            utterance.onend = () => { if (completeCb) completeCb(); };
            utterance.onerror = () => { if (completeCb) completeCb(); };
            speechSynthesis.speak(utterance);
        } else if (completeCb) {
            completeCb();
        }
    }

    async function setOutputDevice(deviceId) {
        if (audio.setSinkId) {
            try {
                await audio.setSinkId(deviceId);
            } catch (e) {
                console.warn('Could not set output device:', e);
            }
        }
    }

    async function getOutputDevices() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
            return [];
        }
        try {
            const devices = await navigator.mediaDevices.enumerateDevices();
            return devices
                .filter(d => d.kind === 'audiooutput')
                .map(d => ({ id: d.deviceId, name: d.label || `Speaker ${d.deviceId.slice(0, 8)}` }));
        } catch {
            return [];
        }
    }

    return { play, setOutputDevice, getOutputDevices };
})();
