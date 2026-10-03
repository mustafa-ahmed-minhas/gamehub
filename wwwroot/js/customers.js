(() => {
    if (window.__gameHubCustomersInitialized) return;
    window.__gameHubCustomersInitialized = true;

    const debounce = (fn, delay = 400) => {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), delay);
        };
    };

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    const toast = (type, title, message) => window.GameHubToasts?.show?.({ type, title, message }) || alert(message);

    document.querySelectorAll("[data-customer-filters]").forEach(form => {
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

    const form = document.querySelector("[data-customer-form]");
    if (form) {
        const first = form.querySelector("[data-preview-first]");
        const last = form.querySelector("[data-preview-last]");
        const preferred = form.querySelector("[data-preview-preferred]");
        const phone = form.querySelector("[data-preview-phone]");
        const whatsapp = form.querySelector("[data-preview-whatsapp]");
        const email = form.querySelector("[data-preview-email]");
        const city = form.querySelector("[data-preview-city]");
        const country = form.querySelector("[data-preview-country]");
        const dob = form.querySelector("[data-preview-dob]");
        const type = form.querySelector("[data-preview-type]");
        const source = form.querySelector("[data-preview-source]");
        const active = form.querySelector("[data-preview-active]");
        const member = form.querySelector("[data-membership-toggle]");
        const blacklist = form.querySelector("[data-blacklist-form-toggle]");
        const avatar = document.querySelector("[data-preview-avatar]");
        const nameText = document.querySelector("[data-preview-name]");
        const preferredText = document.querySelector("[data-preview-preferred-text]");
        const phoneText = document.querySelector("[data-preview-phone-text]");
        const emailText = document.querySelector("[data-preview-email-text]");
        const locationText = document.querySelector("[data-preview-location]");
        const ageText = document.querySelector("[data-preview-age]");
        const typeText = document.querySelector("[data-preview-type-text]");
        const sourceText = document.querySelector("[data-preview-source-text]");
        const memberText = document.querySelector("[data-preview-member]");
        const statusText = document.querySelector("[data-preview-status]");
        const blacklistText = document.querySelector("[data-preview-blacklist]");
        const meter = document.querySelector("[data-complete-meter]");
        const completeText = document.querySelector("[data-complete-value]");

        const updatePreview = () => {
            const fullName = `${first?.value || ""} ${last?.value || ""}`.trim() || "New Customer";
            nameText.textContent = fullName;
            const initials = fullName.split(/\s+/).slice(0, 2).map(x => x[0]).join("").toUpperCase() || "NC";
            avatar.textContent = initials;
            preferredText.textContent = preferred?.value || "Preferred name not set";
            phoneText.textContent = phone?.value || "Phone not provided";
            emailText.textContent = email?.value || "Email not provided";
            locationText.textContent = `${city?.value || "City"}, ${country?.value || "Pakistan"}`;
            typeText.textContent = type?.selectedOptions?.[0]?.textContent || "Individual";
            sourceText.textContent = source?.selectedOptions?.[0]?.textContent || "Walk In";
            memberText.textContent = member?.checked ? "Member" : "Non-Member";
            statusText.textContent = active?.checked ? "Active" : "Inactive";
            blacklistText?.classList.toggle("d-none", !blacklist?.checked);
            if (dob?.value) {
                const birth = new Date(dob.value);
                const now = new Date();
                let age = now.getFullYear() - birth.getFullYear();
                if (birth > new Date(now.getFullYear() - age, now.getMonth(), now.getDate())) age--;
                ageText.textContent = age < 13 ? `${age} years old - under 13` : `${age} years old`;
            } else {
                ageText.textContent = "Age not available";
            }
            const fields = [first, last, phone, whatsapp, email, city, country, type, source, dob, preferred];
            const filled = fields.filter(x => x && (x.type === "checkbox" ? x.checked : x.value)).length;
            const pct = Math.round((filled / fields.length) * 100);
            meter.value = pct;
            completeText.textContent = `${pct}%`;
        };
        form.querySelectorAll("input,select,textarea").forEach(input => input.addEventListener("input", updatePreview));
        form.querySelectorAll("select,input[type=checkbox]").forEach(input => input.addEventListener("change", updatePreview));
        updatePreview();

        const toggleBlocks = () => {
            document.querySelectorAll("[data-membership-field]").forEach(x => x.style.display = member?.checked ? "grid" : "none");
            document.querySelectorAll("[data-credit-field]").forEach(x => x.style.display = form.querySelector("[data-credit-toggle]")?.checked ? "grid" : "none");
            document.querySelectorAll("[data-blacklist-field]").forEach(x => x.style.display = blacklist?.checked ? "grid" : "none");
            document.querySelector("[data-corporate-field]")?.style.setProperty("display", type?.value === "Corporate" ? "grid" : "none");
        };
        form.querySelectorAll("[data-membership-toggle],[data-credit-toggle],[data-blacklist-form-toggle],[data-preview-type]").forEach(x => x.addEventListener("change", toggleBlocks));
        toggleBlocks();

        form.querySelector("[data-same-whatsapp]")?.addEventListener("change", event => {
            if (event.target.checked && whatsapp && phone) {
                whatsapp.value = phone.value;
                updatePreview();
            }
        });

        form.querySelector("[data-image-input]")?.addEventListener("change", event => {
            const file = event.target.files?.[0];
            const img = document.querySelector("[data-preview-image]");
            if (!file || !img) return;
            img.src = URL.createObjectURL(file);
            img.classList.remove("d-none");
            avatar.classList.add("d-none");
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
        form.querySelector("[data-check-email]")?.addEventListener("input", event => verify(event.target, "/Customers/CheckEmail", document.querySelector("[data-email-status]"), "email"));
        form.querySelector("[data-check-phone]")?.addEventListener("input", event => verify(event.target, "/Customers/CheckPhone", document.querySelector("[data-phone-status]"), "phone"));
        form.querySelector("[data-check-whatsapp]")?.addEventListener("input", event => verify(event.target, "/Customers/CheckPhone", document.querySelector("[data-whatsapp-status]"), "phone"));
        form.querySelector("[data-check-national-id]")?.addEventListener("input", event => verify(event.target, "/Customers/CheckNationalId", document.querySelector("[data-national-id-status]"), "nationalId"));

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

    const modalEl = document.getElementById("customerConfirmModal");
    const modal = modalEl ? new bootstrap.Modal(modalEl) : null;
    let pendingAction = null;
    document.querySelectorAll("[data-customer-toggle],[data-blacklist-toggle]").forEach(button => {
        button.addEventListener("click", () => {
            pendingAction = button;
            const isBlacklist = button.hasAttribute("data-blacklist-toggle");
            const isBlacklisted = button.dataset.blacklisted === "true";
            modalEl.querySelector("[data-confirm-message]").textContent = isBlacklist
                ? (isBlacklisted ? "Remove this customer from blacklist?" : "This customer will remain in the system but should be reviewed before accepting future bookings.")
                : button.dataset.message;
            modalEl.querySelector("[data-blacklist-reason]").classList.toggle("d-none", !isBlacklist || isBlacklisted);
            modal?.show();
        });
    });
    modalEl?.querySelector("[data-confirm-submit]")?.addEventListener("click", async () => {
        if (!pendingAction) return;
        const body = new URLSearchParams();
        body.set("__RequestVerificationToken", token());
        if (pendingAction.hasAttribute("data-blacklist-toggle")) {
            body.set("reason", modalEl.querySelector("[data-blacklist-reason]")?.value || "");
        }
        try {
            const response = await fetch(pendingAction.dataset.url, {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body
            });
            const result = await response.json();
            toast(result.success ? "success" : "warning", result.success ? "Success" : "Attention Required", result.message);
            if (result.success) setTimeout(() => window.location.reload(), 700);
        } catch {
            toast("error", "Something Went Wrong", "The action could not be completed.");
        } finally {
            modal?.hide();
        }
    });

    document.querySelectorAll("[data-customer-tabs]").forEach(tabs => {
        const buttons = tabs.querySelectorAll("[data-customer-tab]");
        const panels = tabs.querySelectorAll("[data-customer-panel]");
        buttons.forEach(button => {
            button.addEventListener("click", () => {
                const target = button.dataset.customerTab;
                buttons.forEach(item => item.classList.toggle("active", item === button));
                panels.forEach(panel => panel.classList.toggle("active", panel.dataset.customerPanel === target));
            });
        });
    });
})();
