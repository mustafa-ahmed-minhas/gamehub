(function () {
    if (window.__gameHubFieldValidation) return;
    window.__gameHubFieldValidation = true;

    const patterns = {
        letters: /^[A-Za-z\s'\-]{2,}$/,
        org: /^[A-Za-z0-9\s&.,'()\-]{2,}$/,
        code: /^[A-Za-z0-9\-]{2,}$/,
        location: /^[A-Za-z\s.,\-]{2,}$/,
        tags: /^[A-Za-z0-9\s,#\-]*$/,
        phone: /^0\d{10}$/,
        email: /^[^\s@]+@[^\s@]+\.com$/i,
        int: /^\d{1,10}$/,
        decimal: /^\d{1,12}(\.\d{1,2})?$/
    };

    const defaults = {
        required: 'This field is required.',
        letters: 'Use letters, spaces, hyphens and apostrophes only.',
        org: 'Use letters, numbers, spaces and basic punctuation only.',
        code: 'Use letters, numbers and hyphens only.',
        location: 'Use letters, spaces, periods, commas and hyphens only.',
        tags: 'Use letters, numbers, spaces, commas, hashtags and hyphens only.',
        phone: 'Enter an 11-digit number starting with 0 (e.g. 03001234567).',
        email: 'Enter a valid email address ending in .com.',
        int: 'Enter a whole number.',
        decimal: 'Enter a valid amount (up to 2 decimals).',
        max: 'This value is too large.',
        min: 'This value is too small.',
        maxlength: 'This value is too long.',
        future: 'This date cannot be in the future.',
        past: 'This date cannot be in the past.',
        before: 'This date must be earlier.',
        after: 'This date must be later.',
        time: 'Enter a valid time.'
    };

    const isEmpty = (field) => {
        if (field.type === 'checkbox' || field.type === 'radio') return !field.checked;
        return !String(field.value || '').trim();
    };

    const toDate = (value) => {
        if (!value) return null;
        const date = value.length <= 10 ? new Date(`${value}T00:00:00`) : new Date(value);
        return Number.isNaN(date.getTime()) ? null : date;
    };

    const normalizePhone = (value) => String(value || '').replace(/[^\d]/g, '');

    const messageFor = (rule, field) => {
        const custom = rule === 'before' || rule === 'after' ? null : field.getAttribute(`data-fv-${rule}`);
        if (custom) return custom;
        if (rule === 'maxlength') return `Cannot exceed ${field.maxLength} characters.`;
        if (rule === 'max') return `Cannot be greater than ${field.getAttribute('data-fv-max')}.`;
        if (rule === 'min') return `Cannot be less than ${field.getAttribute('data-fv-min')}.`;
        if (rule === 'before') return `Must be earlier than ${field.getAttribute('data-fv-label') || 'the related date'}.`;
        if (rule === 'after') return `Must be later than ${field.getAttribute('data-fv-label') || 'the related date'}.`;
        return defaults[rule] || 'This value is not valid.';
    };

    const numeric = (field, attribute) => {
        const raw = field.getAttribute(attribute);
        if (raw === null || raw.trim() === '') return NaN;
        return Number(raw);
    };

    const runRule = (rule, field, form) => {
        const value = String(field.value || '').trim();
        const max = numeric(field, 'data-fv-max');
        const min = numeric(field, 'data-fv-min');

        switch (rule) {
            case 'required':
                return isEmpty(field);
            case 'letters':
            case 'org':
            case 'code':
            case 'location':
            case 'tags':
            case 'email':
                return !patterns[rule].test(value);
            case 'phone':
                return !patterns.phone.test(normalizePhone(value));
            case 'int':
                return !patterns.int.test(value) || (Number.isFinite(max) && Number(value) > max) || (Number.isFinite(min) && Number(value) < min);
            case 'decimal':
                return !patterns.decimal.test(value) || (Number.isFinite(max) && Number(value) > max) || (Number.isFinite(min) && Number(value) < min);
            case 'maxlength':
                return value.length > field.maxLength;
            case 'time': {
                if (!value) return false;
                const [hours, minutes] = value.split(':').map(Number);
                return !(Number.isInteger(hours) && Number.isInteger(minutes) && hours >= 0 && hours <= 23 && minutes >= 0 && minutes <= 59);
            }
            case 'past': {
                const date = toDate(value);
                return date != null && date < new Date(new Date().toDateString());
            }
            case 'future': {
                const date = toDate(value);
                return date != null && date > new Date(new Date().toDateString());
            }
            case 'before':
            case 'after': {
                const target = rule.includes(':') ? rule.split(':')[1].trim() : field.getAttribute(`data-fv-${rule}`);
                const other = target ? form.elements.namedItem(target) : null;
                const current = toDate(value);
                const otherDate = other instanceof HTMLElement ? toDate(other.value) : null;
                if (current == null || otherDate == null || !(other instanceof HTMLElement) || !other.value) return false;
                return rule.startsWith('before') ? current >= otherDate : current <= otherDate;
            }
            default:
                return false;
        }
    };

    const messageContainer = (field, form) => {
        const name = field.getAttribute('name');
        if (!name) return null;
        return form.querySelector(`[data-valmsg-for="${CSS.escape(name)}"]`) || document.querySelector(`[data-valmsg-for="${CSS.escape(name)}"]`);
    };

    const clear = (field, form) => {
        field.classList.remove('is-invalid');
        field.removeAttribute('aria-invalid');
        const container = messageContainer(field, form);
        if (container) { container.textContent = ''; container.classList.remove('field-validation-error'); }
    };

    const showError = (field, message, form) => {
        field.classList.add('is-invalid');
        field.setAttribute('aria-invalid', 'true');
        const container = messageContainer(field, form);
        if (container) { container.textContent = message; container.classList.add('field-validation-error'); }
        return message;
    };

    const validateField = (field, form) => {
        const rules = (field.getAttribute('data-fv-rule') || '').split(',').map((x) => x.trim()).filter(Boolean);
        if (!rules.length) return null;
        for (const rule of rules) {
            if (rule !== 'required' && isEmpty(field)) { clear(field, form); return null; }
            if (runRule(rule, field, form)) return showError(field, messageFor(rule, field), form);
        }
        clear(field, form);
        return null;
    };

    const toast = (message) => window.GameHubToasts?.show?.({ type: 'warning', title: 'Check the form', message }) || console.warn(message);

    const bind = (form) => {
        if (!form || form.dataset.fvBound === 'true') return;
        form.setAttribute('novalidate', 'novalidate');
        form.dataset.fvBound = 'true';

        const fields = Array.from(form.querySelectorAll('[data-fv-rule]')).filter((field) => !field.disabled && field.type !== 'hidden');

        fields.forEach((field) => {
            field.addEventListener('blur', () => validateField(field, form));
            field.addEventListener('input', () => {
                if (field.classList.contains('is-invalid')) validateField(field, form);
            });
            field.addEventListener('change', () => {
                if (field.classList.contains('is-invalid')) validateField(field, form);
            });
            if (field.getAttribute('data-fv-rule').includes('phone')) {
                field.addEventListener('input', () => {
                    const caretAtEnd = field.selectionStart === field.value.length;
                    const digits = normalizePhone(field.value);
                    const next = digits.length > 11 ? digits.slice(0, 11) : digits;
                    if (next !== field.value) {
                        field.value = next;
                        if (caretAtEnd) field.setSelectionRange(next.length, next.length);
                    }
                });
            }
        });

        form.addEventListener('submit', (event) => {
            let firstInvalid = null;
            let firstMessage = '';
            for (const field of fields) {
                const message = validateField(field, form);
                if (message && !firstInvalid) { firstInvalid = field; firstMessage = message; }
            }
            if (!firstInvalid) return;
            event.preventDefault();
            event.stopImmediatePropagation();
            firstInvalid.focus({ preventScroll: false });
            firstInvalid.scrollIntoView({ block: 'center', behavior: 'smooth' });
            toast(firstMessage);
        });
    };

    const bindAll = () => document.querySelectorAll('form[data-fv-form]').forEach(bind);

    window.GameHubFieldValidation = { bind, bindAll, patterns };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bindAll);
    else bindAll();
})();
