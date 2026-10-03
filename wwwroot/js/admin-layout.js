// Admin layout interactions only; no business behavior is implemented here.
(function () {
    const shell = document.querySelector("[data-admin-shell]");
    if (!shell) {
        return;
    }

    const collapseButton = document.querySelector("[data-sidebar-collapse]");
    const mobileToggle = document.querySelector("[data-mobile-sidebar-toggle]");
    const overlay = document.querySelector("[data-admin-overlay]");
    const dateTarget = document.querySelector("[data-admin-date]");
    const fullscreenButton = document.querySelector("[data-fullscreen-toggle]");
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const savedSidebarState = localStorage.getItem("gamehub-admin-sidebar");
    if (savedSidebarState === "collapsed") {
        shell.classList.add("is-collapsed");
    }
    if (localStorage.getItem("gamehub-admin-mobile-sidebar") === "open" && window.innerWidth <= 991) {
        shell.classList.add("mobile-open");
    }

    collapseButton?.addEventListener("click", () => {
        shell.classList.toggle("is-collapsed");
        localStorage.setItem(
            "gamehub-admin-sidebar",
            shell.classList.contains("is-collapsed") ? "collapsed" : "expanded"
        );
    });

    const closeMobileSidebar = () => {
        shell.classList.remove("mobile-open");
        localStorage.setItem("gamehub-admin-mobile-sidebar", "closed");
    };

    mobileToggle?.addEventListener("click", () => {
        shell.classList.toggle("mobile-open");
        localStorage.setItem("gamehub-admin-mobile-sidebar", shell.classList.contains("mobile-open") ? "open" : "closed");
    });

    overlay?.addEventListener("click", closeMobileSidebar);

    window.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeMobileSidebar();
        }
    });

    if (dateTarget) {
        dateTarget.textContent = new Intl.DateTimeFormat("en-PK", {
            weekday: "short",
            day: "2-digit",
            month: "short",
            year: "numeric"
        }).format(new Date());
    }

    fullscreenButton?.addEventListener("click", () => {
        if (!document.fullscreenElement) {
            document.documentElement.requestFullscreen?.();
            return;
        }

        document.exitFullscreen?.();
    });

    const revealItems = document.querySelectorAll(".admin-reveal");
    if (reduceMotion || !("IntersectionObserver" in window)) {
        revealItems.forEach((item) => item.classList.add("is-visible"));
        return;
    }

    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.12 });

    revealItems.forEach((item, index) => {
        item.style.transitionDelay = `${Math.min(index * 35, 260)}ms`;
        observer.observe(item);
    });
})();
