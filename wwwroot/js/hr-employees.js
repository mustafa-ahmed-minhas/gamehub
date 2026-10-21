/* ==========================================================================
   HR Module — Employee Management (Screen 2)
   Client-side helpers on top of jQuery Validation Unobtrusive:
     - index: live search / filter navigation, employment-status change modal
     - form: employee number + avatar preview, profile-photo preview,
             uniqueness checks (email / phone / CNIC), character counters
   All checks are re-validated on the server; these helpers only improve UX.
   ========================================================================== */
(() => {
    if (window.__gameHubEmployeesInitialized) return;
    window.__gameHubEmployeesInitialized = true;

    const debounce = (fn, delay = 400) => {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), delay);
        };
    };

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    const toast = (type, title, message) => window.GameHubToasts?.show?.({ type, title, message }) || alert(message);

    /* ------------------------------------------------------------------
       Index — search + filter auto-navigation
       ------------------------------------------------------------------ */
    document.querySelectorAll("[data-employee-filters]").forEach(form => {
        const action = form.getAttribute("action") || window.location.pathname;
        const navigate = () => {
            const data = new FormData(form);
            data.delete("page");
            const params = new URLSearchParams();
            for (const [key, value] of data.entries()) {
                if (value) params.set(key, value);
            }
            const query = params.toString();
            window.location.href = `${action}${query ? `?${query}` : ""}`;
        };
        form.querySelectorAll("[data-filter-select]").forEach(input => input.addEventListener("change", navigate));
        form.querySelector("[data-live-search]")?.addEventListener("input", debounce(navigate));
        form.addEventListener("submit", event => event.preventDefault());
    });

    /* ------------------------------------------------------------------
       Employment-status change modal (index rows + details page)
       ------------------------------------------------------------------ */
    const modalEl = document.getElementById("employeeStatusModal");
    const modal = modalEl ? new bootstrap.Modal(modalEl) : null;
    const statusSelect = () => modalEl?.querySelector("#employeeStatusSelect");
    let pendingStatus = null;

    document.querySelectorAll("[data-employee-status]").forEach(button => {
        button.addEventListener("click", () => {
            pendingStatus = button;
            modalEl.querySelector("[data-employee-name]").textContent =
                `Update the employment status for ${button.dataset.employee}?`;
            const current = button.dataset.status;
            const select = statusSelect();
            if (select) {
                Array.from(select.options).forEach(option => {
                    option.selected = option.value === current;
                });
            }
            modal?.show();
        });
    });

    modalEl?.querySelector("[data-employee-status-submit]")?.addEventListener("click", async () => {
        if (!pendingStatus) return;
        const select = statusSelect();
        const body = new URLSearchParams();
        body.set("__RequestVerificationToken", token());
        body.set("employmentStatus", select?.value || "");
        try {
            const response = await fetch(pendingStatus.dataset.url, {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body
            });
            const result = await response.json();
            toast(result.success ? "success" : "warning", result.success ? "Success" : "Attention Required", result.message);
            if (result.success) setTimeout(() => window.location.reload(), 700);
        } catch {
            toast("error", "Something Went Wrong", "The status could not be updated.");
        } finally {
            modal?.hide();
        }
    });

    /* ------------------------------------------------------------------
       Form — live preview, photo upload, uniqueness checks, counters
       ------------------------------------------------------------------ */
    const form = document.querySelector("[data-employee-form]");
    if (form) {
        const first = form.querySelector("[data-employee-preview-first]");
        const last = form.querySelector("[data-employee-preview-last]");
        const preview = document.querySelector("[data-employee-photo-preview]");
        const photoInput = form.querySelector("[data-employee-image-input]");

        const updateInitials = () => {
            if (!preview || photoInput?.files?.[0]) return;
            if (preview.querySelector("img")) return;
            const raw = `${first?.value || ""}${last?.value || ""}`.trim();
            const initials = (raw.length > 2 ? raw.substring(0, 2) : raw).toUpperCase() || "NA";
            preview.textContent = initials;
        };
        form.querySelectorAll("[data-employee-preview-first],[data-employee-preview-last]").forEach(input =>
            input.addEventListener("input", updateInitials));

        photoInput?.addEventListener("change", event => {
            const file = event.target.files?.[0];
            if (!file || !preview) return;
            preview.innerHTML = "";
            const img = document.createElement("img");
            img.src = URL.createObjectURL(file);
            img.alt = "Profile photo preview";
            preview.appendChild(img);
        });

        const verify = debounce(async (input, endpoint, target, param) => {
            if (!input?.value || !target) return;
            target.textContent = "Checking...";
            try {
                const id = input.dataset.currentId || "";
                const response = await fetch(`${endpoint}?${param}=${encodeURIComponent(input.value)}&id=${encodeURIComponent(id)}`);
                const result = await response.json();
                target.textContent = result.message;
                target.style.color = result.available ? "#187248" : "#b64336";
            } catch {
                target.textContent = "Could not verify right now.";
                target.style.color = "#916000";
            }
        }, 450);
        form.querySelector("[data-check-email]")?.addEventListener("input", event => verify(event.target, "/Employees/CheckEmail", document.querySelector("[data-email-status]"), "email"));
        form.querySelector("[data-check-phone]")?.addEventListener("input", event => verify(event.target, "/Employees/CheckPhone", document.querySelector("[data-phone-status]"), "phone"));
        form.querySelector("[data-check-national-id]")?.addEventListener("input", event => verify(event.target, "/Employees/CheckNationalId", document.querySelector("[data-national-id-status]"), "nationalId"));

        // Frontend input guards: mirror the server-side validation rules (11-digit
        // phone starting with 0, 13-digit numeric CNIC) as a first line of defence.
        const restrictToDigits = (input, maxLength) => {
            input?.addEventListener("input", () => {
                const cleaned = input.value.replace(/\D+/g, "").slice(0, maxLength);
                if (cleaned !== input.value) {
                    input.value = cleaned;
                }
            });
        };
        restrictToDigits(form.querySelector("[name='PhoneNumber']"), 11);
        restrictToDigits(form.querySelector("[name='NationalIdNumber']"), 13);
        restrictToDigits(form.querySelector("[name='EmergencyContactNumber']"), 11);

        form.querySelectorAll("[data-counter-target]").forEach(target => {
            const field = form.querySelector(`[name="${target.dataset.counterTarget}"]`);
            if (!field) return;
            const update = () => {
                const max = Number(field.maxLength || 0);
                target.textContent = max ? `${field.value.length} / ${max}` : "";
            };
            field.addEventListener("input", update);
            update();
        });

        let dirty = false;
        form.querySelectorAll("input,select,textarea").forEach(input => input.addEventListener("change", () => {
            dirty = true;
            document.querySelector("[data-save-bar]")?.classList.add("is-dirty");
        }));
        window.addEventListener("beforeunload", event => {
            if (!dirty) return;
            event.preventDefault();
            event.returnValue = "";
        });
        form.addEventListener("submit", () => {
            dirty = false;
            form.querySelector("[data-loading-button]")?.setAttribute("disabled", "disabled");
        });
    }
})();