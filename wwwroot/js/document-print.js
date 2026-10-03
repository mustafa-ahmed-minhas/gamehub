(function () {
    let isPrinting = false;
    let activeElement = null;
    let restoreTimer = null;

    const page = {
        widthMm: 210,
        heightMm: 297,
        marginMm: 8
    };

    function mmToPx(mm) {
        return (mm * 96) / 25.4;
    }

    function printableSizePx() {
        return {
            width: mmToPx(page.widthMm - page.marginMm * 2),
            height: mmToPx(page.heightMm - page.marginMm * 2)
        };
    }

    function calculateFitScale(element) {
        if (!element) return 1;

        restoreDocumentAfterPrint(false);

        const clone = element.cloneNode(true);
        clone.style.position = 'absolute';
        clone.style.left = '-10000px';
        clone.style.top = '0';
        clone.style.width = `${element.getBoundingClientRect().width}px`;
        clone.style.maxWidth = 'none';
        clone.style.transform = 'none';
        clone.style.visibility = 'hidden';
        clone.style.pointerEvents = 'none';
        document.body.appendChild(clone);

        const contentWidth = Math.ceil(clone.scrollWidth);
        const contentHeight = Math.ceil(clone.scrollHeight);
        clone.remove();

        const printable = printableSizePx();
        const scale = Math.min(printable.width / contentWidth, printable.height / contentHeight, 1);
        return Math.max(Number(scale.toFixed(4)), 0.25);
    }

    function prepareDocumentForPrint(elementId) {
        const element = document.getElementById(elementId);
        if (!element) return false;

        restoreDocumentAfterPrint(false);

        const rect = element.getBoundingClientRect();
        const sourceWidth = Math.ceil(rect.width);
        const sourceHeight = Math.ceil(element.scrollHeight);
        const scale = calculateFitScale(element);
        const printable = printableSizePx();

        document.documentElement.style.setProperty('--print-page-width', `${printable.width}px`);
        document.documentElement.style.setProperty('--print-page-height', `${printable.height}px`);
        document.documentElement.style.setProperty('--print-source-width', `${sourceWidth}px`);
        document.documentElement.style.setProperty('--print-source-height', `${sourceHeight}px`);
        document.documentElement.style.setProperty('--print-scaled-height', `${Math.ceil(sourceHeight * scale)}px`);
        document.documentElement.style.setProperty('--print-scale', scale);

        document.body.classList.add('print-document-ready');
        element.classList.add('is-print-target');
        activeElement = element;
        return true;
    }

    function restoreDocumentAfterPrint(clearFlag = true) {
        window.clearTimeout(restoreTimer);
        document.body.classList.remove('print-document-ready');
        activeElement?.classList.remove('is-print-target');
        activeElement = null;

        ['--print-page-width', '--print-page-height', '--print-source-width', '--print-source-height', '--print-scaled-height', '--print-scale'].forEach((name) => {
            document.documentElement.style.removeProperty(name);
        });

        if (clearFlag) isPrinting = false;
    }

    function runPrint(elementId) {
        if (isPrinting) return;
        if (!prepareDocumentForPrint(elementId)) return;

        isPrinting = true;
        window.setTimeout(() => {
            window.print();
            restoreTimer = window.setTimeout(() => restoreDocumentAfterPrint(), 1600);
        }, 80);
    }

    function printInvoice() {
        runPrint('invoiceDocument');
    }

    function printReceipt() {
        runPrint('receiptDocument');
    }

    document.querySelectorAll('[data-doc-print], [data-doc-download]').forEach((button) => {
        button.addEventListener('click', () => {
            const id = button.dataset.documentId || document.querySelector('[data-document-preview]')?.id;
            runPrint(id);
        });
    });

    document.querySelectorAll('[data-doc-share]').forEach((button) => {
        button.addEventListener('click', async () => {
            const title = document.title;
            const url = window.location.href;
            if (navigator.share) {
                try {
                    await navigator.share({ title, url });
                    return;
                } catch {
                    return;
                }
            }

            if (navigator.clipboard) {
                await navigator.clipboard.writeText(url);
                if (window.GameHubToast) {
                    window.GameHubToast.show({ type: 'success', title: 'Link Copied', message: 'Document link copied to clipboard.' });
                }
            }
        });
    });

    window.addEventListener('beforeprint', () => {
        if (!document.body.classList.contains('print-document-ready')) {
            const id = document.getElementById('invoiceDocument') ? 'invoiceDocument' : 'receiptDocument';
            prepareDocumentForPrint(id);
        }
    });

    window.addEventListener('afterprint', () => restoreDocumentAfterPrint());
    window.addEventListener('resize', () => {
        if (!isPrinting) restoreDocumentAfterPrint(false);
    });

    window.prepareDocumentForPrint = prepareDocumentForPrint;
    window.calculateFitScale = calculateFitScale;
    window.restoreDocumentAfterPrint = restoreDocumentAfterPrint;
    window.printInvoice = printInvoice;
    window.printReceipt = printReceipt;
})();
