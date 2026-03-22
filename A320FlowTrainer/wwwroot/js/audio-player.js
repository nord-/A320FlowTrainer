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
                // Om uppspelning misslyckas, prova TTS
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
        onComplete = null; // Forhindra dubbel-callback
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

    return { play };
})();
