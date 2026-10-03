// System Settings page interactions only.
(function () {
    const page = document.querySelector("[data-settings-page]");
    if (!page) {
        return;
    }

    const form = page.querySelector("[data-settings-form]");
    const submit = page.querySelector("[data-settings-submit]");
    const actionBar = page.querySelector(".settings-action-bar");
    const logoInput = page.querySelector("[data-logo-input]");
    const logoPreview = page.querySelector("[data-logo-preview]");
    const arenaInput = page.querySelector("[data-arena-name]");
    const logoName = page.querySelector("[data-logo-name]");
    const navLinks = page.querySelectorAll(".settings-nav a");
    const initialSnapshot = new FormData(form);
    let isSubmitting = false;

    const serialize = () => new URLSearchParams(new FormData(form)).toString();
    const initialValue = new URLSearchParams(initialSnapshot).toString();

    const updateDirtyState = () => {
        const isDirty = serialize() !== initialValue;
        actionBar?.classList.toggle("is-dirty", isDirty);
        return isDirty;
    };

    const updateLogoPreview = () => {
        const value = logoInput?.value?.trim();
        const name = arenaInput?.value?.trim() || "GameHub Arena";

        if (logoName) {
            logoName.textContent = name;
        }

        if (!logoPreview) {
            return;
        }

        logoPreview.querySelector("img")?.remove();
        const mark = logoPreview.querySelector("span");
        if (value) {
            const image = document.createElement("img");
            image.src = value;
            image.alt = `${name} logo preview`;
            image.addEventListener("load", () => mark?.setAttribute("hidden", "hidden"));
            image.addEventListener("error", () => {
                image.remove();
                mark?.removeAttribute("hidden");
            });
            logoPreview.prepend(image);
        } else {
            mark?.removeAttribute("hidden");
        }
    };

    form?.addEventListener("input", () => {
        updateDirtyState();
        updateLogoPreview();
    });

    form?.addEventListener("change", () => {
        updateDirtyState();
        updateLogoPreview();
    });

    form?.addEventListener("submit", () => {
        isSubmitting = true;
        if (submit) {
            submit.disabled = true;
            submit.querySelector("span").textContent = "Saving...";
        }
    });

    window.addEventListener("beforeunload", (event) => {
        if (!isSubmitting && updateDirtyState()) {
            event.preventDefault();
            event.returnValue = "";
        }
    });

    page.querySelector("[data-settings-cancel]")?.addEventListener("click", (event) => {
        if (updateDirtyState() && !window.confirm("You have unsaved settings changes. Leave without saving?")) {
            event.preventDefault();
        }
    });

    navLinks.forEach((link) => {
        link.addEventListener("click", () => {
            navLinks.forEach((item) => item.classList.remove("active"));
            link.classList.add("active");
        });
    });

    updateLogoPreview();
    updateDirtyState();
})();
