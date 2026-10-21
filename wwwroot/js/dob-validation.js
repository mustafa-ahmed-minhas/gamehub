// Date-of-Birth minimum-age validation (16+), shared by every form that collects
// a date of birth. Mirrors GameHub.Helpers.DateOfBirthRules on the server:
// it rejects future and impossible dates, and anyone below the minimum age.
//
// Mark a date input with `data-dob` to opt it in. Optional overrides:
//   data-dob-min-age="18"                       -> stricter module rule (HR)
//   data-dob-message="Employee must be ..."     -> message for that stricter rule
(function () {
    if (window.GameHubDobValidation) return;
    window.GameHubDobValidation = true;

    const DEFAULT_MIN_AGE = 16;
    const MAX_AGE_YEARS = 120;

    const MESSAGES = {
        future: "Date of birth cannot be in the future.",
        invalid: "Please enter a valid date of birth.",
        underage: "You are underage. You must be at least 16 years old to register."
    };

    const today = () => {
        const now = new Date();
        return new Date(now.getFullYear(), now.getMonth(), now.getDate());
    };

    const parse = (value) => {
        const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value || "");
        if (!match) return null;
        const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
        return Number.isNaN(date.getTime()) ? null : date;
    };

    const ageOn = (dob, reference) => {
        let age = reference.getFullYear() - dob.getFullYear();
        const monthDelta = reference.getMonth() - dob.getMonth();
        if (monthDelta < 0 || (monthDelta === 0 && reference.getDate() < dob.getDate())) age--;
        return age;
    };

    const messageFor = (field) => {
        if (!String(field.value || "").trim()) return "";
        const dob = parse(field.value);
        if (!dob) return MESSAGES.invalid;
        const reference = today();
        if (dob > reference) return MESSAGES.future;
        const age = ageOn(dob, reference);
        if (age > MAX_AGE_YEARS) return MESSAGES.invalid;
        const minAge = Number(field.dataset.dobMinAge || DEFAULT_MIN_AGE);
        if (age < minAge) return field.dataset.dobMessage || MESSAGES.underage;
        return "";
    };

    const containerFor = (field) => {
        const name = field.getAttribute("name");
        if (!name) return null;
        return document.querySelector(`[data-valmsg-for="${CSS.escape(name)}"]`);
    };

    const show = (field, message) => {
        field.classList.add("is-invalid");
        field.setAttribute("aria-invalid", "true");
        const container = containerFor(field);
        if (container) {
            container.textContent = message;
            container.classList.add("field-validation-error");
        }
    };

    const clear = (field) => {
        field.classList.remove("is-invalid");
        field.removeAttribute("aria-invalid");
        const container = containerFor(field);
        if (container && container.textContent.trim()) {
            container.textContent = "";
            container.classList.remove("field-validation-error");
        }
    };

    const validate = (field) => {
        const message = messageFor(field);
        if (message) {
            show(field, message);
            return message;
        }
        clear(field);
        return "";
    };

    const bind = (field) => {
        if (field.dataset.dobBound === "true") return;
        field.dataset.dobBound = "true";
        field.addEventListener("blur", () => validate(field));
        field.addEventListener("change", () => validate(field));
        field.addEventListener("input", () => validate(field));
    };

    const init = () => document.querySelectorAll('input[type="date"][data-dob]').forEach(bind);

    // Captured at the document so it runs before any form-level handler
    // (field-validation.js, users.js, jQuery unobtrusive) and can stop a request
    // that carries an under-age date of birth.
    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        const fields = form.querySelectorAll('input[type="date"][data-dob]');
        if (!fields.length) return;
        for (const field of fields) {
            if (validate(field)) {
                event.preventDefault();
                event.stopImmediatePropagation();
                field.focus();
                return;
            }
        }
    }, true);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
