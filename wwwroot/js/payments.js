(() => {
    const invoice = document.querySelector('[data-payment-invoice]');
    if (!invoice) return;
    invoice.addEventListener('change', () => {
        const url = new URL(window.location.href);
        if (invoice.value) url.searchParams.set('invoiceId', invoice.value);
        else url.searchParams.delete('invoiceId');
        window.location.href = url.toString();
    });
})();
