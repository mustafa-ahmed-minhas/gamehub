/* ==================================================================
   Home page Customer Inquiry client-side
   - Reuses field-validation.js patterns (letters, email, phone, int, past, time, maxlength, required)
   - Prevents duplicate submission, mirrors inline errors, and provides
     loading/success UI. Does NOT modify the ERP admin panel or call
     server APIs beyond the existing POST to /Inquiry.
   ================================================================== */

(function () {
    "use strict";

    const selectors = {
        form: "[data-home-inquiry-form]",
        summary: "[data-inquiry-summary]",
        summaryText: "[data-inquiry-summary-text]",
        submit: "[data-inquiry-submit]",
        success: "[data-inquiry-success]",
        ref: "[data-inquiry-ref]",
        refText: "[data-inquiry-reference]",
        reset: "[data-inquiry-reset]",
        panel: ".inquiry-panel",
        counterFor: "[data-inquiry-counter-for]",
        dateInput: "input[type='date']"
    };

    const form = document.querySelector(selectors.form);
    if (!form) {
        return;
    }

    const panel = form.closest(selectors.panel);
    const summary = form.querySelector(selectors.summary);
    const summaryText = form.querySelector(selectors.summaryText);
    const submitBtn = form.querySelector(selectors.submit);
    const successEl = document.querySelector(selectors.success);
    const refEl = document.querySelector(selectors.ref);
    const refText = document.querySelector(selectors.refText);
    const resetBtn = document.querySelector(selectors.reset);
    const dateInput = form.querySelector(selectors.dateInput);

    const submitting = () => {
        if (!submitBtn) {
            return;
        }
        submitBtn.disabled = true;
        submitBtn.setAttribute("aria-busy", "true");
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span> Submitting Inquiry';
    };

    const notSubmitting = () => {
        if (!submitBtn) {
            return;
        }
        submitBtn.disabled = false;
        submitBtn.removeAttribute("aria-busy");
        submitBtn.innerHTML = '<i class="bi bi-send" aria-hidden="true"></i> Submit Inquiry';
    };

    const clearSummary = () => {
        if (!summary) {
            return;
        }
        summary.hidden = true;
        if (summaryText) {
            summaryText.textContent = "";
        }
    };

    const showSummary = (message) => {
        if (!summary || !summaryText) {
            return;
        }
        summaryText.textContent = message;
        summary.hidden = false;
        window.setTimeout(() => {
            summary.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }, 50);
    };

    // Applies server-side validation errors to the matching inline fields so the
    // visitor sees exactly which inputs need attention.
    const applyServerErrors = (errors) => {
        if (!errors || typeof errors !== "object") {
            return null;
        }
        let firstInvalid = null;
        Object.entries(errors).forEach(([name, messages]) => {
            const field = form.querySelector(`[name="${CSS.escape(name)}"]`);
            const message = Array.isArray(messages) ? messages[0] : String(messages || "");
            if (!message) {
                return;
            }
            if (field instanceof HTMLElement) {
                field.classList.add("is-invalid");
                field.setAttribute("aria-invalid", "true");
                if (!firstInvalid) {
                    firstInvalid = field;
                }
            }
            const msg = form.querySelector(`[data-valmsg-for="${CSS.escape(name)}"]`);
            if (msg) {
                msg.textContent = message;
                msg.classList.add("field-validation-error");
            }
        });
        return firstInvalid;
    };

    const clearAllFieldErrors = () => {
        form.querySelectorAll(".is-invalid").forEach((field) => {
            field.classList.remove("is-invalid");
            field.removeAttribute("aria-invalid");
        });
        form.querySelectorAll("[data-valmsg-for]").forEach((msg) => {
            msg.textContent = "";
            msg.classList.remove("field-validation-error");
        });
    };

    const showSuccess = (reference) => {
        if (panel) {
            panel.classList.add("has-success");
        }
        if (successEl) {
            successEl.hidden = false;
            successEl.focus();
        }
        if (refEl && refText) {
            if (reference && reference.length > 0) {
                refText.textContent = reference;
                refEl.hidden = false;
            } else {
                refText.textContent = "";
                refEl.hidden = true;
            }
        }
        clearSummary();
    };

    const resetState = () => {
        if (panel) {
            panel.classList.remove("has-success");
        }
        if (successEl) {
            successEl.hidden = true;
        }
        form.reset();
        clearSummary();
        // Clear any inline field errors shown by field-validation
        form.querySelectorAll(".inquiry-field .form-control, .inquiry-field .form-select").forEach((field) => {
            field.classList.remove("is-invalid");
            field.removeAttribute("aria-invalid");
            const name = field.getAttribute("name");
            if (name) {
                const msg = form.querySelector(`[data-valmsg-for="${CSS.escape(name)}"]`);
                if (msg) {
                    msg.textContent = "";
                    msg.classList.remove("field-validation-error");
                }
            }
        });
        notSubmitting();
        const firstField = form.querySelector("input[name='FullName'], select[name='ServiceInterest'], input[name='InquirySubject']");
        if (firstField instanceof HTMLElement) {
            firstField.focus({ preventScroll: true });
        }
    };

    // Character counter for InquiryDetails
    document.querySelectorAll(selectors.counterFor).forEach((counter) => {
        const targetId = counter.getAttribute("data-inquiry-counter-for");
        if (!targetId) {
            return;
        }
        const target = form.querySelector(`#inquiry-details, textarea[name="${CSS.escape(targetId)}"], [name="${CSS.escape(targetId)}"]`);
        if (!target || !(target instanceof HTMLTextAreaElement)) {
            return;
        }
        const max = Number(target.maxLength || 0);
        const update = () => {
            counter.textContent = `${target.value.length} / ${max > 0 ? max : 2000} characters`;
        };
        target.addEventListener("input", update);
        update();
    });

    // Restrict preferred date to not in the past (mirror existing server rule)
    if (dateInput instanceof HTMLInputElement) {
        try {
            const today = new Date();
            today.setHours(0, 0, 0, 0);
            const iso = today.toISOString().split("T")[0];
            dateInput.min = iso;
        } catch {
            // Ignore if browser cannot format
        }
    }

    // Normalize and sanitize phone inputs (mirror server NormalizePhone)
    form.querySelectorAll("input[type='tel'][data-fv-rule*='phone']").forEach((input) => {
        const el = input;
        el.addEventListener("input", () => {
            const caretAtEnd = el.selectionStart === el.value.length;
            const digits = el.value.replace(/[^\d]/g, "");
            const prefix = el.value.trim().startsWith("+") && digits.length > 0 ? "+" : "";
            const next = prefix + digits;
            if (next !== el.value) {
                el.value = next.length > 20 ? next.slice(0, 20) : next;
                if (caretAtEnd && el.value.length > 0) {
                    el.setSelectionRange(el.value.length, el.value.length);
                }
            }
        });
    });

    // Handle AJAX submission to reuse existing server validation and response.
    // The form currently posts to /Inquiry; we intercept to show inline UI
    // without forcing a full page refresh. We still rely on server-side
    // validation and duplicate prevention.
    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        clearSummary();

        // Trigger field-validation.js logic by re-validating
        // and also perform a lightweight client-side gate.
        if (window.GameHubFieldValidation) {
            const fields = Array.from(form.querySelectorAll("[data-fv-rule]")).filter((field) => {
                if (field instanceof HTMLElement) {
                    return !field.hasAttribute("disabled") && field.getAttribute("type") !== "hidden";
                }
                return false;
            });
            let firstInvalid = null;
            let firstMessage = "";
            for (const field of fields) {
                const message = window.GameHubFieldValidation.validateField?.(field, form);
                if (message && !firstInvalid) {
                    firstInvalid = field;
                    firstMessage = message;
                }
            }
            if (firstInvalid) {
                firstInvalid.focus({ preventScroll: false });
                firstInvalid.scrollIntoView({ block: "center", behavior: "smooth" });
                if (window.GameHubToast?.show) {
                    window.GameHubToast.show({ type: "warning", title: "Check the form", message: firstMessage });
                } else {
                    showSummary(firstMessage);
                }
                return;
            }
        }

        // Basic cross-field gate: need email or phone
        const email = form.querySelector("input[name='Email']");
        const phone = form.querySelector("input[name='Phone']");
        const whatsapp = form.querySelector("input[name='WhatsAppNumber']");
        const emailVal = email instanceof HTMLInputElement ? email.value.trim() : "";
        const phoneVal = phone instanceof HTMLInputElement ? phone.value.replace(/[^\d]/g, "") : "";
        const whatsappVal = whatsapp instanceof HTMLInputElement ? whatsapp.value.replace(/[^\d]/g, "") : "";
        if (!emailVal && !phoneVal && !whatsappVal) {
            showSummary("Please provide an email address, phone number or WhatsApp number so we can reach you.");
            if (email instanceof HTMLElement) {
                email.focus();
                email.scrollIntoView({ block: "center", behavior: "smooth" });
            }
            return;
        }
        if (emailVal) {
            // simple inline guard (matches server LeadInquiryRules.HasValidComEmail)
            const emailRegex = /^[^\s@]+@[^\s@]+\.com$/i;
            if (!emailRegex.test(emailVal)) {
                showSummary("Enter a valid email address ending in .com.");
                if (email instanceof HTMLElement) {
                    email.focus();
                    email.scrollIntoView({ block: "center", behavior: "smooth" });
                }
                return;
            }
        }
        if (phoneVal) {
            if (!/^0\d{10}$/.test(phoneVal)) {
                showSummary("Enter an 11-digit phone number starting with 0 (e.g. 03001234567).");
                if (phone instanceof HTMLElement) {
                    phone.focus();
                    phone.scrollIntoView({ block: "center", behavior: "smooth" });
                }
                return;
            }
        }
        if (whatsappVal) {
            if (!/^0\d{10}$/.test(whatsappVal)) {
                showSummary("Enter an 11-digit WhatsApp number starting with 0 (e.g. 03001234567).");
                if (whatsapp instanceof HTMLElement) {
                    whatsapp.focus();
                    whatsapp.scrollIntoView({ block: "center", behavior: "smooth" });
                }
                return;
            }
        }

        // Prevent double submit
        if (submitBtn && submitBtn.disabled) {
            return;
        }

        clearAllFieldErrors();
        submitting();
        clearSummary();

        try {
            const formData = new FormData(form);
            // Include checkbox explicitly
            if (!(formData.get("AcceptPrivacyPolicy"))) {
                formData.set("AcceptPrivacyPolicy", "true");
            }

            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            // The server returns JSON for AJAX requests (detected via the
            // X-Requested-With header): { success, reference, message, errors }.
            let data = null;
            const contentType = response.headers.get("content-type") || "";
            if (contentType.includes("application/json")) {
                try {
                    data = await response.json();
                } catch {
                    data = null;
                }
            }

            if (response.ok && data && data.success) {
                if (window.GameHubToast?.show) {
                    window.GameHubToast.show({
                        type: "success",
                        title: "Inquiry Received",
                        message: data.reference ? `Reference: ${data.reference}` : (data.message || "Your inquiry has been submitted successfully.")
                    });
                }
                showSuccess(data.reference || "");
                return;
            }

            if (response.status === 400 || response.status === 422) {
                const firstInvalid = applyServerErrors(data && data.errors);
                const summaryMessage = (data && data.message) || "Please fix the highlighted fields and try again.";
                showSummary(summaryMessage);
                if (firstInvalid) {
                    firstInvalid.focus({ preventScroll: false });
                    firstInvalid.scrollIntoView({ block: "center", behavior: "smooth" });
                } else if (window.GameHubToast?.show) {
                    window.GameHubToast.show({ type: "warning", title: "Check the form", message: summaryMessage });
                }
            } else if (response.status === 409) {
                showSummary((data && data.message) || "A similar inquiry was submitted recently. Please wait a few minutes before submitting again.");
            } else if (response.status === 429) {
                showSummary((data && data.message) || "Too many inquiries from your network. Please wait a few minutes and try again.");
            } else if (data && data.message) {
                showSummary(data.message);
            } else {
                // Non-JSON or unexpected response (e.g. the app was restarting, or
                // the page/session went stale). Give an actionable next step.
                showSummary("We couldn't submit your inquiry right now. Please refresh the page and try again.");
            }
        } catch (error) {
            showSummary("We couldn't reach the server. Please check your connection, refresh the page, and try again.");
        } finally {
            notSubmitting();
        }
    });

    if (resetBtn) {
        resetBtn.addEventListener("click", (event) => {
            event.preventDefault();
            resetState();
        });
    }
})();
