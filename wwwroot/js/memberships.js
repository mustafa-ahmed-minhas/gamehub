// Membership module interactions: previews, dirty forms, and simple POST actions.
// Filter forms are handled by the shared gamehub-table.js handler (data-gamehub-table-filters).
(function () {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    document.querySelectorAll('[data-counter]').forEach((counter) => {
        const raw = counter.textContent.trim();
        const numeric = Number(raw.replace(/,/g, ''));
        if (!Number.isFinite(numeric) || reduceMotion) return;
        const duration = 720;
        const start = performance.now();
        const formatter = new Intl.NumberFormat();
        const step = (now) => {
            const progress = Math.min((now - start) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            counter.textContent = formatter.format(Math.round(numeric * eased));
            if (progress < 1) requestAnimationFrame(step);
            else counter.textContent = raw;
        };
        counter.textContent = '0';
        requestAnimationFrame(step);
    });

    document.querySelectorAll('[data-membership-form]').forEach((form) => {
        let dirty = false;
        form.addEventListener('input', () => { dirty = true; updatePreview(form); });
        form.addEventListener('change', () => { dirty = true; updatePreview(form); });
        form.addEventListener('submit', () => {
            dirty = false;
            const button = form.querySelector('[type="submit"]');
            if (button) {
                button.disabled = true;
                button.dataset.originalText = button.innerHTML;
                button.classList.add('is-loading');
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Saving...';
            }
        });
        window.addEventListener('beforeunload', (event) => {
            if (!dirty) return;
            event.preventDefault();
            event.returnValue = '';
        });
        updatePreview(form);
    });

    function value(form, name) {
        const field = form.querySelector(`[name="${name}"]`);
        if (!field) return '';
        if (field.type === 'checkbox') return field.checked ? 'Yes' : 'No';
        return field.options ? field.options[field.selectedIndex]?.text || '' : field.value;
    }

    function updatePreview(form) {
        form.querySelectorAll('[data-preview-for]').forEach((target) => {
            const names = target.dataset.previewFor.split(',');
            const text = names.map((name) => value(form, name.trim())).filter(Boolean).join(' ').trim();
            target.textContent = text || target.dataset.placeholder || 'Not provided';
        });
    }

    function antiforgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    async function postAction(url) {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'RequestVerificationToken': antiforgeryToken(), 'X-Requested-With': 'XMLHttpRequest' }
        });
        const result = await response.json().catch(() => ({ success: false, message: 'Action failed.' }));
        const type = result.success ? 'success' : 'error';
        const message = result.message || (result.success ? 'Action completed.' : 'Action failed.');
        window.GameHubToast?.show?.({ type, title: result.success ? 'Success' : 'Something Went Wrong', message });
        window.GameHubToasts?.show?.({ type, title: result.success ? 'Success' : 'Something Went Wrong', message });
        if (result.success) {
            window.setTimeout(() => window.location.reload(), 650);
        }
    }

    document.querySelectorAll('.js-membership-action,[data-membership-action]').forEach((button) => {
        button.addEventListener('click', () => {
            const message = button.dataset.message || 'Are you sure you want to continue?';
            if (window.confirm(message)) {
                button.disabled = true;
                button.classList.add('is-loading');
                postAction(button.dataset.url).finally(() => {
                    button.disabled = false;
                    button.classList.remove('is-loading');
                });
            }
        });
    });
})();
