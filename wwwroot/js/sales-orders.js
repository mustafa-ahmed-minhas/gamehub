(function () {
    // Order form behaviour only. Search/filter toolbars use the shared
    // gamehub-table.js handler (data-gamehub-table-filters) or sales.js
    // (data-sales-search / data-sales-filter / filter drawer), and the guidance
    // rail collapse is owned by sales.js so there is a single owner of
    // [data-sales-guidance-toggle].
    document.querySelectorAll('[data-sales-order-form]').forEach((form) => {
        let dirty = false;
        form.addEventListener('input', () => { dirty = true; });
        form.addEventListener('change', () => { dirty = true; });
        form.addEventListener('submit', () => {
            dirty = false;
            form.querySelectorAll('[data-loading-button]').forEach((button) => {
                button.disabled = true;
                button.dataset.originalText = button.innerHTML;
                button.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Saving...';
            });
        });
        window.addEventListener('beforeunload', (event) => {
            if (!dirty) return;
            event.preventDefault();
            event.returnValue = '';
        });
    });
})();
