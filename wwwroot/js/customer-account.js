(function () {
    const qs = (selector, root = document) => root.querySelector(selector);
    const qsa = (selector, root = document) => Array.from(root.querySelectorAll(selector));

    const passwordInput = qs('[data-password-input]');
    const confirmInput = qs('[data-confirm-password]');
    const meter = qs('[data-password-meter]');
    const meterText = qs('[data-password-text]');

    function scorePassword(value) {
        let score = 0;
        if (value.length >= 8) score++;
        if (/[A-Z]/.test(value)) score++;
        if (/[a-z]/.test(value)) score++;
        if (/\d/.test(value)) score++;
        if (/[^A-Za-z0-9]/.test(value)) score++;
        return score;
    }

    function updatePasswordUi() {
        if (!passwordInput || !meter) return;
        const score = scorePassword(passwordInput.value);
        meter.classList.remove('weak', 'medium', 'strong');
        meter.classList.add(score >= 5 ? 'strong' : score >= 3 ? 'medium' : 'weak');
        if (meterText) {
            const match = confirmInput && confirmInput.value ? (confirmInput.value === passwordInput.value ? ' Passwords match.' : ' Passwords do not match.') : '';
            meterText.textContent = `${score >= 5 ? 'Strong' : score >= 3 ? 'Medium' : 'Weak'} password.${match}`;
        }
    }

    passwordInput?.addEventListener('input', updatePasswordUi);
    confirmInput?.addEventListener('input', updatePasswordUi);

    function debounce(callback, delay = 450) {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => callback(...args), delay);
        };
    }

    async function checkAvailability(input, output, url, key) {
        if (!input || !output || !url || !input.value.trim()) return;
        output.textContent = 'Checking...';
        output.className = 'field-hint';
        try {
            const response = await fetch(`${url}?${key}=${encodeURIComponent(input.value.trim())}`);
            const data = await response.json();
            output.textContent = data.message || 'Checked.';
            output.classList.add(data.available ? 'good' : 'bad');
        } catch {
            output.textContent = 'Could not check right now. We will validate when you submit.';
        }
    }

    const form = qs('[data-account-form]');
    if (form) {
        const emailInput = qs('[data-check-email]', form);
        const phoneInput = qs('[data-check-phone]', form);
        const emailResult = qs('[data-email-result]', form);
        const phoneResult = qs('[data-phone-result]', form);
        const emailUrl = form.dataset.emailCheckUrl;
        const phoneUrl = form.dataset.phoneCheckUrl;
        emailInput?.addEventListener('input', debounce(() => checkAvailability(emailInput, emailResult, emailUrl, 'email')));
        emailInput?.addEventListener('blur', () => checkAvailability(emailInput, emailResult, emailUrl, 'email'));
        phoneInput?.addEventListener('input', debounce(() => checkAvailability(phoneInput, phoneResult, phoneUrl, 'phone')));
        phoneInput?.addEventListener('blur', () => checkAvailability(phoneInput, phoneResult, phoneUrl, 'phone'));

        let dirty = false;
        qsa('input, select, textarea', form).forEach((field) => field.addEventListener('change', () => dirty = true));
        form.addEventListener('submit', () => {
            dirty = false;
            const button = qs('[data-loading-button]', form);
            if (button) {
                button.disabled = true;
                button.dataset.originalText = button.innerHTML;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Saving...';
            }
        });
        window.addEventListener('beforeunload', (event) => {
            if (!dirty) return;
            event.preventDefault();
            event.returnValue = '';
        });
    }
})();
