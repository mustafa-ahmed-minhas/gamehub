/* ==========================================================================
   HR Module — Recruitment Management (Screen 2)
   Client-side helpers layered on top of the server-rendered screens:
     - index: live search / filter navigation (openings + applications tabs)
     - workflow confirmation modal for shortlist / reject actions
     - candidate details: pre-select current interview status / outcome
     - interview form: character counters, unsaved-change guard, loading state
   All actions are re-validated on the server; these helpers only improve UX.
   ========================================================================== */
(() => {
    if (window.__gameHubRecruitmentInitialized) return;
    window.__gameHubRecruitmentInitialized = true;

    const debounce = (fn, delay = 400) => {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), delay);
        };
    };

    /* ------------------------------------------------------------------
       Index — search + filter auto-navigation (per active tab)
       ------------------------------------------------------------------ */
    document.querySelectorAll("[data-recruitment-filters], [data-offer-filters], [data-performance-filters], [data-termination-filters]").forEach(form => {
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
       Workflow confirmation modal (shortlist / reject)
       The real <form> stays intact so antiforgery + server routing are
       preserved; the modal only gates the submission.
       ------------------------------------------------------------------ */
    const confirmEl = document.getElementById("hrConfirmModal");
    const confirmModal = confirmEl ? new bootstrap.Modal(confirmEl) : null;
    let pendingForm = null;

    document.querySelectorAll("[data-hr-confirm-form]").forEach(form => {
        form.addEventListener("submit", event => {
            if (form.dataset.hrConfirmed === "true") return;
            event.preventDefault();
            pendingForm = form;
            const title = confirmEl?.querySelector("[data-hr-confirm-title]");
            const message = confirmEl?.querySelector("[data-hr-confirm-message]");
            const submit = confirmEl?.querySelector("[data-hr-confirm-submit]");
            if (title) title.textContent = form.dataset.confirmTitle || "Confirm Action";
            if (message) message.textContent = form.dataset.confirmMessage || "Are you sure?";
            if (submit) submit.classList.toggle("is-danger", form.dataset.confirmDanger === "true");
            confirmModal?.show();
        });
    });

    confirmEl?.querySelector("[data-hr-confirm-submit]")?.addEventListener("click", () => {
        if (!pendingForm) return;
        pendingForm.dataset.hrConfirmed = "true";
        confirmModal?.hide();
        pendingForm.submit();
    });

    /* ------------------------------------------------------------------
       Candidate details — pre-select the stored interview status / outcome
       ------------------------------------------------------------------ */
    document.querySelectorAll("[data-interview-update]").forEach(form => {
        form.querySelectorAll("select[data-selected]").forEach(select => {
            Array.from(select.options).forEach(option => {
                option.selected = option.value === select.dataset.selected;
            });
        });
    });

    /* ------------------------------------------------------------------
       Interview form — counters, unsaved-change guard, loading state
       ------------------------------------------------------------------ */
    const interviewForm = document.querySelector("[data-interview-form]");
    if (interviewForm) {
        interviewForm.querySelectorAll("[data-counter-target]").forEach(target => {
            const field = interviewForm.querySelector(`[name="${target.dataset.counterTarget}"]`);
            if (!field) return;
            const update = () => {
                const max = Number(field.maxLength || 0);
                target.textContent = max ? `${field.value.length} / ${max}` : "";
            };
            field.addEventListener("input", update);
            update();
        });

        let dirty = false;
        interviewForm.querySelectorAll("input,select,textarea").forEach(input =>
            input.addEventListener("change", () => {
                dirty = true;
                document.querySelector("[data-save-bar]")?.classList.add("is-dirty");
            }));

        window.addEventListener("beforeunload", event => {
            if (!dirty) return;
            event.preventDefault();
            event.returnValue = "";
        });

        interviewForm.addEventListener("submit", () => {
            dirty = false;
            interviewForm.querySelector("[data-loading-button]")?.setAttribute("disabled", "disabled");
        });
    }

    /* ------------------------------------------------------------------
       Duplicate-submission guard
       Opt-in forms (create / edit) disable their submit button once the
       browser has accepted the submission, so a double click cannot post the
       same record twice. The server still re-validates every request.
       ------------------------------------------------------------------ */
    document.querySelectorAll("form[data-hr-submit-once]").forEach(form => {
        form.addEventListener("submit", () => {
            form.querySelectorAll("button[type='submit']").forEach(button => {
                button.setAttribute("disabled", "disabled");
            });
        });
    });
})();
