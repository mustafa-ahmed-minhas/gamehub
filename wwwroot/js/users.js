// User Management module behavior. All business validation remains server-side.
(function () {
    const debounce = (callback, delay = 400) => {
        let timeoutId;
        return (...args) => {
            window.clearTimeout(timeoutId);
            timeoutId = window.setTimeout(() => callback(...args), delay);
        };
    };

    const showToast = (message, isError = false) => {
        if (window.GameHubToast) {
            window.GameHubToast.show({
                type: isError ? "error" : "success",
                title: isError ? "Something Went Wrong" : "Success",
                message
            });
            return;
        }

        const toast = document.createElement("div");
        toast.className = `user-toast${isError ? " error" : ""}`;
        toast.textContent = message;
        document.body.appendChild(toast);
        window.setTimeout(() => toast.remove(), 3600);
    };

    const getAntiForgeryToken = () => document.querySelector("input[name='__RequestVerificationToken']")?.value || "";

    const filterForm = document.querySelector("[data-users-filter-form]");
    if (filterForm) {
        const searchInput = filterForm.querySelector("[data-live-search]");
        const sortSelect = filterForm.querySelector("select[name='sortBy']");
        const sortDirection = filterForm.querySelector("[data-sort-direction]");

        const submitFilters = () => filterForm.requestSubmit();
        searchInput?.addEventListener("input", debounce(() => submitFilters(), 450));

        filterForm.querySelectorAll("[data-auto-submit]").forEach((control) => {
            control.addEventListener("change", () => {
                if (control === sortSelect && sortDirection) {
                    const selected = sortSelect.options[sortSelect.selectedIndex];
                    sortDirection.value = selected.dataset.direction || (selected.value === "oldest" ? "asc" : "desc");
                }

                submitFilters();
            });
        });
    }

    const formPage = document.querySelector("[data-user-form]");
    if (formPage) {
        const form = formPage.querySelector("form");
        const submitButton = formPage.querySelector("[data-user-submit]");
        const firstName = formPage.querySelector("[data-preview-first]");
        const lastName = formPage.querySelector("[data-preview-last]");
        const email = formPage.querySelector("[data-preview-email]");
        const phone = formPage.querySelector("[data-preview-phone]");
        const role = formPage.querySelector("[data-preview-role]");
        const status = formPage.querySelector("[data-preview-status]");
        const password = formPage.querySelector("[data-password-input]");
        const confirmPassword = formPage.querySelector("[data-confirm-password]");
        const emailMessage = formPage.querySelector("[data-email-message]");
        let emailAvailable = true;

        const roleSummaries = {
            SuperAdmin: "This user will have complete access to all ERP modules.",
            Admin: "This user will have general administrative access across the ERP.",
            BookingManager: "This user will manage bookings, customers, and schedules.",
            CourtManager: "This user will manage courts and facility availability.",
            FinanceManager: "This user will manage payments and financial records.",
            StaffManager: "This user will manage staff and attendance.",
            Receptionist: "This user will handle bookings, customers, and front-desk operations.",
            Viewer: "This user will have read-only access."
        };

        const displayRoleNames = {
            SuperAdmin: "Super Admin",
            Admin: "Admin",
            BookingManager: "Booking Manager",
            CourtManager: "Court Manager",
            FinanceManager: "Finance Manager",
            StaffManager: "Staff Manager",
            Receptionist: "Receptionist",
            Viewer: "Viewer"
        };

        const updatePreview = () => {
            const fullName = `${firstName?.value || ""} ${lastName?.value || ""}`.trim() || "New User";
            const initials = fullName === "New User"
                ? "NU"
                : fullName.split(/\s+/).slice(0, 2).map((part) => part[0]).join("").toUpperCase();
            const selectedRole = role?.value || "";
            const isActive = status?.checked ?? true;

            formPage.querySelector("[data-preview-initials]").textContent = initials;
            formPage.querySelector("[data-preview-name]").textContent = fullName;
            formPage.querySelector("[data-preview-email-text]").textContent = email?.value || "email@gamehub.pk";
            formPage.querySelector("[data-preview-phone-text]").textContent = phone?.value || "Phone not provided";
            formPage.querySelector("[data-preview-role-text]").textContent = displayRoleNames[selectedRole] || "Select a role";

            const previewStatus = formPage.querySelector("[data-preview-status-text]");
            previewStatus.classList.toggle("active", isActive);
            previewStatus.classList.toggle("inactive", !isActive);
            previewStatus.querySelector("span").textContent = isActive ? "Active" : "Inactive";

            formPage.querySelector("[data-access-summary]").textContent = roleSummaries[selectedRole] || "Select a role to preview this user's future ERP access.";
            formPage.querySelector("[data-role-description]").textContent = role?.selectedOptions[0]?.dataset.description || "Select a role to define ERP access.";
        };

        [firstName, lastName, email, phone, role, status].forEach((input) => {
            input?.addEventListener("input", updatePreview);
            input?.addEventListener("change", updatePreview);
        });

        formPage.querySelectorAll("[data-password-toggle]").forEach((button) => {
            button.addEventListener("click", () => {
                const input = button.parentElement?.querySelector("input");
                if (!input) {
                    return;
                }

                input.type = input.type === "password" ? "text" : "password";
                button.querySelector("i").className = input.type === "password" ? "bi bi-eye" : "bi bi-eye-slash";
            });
        });

        const evaluatePassword = () => {
            const value = password?.value || "";
            const rules = {
                length: value.length >= 8,
                upper: /[A-Z]/.test(value),
                lower: /[a-z]/.test(value),
                number: /\d/.test(value),
                special: /[^A-Za-z0-9]/.test(value)
            };

            Object.entries(rules).forEach(([rule, met]) => {
                formPage.querySelector(`[data-rule='${rule}']`)?.classList.toggle("met", met);
            });

            const score = Object.values(rules).filter(Boolean).length;
            const meter = formPage.querySelector("[data-password-meter]");
            const label = formPage.querySelector("[data-password-strength]");
            meter.className = "";

            if (!value) {
                meter.style.width = "0";
                label.textContent = "Enter a password to check strength.";
            } else if (score <= 2) {
                meter.style.width = "34%";
                label.textContent = "Weak password";
            } else if (score <= 4) {
                meter.classList.add("medium");
                label.textContent = "Medium password";
            } else {
                meter.classList.add("strong");
                label.textContent = "Strong password";
            }

            const matchLabel = formPage.querySelector("[data-password-match]");
            if (!confirmPassword?.value && !value) {
                matchLabel.textContent = "Passwords must match.";
                matchLabel.className = "";
            } else if (value && confirmPassword?.value === value) {
                matchLabel.textContent = "Passwords match";
                matchLabel.className = "match-ok";
            } else {
                matchLabel.textContent = "Passwords do not match";
                matchLabel.className = "match-error";
            }
        };

        password?.addEventListener("input", evaluatePassword);
        confirmPassword?.addEventListener("input", evaluatePassword);

        const checkEmail = debounce(async () => {
            const value = email?.value.trim() || "";
            const id = formPage.dataset.userId || "";

            if (!value || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) {
                emailAvailable = false;
                emailMessage.textContent = "Invalid email format";
                emailMessage.className = "email-error";
                submitButton.disabled = true;
                return;
            }

            emailMessage.textContent = "Checking email...";
            emailMessage.className = "";

            try {
                const response = await fetch(`/Users/CheckEmail?email=${encodeURIComponent(value)}&id=${encodeURIComponent(id)}`, {
                    headers: { "Accept": "application/json" }
                });
                const result = await response.json();
                emailAvailable = Boolean(result.available);
                emailMessage.textContent = result.message;
                emailMessage.className = emailAvailable ? "email-ok" : "email-error";
                submitButton.disabled = !emailAvailable;
            } catch {
                emailAvailable = true;
                emailMessage.textContent = "Email check unavailable. Server validation will still run.";
                emailMessage.className = "";
                submitButton.disabled = false;
            }
        }, 450);

        email?.addEventListener("input", checkEmail);
        email?.addEventListener("blur", checkEmail);
        form?.addEventListener("submit", (event) => {
            if (!emailAvailable) {
                event.preventDefault();
                showToast("Email address is already in use", true);
            }
        });

        updatePreview();
        evaluatePassword();
    }

    const modalElement = document.getElementById("statusConfirmModal");
    if (modalElement && window.bootstrap) {
        const modal = new bootstrap.Modal(modalElement);
        let pendingButton = null;
        const message = modalElement.querySelector("[data-status-message]");
        const confirm = modalElement.querySelector("[data-confirm-status]");

        document.querySelectorAll("[data-status-toggle]").forEach((button) => {
            button.addEventListener("click", () => {
                pendingButton = button;
                const isActive = button.dataset.isActive === "true";
                const name = button.dataset.userName || "this user";
                message.textContent = isActive
                    ? `${name} will no longer be able to access the ERP once authentication is connected.`
                    : `${name} will regain ERP access once authentication is connected.`;
                modal.show();
            });
        });

        confirm?.addEventListener("click", async () => {
            if (!pendingButton) {
                return;
            }

            const userId = pendingButton.dataset.userId;
            confirm.disabled = true;

            try {
                const response = await fetch(`/Users/ToggleStatus/${userId}`, {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken": getAntiForgeryToken(),
                        "Accept": "application/json"
                    }
                });
                const result = await response.json();

                if (!result.success) {
                    showToast(result.message || "Status could not be changed", true);
                    return;
                }

                pendingButton.dataset.isActive = String(result.isActive);
                pendingButton.setAttribute("aria-label", `${result.isActive ? "Deactivate" : "Activate"} ${pendingButton.dataset.userName}`);
                pendingButton.querySelector("i").className = result.isActive ? "bi bi-pause-circle" : "bi bi-play-circle";

                const row = document.querySelector(`[data-user-row='${userId}']`) || document;
                row.querySelectorAll("[data-status-badge]").forEach((badge) => {
                    badge.classList.toggle("active", result.isActive);
                    badge.classList.toggle("inactive", !result.isActive);
                    badge.querySelector("[data-status-text]").textContent = result.statusText;
                });

                document.querySelectorAll("[data-status-text]").forEach((statusText) => {
                    if (statusText.closest("[data-status-badge]")) {
                        return;
                    }
                    statusText.textContent = result.statusText;
                });

                showToast(result.message || "User status updated");
                modal.hide();
            } catch {
                showToast("Status update failed. Please try again.", true);
            } finally {
                confirm.disabled = false;
            }
        });
    }
})();
