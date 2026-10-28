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

        // Frontend input guards: mirror the server-side rules (11-digit phone
        // starting with 0, 13-digit numeric CNIC) as a first line of defence.
        const restrictToDigits = (input, maxLength) => {
            if (!input) return;
            input.addEventListener('input', () => {
                const cleaned = input.value.replace(/\D+/g, '').slice(0, maxLength);
                if (cleaned !== input.value) input.value = cleaned;
            });
        };
        restrictToDigits(qs('input[name="PrimaryPhone"]', form), 11);
        restrictToDigits(qs('input[name="WhatsAppNumber"]', form), 11);
        restrictToDigits(qs('input[name="NationalIdNumber"]', form), 13);

        let dirty = false;
        qsa('input, select, textarea', form).forEach((field) => field.addEventListener('change', () => dirty = true));

        const submitButton = qs('[data-loading-button]', form);
        const restoreSubmitButton = () => {
            if (!submitButton || !submitButton.disabled) return;
            submitButton.disabled = false;
            if (submitButton.dataset.originalText) {
                submitButton.innerHTML = submitButton.dataset.originalText;
            }
        };

        form.addEventListener('submit', () => {
            dirty = false;
            if (!submitButton) return;
            submitButton.disabled = true;
            submitButton.dataset.originalText = submitButton.innerHTML;
            submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Saving...';
        });

        // jQuery unobtrusive blocks an invalid submission in the same submit
        // event, so re-enable the button it was disabled in that cycle.
        document.addEventListener('DOMContentLoaded', () => {
            if (window.jQuery) window.jQuery(form).on('invalid-form', restoreSubmitButton);
        });
        window.addEventListener('beforeunload', (event) => {
            if (!dirty) return;
            event.preventDefault();
            event.returnValue = '';
        });
    }
})();
