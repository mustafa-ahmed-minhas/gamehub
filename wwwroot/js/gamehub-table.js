(() => {
    const debounce = (fn, delay = 400) => {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), delay);
        };
    };

    document.querySelectorAll("[data-gamehub-table-filters]").forEach(form => {
        const navigate = () => {
            const data = new FormData(form);
            data.delete("page");
            const params = new URLSearchParams();
            for (const [key, value] of data.entries()) {
                if (value) params.set(key, value);
            }
            const query = params.toString();
            window.location.href = `${form.action || window.location.pathname}${query ? `?${query}` : ""}`;
        };

        form.querySelectorAll("[data-filter-select]").forEach(input => input.addEventListener("change", navigate));
        form.querySelector("[data-live-search]")?.addEventListener("input", debounce(navigate));
        form.addEventListener("submit", event => event.preventDefault());
    });

    const closeMenus = except => {
        document.querySelectorAll("[data-gamehub-table-menu].is-open").forEach(menu => {
            if (menu !== except) menu.classList.remove("is-open", "drop-up");
        });
    };

    document.addEventListener("click", event => {
        const button = event.target.closest("[data-gamehub-table-menu-button]");
        if (button) {
            event.preventDefault();
            event.stopPropagation();
            const menu = button.closest(".gamehub-table-action-menu")?.querySelector("[data-gamehub-table-menu]");
            if (!menu) return;
            const shouldOpen = !menu.classList.contains("is-open");
            closeMenus(menu);
            menu.classList.toggle("is-open", shouldOpen);
            button.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
            if (shouldOpen) {
                requestAnimationFrame(() => {
                    const rect = menu.getBoundingClientRect();
                    menu.classList.toggle("drop-up", rect.bottom > window.innerHeight - 16);
                });
            }
            return;
        }

        if (!event.target.closest("[data-gamehub-table-menu]")) closeMenus();
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") closeMenus();
    });
})();
