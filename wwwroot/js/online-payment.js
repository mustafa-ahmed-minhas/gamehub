(function () {
    const form = document.querySelector('[data-payment-form]');
    if (!form) return;

    const panels = document.querySelectorAll('[data-method-panel]');
    const radios = document.querySelectorAll('input[name="PaymentMethod"]');
    const cardNumber = document.querySelector('[data-card-number]');
    const cardHolder = document.querySelector('[data-card-holder]');
    const month = document.querySelector('[data-expiry-month]');
    const year = document.querySelector('[data-expiry-year]');
    const cvv = document.querySelector('[data-cvv]');
    const previewNumber = document.querySelector('[data-preview-number]');
    const previewHolder = document.querySelector('[data-preview-holder]');
    const previewExpiry = document.querySelector('[data-preview-expiry]');
    const previewBrand = document.querySelector('[data-card-brand]');
    const brandBadge = document.querySelector('[data-card-brand-badge]');
    const numberFeedback = document.querySelector('[data-card-number-feedback]');
    const holderFeedback = document.querySelector('[data-holder-feedback]');
    const cvvFeedback = document.querySelector('[data-cvv-feedback]');
    const expiryFeedback = document.querySelector('[data-expiry-feedback]');

    const brandMeta = {
        visa: { label: 'Visa', icon: 'bi-credit-card-2-front-fill' },
        mastercard: { label: 'Mastercard', icon: 'bi-credit-card-fill' },
        amex: { label: 'Amex', icon: 'bi-credit-card-2-back-fill' },
        generic: { label: 'Card', icon: 'bi-credit-card-2-front' }
    };

    function digits(value) {
        return (value || '').replace(/\D/g, '');
    }

    function detectBrand(value) {
        const clean = digits(value);
        if (/^4/.test(clean)) return 'visa';
        if (/^(5[1-5]|2[2-7])/.test(clean)) return 'mastercard';
        if (/^3[47]/.test(clean)) return 'amex';
        return 'generic';
    }

    function formatCardNumber(value) {
        const clean = digits(value).slice(0, 16);
        return clean.replace(/(.{4})/g, '$1 ').trim();
    }

    function setFieldState(input, isValid, feedback, message) {
        const field = input?.closest('.payment-field');
        if (!field) return;
        field.classList.toggle('is-valid', Boolean(isValid));
        field.classList.toggle('is-invalid', isValid === false);
        if (feedback && message) feedback.textContent = message;
    }

    function updateCardPreview() {
        const cleanNumber = digits(cardNumber?.value);
        const formatted = cardNumber?.value || '';
        const brand = detectBrand(cleanNumber);
        const meta = brandMeta[brand];
        const holder = (cardHolder?.value || '').trim().toUpperCase();
        const expMonth = month?.value || 'MM';
        const expYear = year?.value || 'YYYY';

        if (previewNumber) previewNumber.textContent = formatted || '•••• •••• •••• ••••';
        if (previewHolder) previewHolder.textContent = holder || 'YOUR NAME';
        if (previewExpiry) previewExpiry.textContent = `${expMonth}/${expYear}`;
        if (previewBrand) previewBrand.textContent = meta.label;
        if (brandBadge) brandBadge.innerHTML = `<i class="bi ${meta.icon}"></i>`;

        if (cvv) {
            const max = brand === 'amex' ? 4 : 3;
            cvv.maxLength = max;
            if (cvv.value.length > max) cvv.value = cvv.value.slice(0, max);
        }
    }

    function validateCardNumber() {
        if (!cardNumber) return true;
        const clean = digits(cardNumber.value);
        const valid = clean.length === 16;
        if (!clean.length) {
            setFieldState(cardNumber, null, numberFeedback, 'Enter a valid card number.');
        } else {
            setFieldState(cardNumber, valid, numberFeedback, valid ? 'Card number looks valid.' : 'Card number must be 16 digits for this demo gateway.');
        }
        return valid;
    }

    function validateHolder() {
        if (!cardHolder) return true;
        const value = cardHolder.value.trim();
        const valid = value.length > 1 && /^[A-Za-z\s'.-]+$/.test(value);
        if (!value.length) {
            setFieldState(cardHolder, null, holderFeedback, 'Letters and spaces only.');
        } else {
            setFieldState(cardHolder, valid, holderFeedback, valid ? 'Card holder name looks good.' : 'Numbers are not allowed in card holder name.');
        }
        return valid;
    }

    function validateExpiry() {
        if (!month || !year || !expiryFeedback) return true;
        const selectedMonth = Number(month.value);
        const selectedYear = Number(year.value);
        expiryFeedback.classList.remove('is-valid', 'is-invalid');
        if (!selectedMonth || !selectedYear) {
            expiryFeedback.textContent = '';
            return true;
        }

        const now = new Date();
        const expiry = new Date(selectedYear, selectedMonth, 0, 23, 59, 59);
        const valid = expiry >= now;
        expiryFeedback.classList.add(valid ? 'is-valid' : 'is-invalid');
        expiryFeedback.textContent = valid ? 'Expiry date is valid.' : 'This card expiry date has passed.';
        return valid;
    }

    function validateCvv() {
        if (!cvv) return true;
        const clean = digits(cvv.value);
        const brand = detectBrand(cardNumber?.value);
        const required = brand === 'amex' ? 4 : 3;
        const valid = clean.length === required;
        if (!clean.length) {
            setFieldState(cvv, null, cvvFeedback, 'Numbers only.');
        } else {
            setFieldState(cvv, valid, cvvFeedback, valid ? 'Security code accepted.' : `CVV must be ${required} digits.`);
        }
        return valid;
    }

    function showPanel() {
        const value = document.querySelector('input[name="PaymentMethod"]:checked')?.value || 'Card';
        panels.forEach((panel) => panel.classList.toggle('d-none', panel.dataset.methodPanel !== value));
    }

    radios.forEach((radio) => radio.addEventListener('change', showPanel));
    showPanel();

    cardNumber?.addEventListener('input', () => {
        cardNumber.value = formatCardNumber(cardNumber.value);
        updateCardPreview();
        validateCardNumber();
        validateCvv();
    });

    cardHolder?.addEventListener('input', () => {
        cardHolder.value = cardHolder.value.replace(/[0-9]/g, '');
        updateCardPreview();
        validateHolder();
    });

    [month, year].forEach((input) => input?.addEventListener('change', () => {
        updateCardPreview();
        validateExpiry();
    }));

    cvv?.addEventListener('input', () => {
        cvv.value = digits(cvv.value).slice(0, cvv.maxLength || 3);
        validateCvv();
    });

    document.querySelectorAll('.btn-premium, .btn-ghost-light').forEach((button) => {
        button.addEventListener('click', (event) => {
            const ripple = document.createElement('span');
            ripple.className = 'button-ripple';
            const rect = button.getBoundingClientRect();
            ripple.style.left = `${event.clientX - rect.left}px`;
            ripple.style.top = `${event.clientY - rect.top}px`;
            button.appendChild(ripple);
            window.setTimeout(() => ripple.remove(), 650);
        });
    });

    form.addEventListener('submit', (event) => {
        if (expired) {
            event.preventDefault();
            expireSession();
            return;
        }
        const selectedMethod = document.querySelector('input[name="PaymentMethod"]:checked')?.value || 'Card';
        if (selectedMethod === 'Card') {
            const valid = validateHolder() & validateCardNumber() & validateExpiry() & validateCvv();
            if (!valid) {
                event.preventDefault();
                return;
            }
        }

        const submit = document.querySelector('[data-payment-submit]');
        if (submit) {
            submit.setAttribute('disabled', 'disabled');
            submit.innerHTML = '<span class="spinner-border spinner-border-sm"></span><span>Processing Secure Payment</span>';
        }

        const overlay = document.querySelector('[data-processing-overlay]');
        overlay?.classList.add('show');
        const steps = ['Verifying payment', 'Rechecking court availability', 'Confirming reservation', 'Preparing receipt'];
        let index = 0;
        window.setInterval(() => {
            const el = document.querySelector('[data-processing-step]');
            if (el) el.textContent = steps[Math.min(++index, steps.length - 1)];
        }, 900);
    });

    const countdown = document.querySelector('[data-payment-countdown]');
    const submit = document.querySelector('[data-payment-submit]');
    let left = Number(countdown?.dataset.paymentCountdown || 0);
    let expired = false;

    function expireSession() {
        if (countdown) {
            countdown.textContent = 'Expired';
            countdown.classList.add('is-expired');
        }
        if (submit) {
            submit.setAttribute('disabled', 'disabled');
            submit.classList.add('is-expired');
            submit.innerHTML = '<span>Session Expired</span>';
        }
    }

    function tick() {
        if (!countdown || expired) return;
        if (left <= 0) {
            expired = true;
            expireSession();
            return;
        }
        countdown.textContent = `${Math.floor(left / 60)}:${String(left % 60).padStart(2, '0')} remaining`;
        left -= 1;
        window.setTimeout(tick, 1000);
    }

    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((item) => {
        if (window.bootstrap?.Tooltip) new window.bootstrap.Tooltip(item);
    });

    updateCardPreview();
    tick();
})();
