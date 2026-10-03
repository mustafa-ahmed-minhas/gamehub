// Shared layout behavior for visual-only UI details.
(function () {
    "use strict";

    document.querySelectorAll("[data-password-toggle]").forEach((toggle) => {
        toggle.addEventListener("click", () => {
            const input = toggle.closest(".password-wrap, .auth-field")?.querySelector("input")
                ?? toggle.parentElement?.querySelector("input");
            if (!input) {
                return;
            }

            const isPassword = input.type === "password";
            input.type = isPassword ? "text" : "password";
            toggle.setAttribute("aria-label", isPassword ? "Hide password" : "Show password");

            const icon = toggle.querySelector("i");
            if (icon) {
                icon.className = isPassword ? "bi bi-eye-slash" : "bi bi-eye";
            }
        });
    });

    const loginForm = document.querySelector("[data-login-form]");
    if (loginForm) {
        loginForm.addEventListener("submit", () => {
            const button = loginForm.querySelector("[data-login-submit]");
            if (!button) {
                return;
            }

            button.disabled = true;
            button.querySelector("span").textContent = "Signing In...";
        });
    }
})();
