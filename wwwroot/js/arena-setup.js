// Arena Setup interactions: filters, live previews, dirty forms, status toggles.
(function () {
    const debounce = (fn, wait = 420) => {
        let id;
        return (...args) => {
            clearTimeout(id);
            id = setTimeout(() => fn(...args), wait);
        };
    };

    const filterForm = document.querySelector("[data-arena-filter-form]");
    filterForm?.querySelector("[data-arena-search]")?.addEventListener("input", debounce(() => filterForm.requestSubmit()));
    filterForm?.querySelectorAll("[data-arena-auto]").forEach((item) => item.addEventListener("change", () => filterForm.requestSubmit()));

    const formPage = document.querySelector("[data-arena-form]");
    if (formPage) {
        const form = formPage.querySelector("form");
        const save = formPage.querySelector("[data-arena-save]");
        const saveBar = formPage.querySelector(".arena-save-bar");
        const initial = new URLSearchParams(new FormData(form)).toString();
        let submitting = false;
        const val = (selector) => formPage.querySelector(selector)?.value || "";
        const checked = (selector) => formPage.querySelector(selector)?.checked;
        const text = (selector, value) => { const el = formPage.querySelector(selector); if (el) el.textContent = value; };
        const selected = (selector) => formPage.querySelector(selector)?.selectedOptions?.[0]?.text || "";
        const update = () => {
            text("[data-preview-name-target]", val("[data-preview-name]") || "Name");
            text("[data-preview-code-target]", val("[data-preview-code]") || "CODE");
            text("[data-preview-duration-target]", `${val("[data-preview-duration]") || 60} min`);
            text("[data-preview-type-target]", selected("[data-preview-type]"));
            text("[data-preview-location-target]", val("[data-preview-location]") || "Location not set");
            text("[data-preview-sport-target]", selected("[data-preview-sport]") || "Sport");
            text("[data-preview-facility-target]", selected("[data-preview-facility]") || "Facility");
            text("[data-preview-capacity-target]", `${val("[data-preview-capacity]") || 0} players`);
            text("[data-preview-court-target]", selected("[data-preview-court]") || "Court");
            text("[data-preview-day-target]", selected("[data-preview-day]") || "All Days");
            text("[data-preview-time-target]", `${val("[data-preview-start]") || val("[data-preview-open]") || "--:--"} - ${val("[data-preview-end]") || val("[data-preview-close]") || "--:--"}`);
            text("[data-preview-price-target]", `PKR ${Number(val("[data-preview-price]") || 0).toLocaleString()}`);
            text("[data-preview-peak-target]", checked("[data-preview-peak]") ? "Peak" : "Standard");
            text("[data-preview-buffer-target]", `${val("[data-preview-buffer]") || 0} min buffer`);
            text("[data-preview-closed-target]", checked("[data-preview-closed]") ? "Closed" : "Open");
            text("[data-preview-active-target]", checked("[data-preview-active]") === false ? "Inactive" : "Active");
            const icon = formPage.querySelector("[data-preview-icon-target]");
            const iconInput = val("[data-preview-icon]");
            if (icon && iconInput) icon.className = `bi ${iconInput}`;
            if (icon) icon.style.setProperty("--accent", val("[data-preview-accent]") || "#d6a950");
            saveBar?.classList.toggle("is-dirty", new URLSearchParams(new FormData(form)).toString() !== initial);
        };
        form?.addEventListener("input", update);
        form?.addEventListener("change", update);

        // Client-side validation mirrors the server-side rules for immediate feedback.
        const rulePatterns = {
            letters: { re: /^[A-Za-z ]+$/, message: "Only letters and spaces are allowed." },
            title: { re: /^[A-Za-z0-9 \-]+$/, message: "Only letters, numbers, spaces and hyphens are allowed." },
            code: { re: /^[A-Za-z0-9\-]+$/, message: "Only letters, numbers and hyphens are allowed." },
            location: { re: /^[A-Za-z0-9 \-/#.]+$/, message: "Only letters, numbers, spaces and - / # . are allowed." },
            int: { re: /^\d+$/, message: "Only whole numbers are allowed." },
            decimal: { re: /^\d+(\.\d{1,2})?$/, message: "Enter a valid amount with up to 2 decimals." }
        };

        const sanitizers = {
            letters: (v) => v.replace(/[^A-Za-z ]/g, ""),
            title: (v) => v.replace(/[^A-Za-z0-9 \-]/g, ""),
            code: (v) => v.replace(/[^A-Za-z0-9\-]/g, "").toUpperCase(),
            location: (v) => v.replace(/[^A-Za-z0-9 \-/#.]/g, ""),
            int: (v) => v.replace(/[^\d]/g, "")
        };

        const messageElement = (input) => {
            const holder = input.closest("label") || input.parentElement;
            if (!holder) return null;
            let message = holder.querySelector(`[data-valmsg-for='${input.name}']`);
            if (!message) {
                message = holder.querySelector(".arena-field-error");
                if (!message) {
                    message = document.createElement("small");
                    message.className = "arena-field-error";
                    holder.appendChild(message);
                }
            }
            return message;
        };

        const setFieldError = (input, text) => {
            input.classList.toggle("is-invalid", Boolean(text));
            const message = messageElement(input);
            if (message) message.textContent = text || "";
        };

        const validateInput = (input) => {
            const value = (input.value || "").trim();
            if (input.dataset.arenaRequired === "true" && !value) {
                setFieldError(input, "This field is required.");
                return false;
            }
            if (!value) {
                setFieldError(input, "");
                return true;
            }
            const rule = rulePatterns[input.dataset.arenaRule];
            if (rule && !rule.re.test(value)) {
                setFieldError(input, rule.message);
                return false;
            }
            const min = input.dataset.arenaMin;
            const max = input.dataset.arenaMax;
            if ((min !== undefined || max !== undefined) && /^\d+(\.\d{1,2})?$/.test(value)) {
                const numeric = Number(value);
                if (min !== undefined && numeric < Number(min)) {
                    setFieldError(input, `Must be at least ${min}.`);
                    return false;
                }
                if (max !== undefined && numeric > Number(max)) {
                    setFieldError(input, `Must be at most ${max}.`);
                    return false;
                }
            }
            setFieldError(input, "");
            return true;
        };

        const validateOrder = (input) => {
            const otherName = input.dataset.arenaBefore;
            if (!otherName) return true;
            const condition = input.dataset.arenaWhenUnchecked;
            if (condition) {
                const checkbox = form.querySelector(`[name='${condition}']`);
                if (checkbox && checkbox.checked) {
                    setFieldError(input, "");
                    return true;
                }
            }
            const other = form.querySelector(`[name='${otherName}']`);
            if (input.value && other?.value && input.value >= other.value) {
                setFieldError(input, "Start time must be before end time.");
                return false;
            }
            setFieldError(input, "");
            return true;
        };

        const validatedInputs = Array.from(form.querySelectorAll("[data-arena-rule],[data-arena-required],[data-arena-before]"));
        validatedInputs.forEach((input) => {
            const sanitize = sanitizers[input.dataset.arenaRule];
            if (sanitize) {
                input.addEventListener("input", () => {
                    const cleaned = sanitize(input.value);
                    if (cleaned !== input.value) input.value = cleaned;
                });
            }
        });

        const refresh = (input) => {
            if (input.matches("[data-arena-rule],[data-arena-required]")) validateInput(input);
            if (input.dataset.arenaBefore) validateOrder(input);
            form.querySelectorAll("[data-arena-before]").forEach((other) => {
                if (other.dataset.arenaBefore === input.name) validateOrder(other);
            });
        };

        validatedInputs.forEach((input) => {
            input.addEventListener("input", () => refresh(input));
            input.addEventListener("change", () => refresh(input));
        });

        form?.addEventListener("submit", (event) => {
            if (submitting) {
                event.preventDefault();
                return;
            }
            let firstInvalid = null;
            validatedInputs.forEach((input) => {
                const valid = validateInput(input) && validateOrder(input);
                if (!valid && !firstInvalid) firstInvalid = input;
            });
            if (firstInvalid) {
                event.preventDefault();
                firstInvalid.focus();
                window.GameHubToast?.show({ type: "error", title: "Please review the form", message: "Fix the highlighted fields before saving." });
                return;
            }
            submitting = true;
            if (save) {
                save.disabled = true;
                save.querySelector("span").textContent = "Saving...";
            }
        });
        window.addEventListener("beforeunload", (event) => {
            if (!submitting && new URLSearchParams(new FormData(form)).toString() !== initial) {
                event.preventDefault();
                event.returnValue = "";
            }
        });
        update();
    }

    const modalEl = document.getElementById("arenaConfirmModal");
    if (modalEl && window.bootstrap) {
        const modal = new bootstrap.Modal(modalEl);
        let target = null;
        document.querySelectorAll("[data-arena-toggle]").forEach((button) => {
            button.addEventListener("click", () => {
                target = button;
                modalEl.querySelector("[data-arena-confirm-message]").textContent = button.dataset.active === "true" ? "This record will be deactivated." : "This record will be activated.";
                modal.show();
            });
        });
        modalEl.querySelector("[data-arena-confirm]")?.addEventListener("click", async () => {
            if (!target) return;
            try {
                const token = document.querySelector("#arenaStatusToken input[name='__RequestVerificationToken']")?.value || "";
                const response = await fetch(target.dataset.url, { method: "POST", headers: { "RequestVerificationToken": token, "Accept": "application/json" } });
                const result = await response.json();
                window.GameHubToast?.show({ type: result.success ? "success" : "error", title: result.success ? "Success" : "Something Went Wrong", message: result.message || "Status updated." });
                if (result.success) setTimeout(() => location.reload(), 650);
            } catch {
                window.GameHubToast?.show({ type: "error", title: "Something Went Wrong", message: "Status update failed." });
            } finally {
                modal.hide();
            }
        });
    }
})();
