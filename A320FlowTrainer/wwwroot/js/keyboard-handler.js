const KeyboardHandler = (() => {
    let sendFn = null;

    const keyMap = {
        'Enter': 'enter',
        'Escape': 'escape',
        ' ': 'space',
        'r': 'r',
        'R': 'r',
        'n': 'n',
        'N': 'n',
        'Tab': 'tab',
    };

    function init(send) {
        sendFn = send;
        document.addEventListener('keydown', onKeyDown);
    }

    function onKeyDown(e) {
        const mapped = keyMap[e.key];
        if (mapped && sendFn) {
            e.preventDefault();
            sendFn({ type: 'keypress', key: mapped });
        }
    }

    return { init };
})();
