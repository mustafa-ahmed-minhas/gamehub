// Global JavaScript for the GameHub public landing page.
(function () {
    "use strict";

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const navbar = document.querySelector("[data-public-navbar]");
    const navToggle = document.querySelector("[data-nav-toggle]");
    const navLinks = Array.from(document.querySelectorAll("[data-nav-links] a"));
    const revealItems = Array.from(document.querySelectorAll(".reveal"));
    const counters = Array.from(document.querySelectorAll(".counter"));
    const heroBg = document.querySelector(".hero-bg");

    function updateNavbar() {
        if (!navbar) return;
        navbar.classList.toggle("is-scrolled", window.scrollY > 24);
    }

    function closeMobileMenu() {
        if (!navbar || !navToggle) return;
        navbar.classList.remove("menu-open");
        navToggle.setAttribute("aria-expanded", "false");
        const icon = navToggle.querySelector("i");
        if (icon) icon.className = "bi bi-list";
    }

    function setActiveLink() {
        const fromTop = window.scrollY + 130;

        navLinks.forEach((link) => {
            const section = document.querySelector(link.getAttribute("href"));
            if (!section) return;
            const isActive = section.offsetTop <= fromTop &&
                section.offsetTop + section.offsetHeight > fromTop;
            link.classList.toggle("active", isActive);
        });
    }

    function animateCounter(counter) {
        if (counter.dataset.counted === "true") return;
        counter.dataset.counted = "true";

        const target = Number(counter.dataset.target || "0");
        const duration = 1200;
        const start = performance.now();

        function tick(now) {
            const progress = Math.min((now - start) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            counter.textContent = Math.round(target * eased).toLocaleString();

            if (progress < 1) {
                requestAnimationFrame(tick);
            }
        }

        requestAnimationFrame(tick);
    }

    if (navToggle && navbar) {
        navToggle.addEventListener("click", () => {
            const isOpen = navbar.classList.toggle("menu-open");
            navToggle.setAttribute("aria-expanded", isOpen.toString());
            const icon = navToggle.querySelector("i");
            if (icon) icon.className = isOpen ? "bi bi-x-lg" : "bi bi-list";
        });
    }

    navLinks.forEach((link) => {
        link.addEventListener("click", closeMobileMenu);
    });

    const revealObserver = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) return;
            entry.target.classList.add("is-visible");
            revealObserver.unobserve(entry.target);
        });
    }, { threshold: 0.16, rootMargin: "0px 0px -40px 0px" });

    revealItems.forEach((item, index) => {
        item.style.transitionDelay = `${Math.min(index % 4, 3) * 80}ms`;
        revealObserver.observe(item);
    });

    const counterObserver = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) return;
            animateCounter(entry.target);
            counterObserver.unobserve(entry.target);
        });
    }, { threshold: 0.6 });

    counters.forEach((counter) => counterObserver.observe(counter));

    const testimonialCards = Array.from(document.querySelectorAll(".testimonial-card"));
    const nextButton = document.querySelector("[data-testimonial-next]");
    const prevButton = document.querySelector("[data-testimonial-prev]");
    let testimonialIndex = testimonialCards.findIndex((card) => card.classList.contains("active"));
    testimonialIndex = testimonialIndex < 0 ? 0 : testimonialIndex;

    function showTestimonial(nextIndex) {
        if (!testimonialCards.length) return;
        testimonialIndex = (nextIndex + testimonialCards.length) % testimonialCards.length;
        testimonialCards.forEach((card, index) => {
            card.classList.toggle("active", index === testimonialIndex);
        });
    }

    if (nextButton) {
        nextButton.addEventListener("click", () => showTestimonial(testimonialIndex + 1));
    }

    if (prevButton) {
        prevButton.addEventListener("click", () => showTestimonial(testimonialIndex - 1));
    }

    if (!reducedMotion && heroBg) {
        window.addEventListener("scroll", () => {
            const offset = Math.min(window.scrollY * 0.08, 42);
            heroBg.style.transform = `scale(1.07) translateY(${offset}px)`;
        }, { passive: true });
    }

    window.addEventListener("scroll", () => {
        updateNavbar();
        setActiveLink();
    }, { passive: true });

    updateNavbar();
    setActiveLink();
})();
