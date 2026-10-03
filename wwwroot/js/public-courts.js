(function () {
    const nav = document.querySelector('[data-customer-nav]');
    const navToggle = nav?.querySelector('[data-nav-toggle]');
    if (nav && navToggle) {
        navToggle.addEventListener('click', () => {
            const isOpen = nav.classList.toggle('open');
            navToggle.setAttribute('aria-expanded', isOpen.toString());
            const icon = navToggle.querySelector('i');
            if (!icon) return;
            icon.classList.toggle('bi-list', !isOpen);
            icon.classList.toggle('bi-x-lg', isOpen);
        });
    }

    document.querySelector('[data-phase-toast]')?.addEventListener('click', () => {
        if (window.GameHubToast) {
            window.GameHubToast.show({ type: 'info', title: 'Coming Next', message: 'Online booking creation and payment will be enabled in Phase 2.' });
            return;
        }
        alert('Online booking creation and payment will be enabled in Phase 2.');
    });

    document.querySelectorAll('[data-public-filters] select').forEach((select) => {
        select.addEventListener('change', () => select.form?.submit());
    });
})();
