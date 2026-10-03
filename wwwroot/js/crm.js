(function () {
    const debounce = (fn, ms = 450) => {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), ms);
        };
    };

    document.querySelectorAll('[data-crm-search]').forEach((input) => {
        input.addEventListener('input', debounce(() => input.form?.submit()));
    });

    document.querySelectorAll('[data-crm-filter]').forEach((input) => {
        input.addEventListener('change', () => input.form?.submit());
    });

    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((element) => {
        if (window.bootstrap?.Tooltip) {
            new bootstrap.Tooltip(element);
        }
    });

    const guidance = document.querySelector('[data-crm-guidance]');
    const guidanceToggle = document.querySelector('[data-crm-guidance-toggle]');
    const collapsed = localStorage.getItem('gamehub-crm-guidance-collapsed') === 'true';
    if (guidance && collapsed) {
        guidance.classList.add('is-collapsed');
        guidanceToggle?.setAttribute('aria-expanded', 'false');
    }
    guidanceToggle?.addEventListener('click', () => {
        guidance?.classList.toggle('is-collapsed');
        const isCollapsed = guidance?.classList.contains('is-collapsed') === true;
        guidanceToggle.setAttribute('aria-expanded', String(!isCollapsed));
        localStorage.setItem('gamehub-crm-guidance-collapsed', String(isCollapsed));
    });

    document.querySelector('[data-crm-guide-open]')?.addEventListener('click', () => {
        const modal = document.getElementById('crmGuideModal');
        if (modal && window.bootstrap?.Modal) {
            bootstrap.Modal.getOrCreateInstance(modal).show();
        }
    });

    const getActiveFollowupState = () => document.querySelector('[data-followup-tab].active')?.dataset.followupTab || 'all';
    const applyFollowupFilters = () => {
        const state = getActiveFollowupState();
        const query = (document.querySelector('[data-followup-search]')?.value || '').trim().toLowerCase();
        document.querySelectorAll('[data-followup-state]').forEach((item) => {
            const matchesState = state === 'all' || item.dataset.followupState === state;
            const matchesSearch = !query || (item.dataset.followupSearchRow || item.textContent || '').toLowerCase().includes(query);
            item.hidden = !matchesState || !matchesSearch;
        });
    };

    document.querySelectorAll('[data-followup-tab]').forEach((button) => {
        button.addEventListener('click', () => {
            document.querySelectorAll('[data-followup-tab]').forEach((item) => item.classList.toggle('active', item === button));
            applyFollowupFilters();
        });
    });

    document.querySelector('[data-followup-search]')?.addEventListener('input', debounce(applyFollowupFilters, 250));
    document.querySelector('[data-followup-clear]')?.addEventListener('click', () => {
        const search = document.querySelector('[data-followup-search]');
        if (search) search.value = '';
        const allButton = document.querySelector('[data-followup-tab="all"]');
        document.querySelectorAll('[data-followup-tab]').forEach((item) => item.classList.toggle('active', item === allButton));
        applyFollowupFilters();
    });

    const stage = document.querySelector('[data-stage-select]');
    const probability = document.querySelector('[data-preview-probability]');
    const probabilities = {
        Qualification: 10,
        NeedsAnalysis: 25,
        ProposalPreparation: 40,
        ProposalSent: 60,
        Negotiation: 80,
        Won: 100,
        Lost: 0,
        OnHold: 20
    };
    stage?.addEventListener('change', () => {
        if (probability && probabilities[stage.value] != null) {
            probability.value = probabilities[stage.value];
            probability.dispatchEvent(new Event('input'));
        }
    });

    document.querySelectorAll('[data-crm-form]').forEach((form) => {
        let dirty = false;
        form.addEventListener('input', () => { dirty = true; });
        form.addEventListener('submit', () => { dirty = false; });
        window.addEventListener('beforeunload', (event) => {
            if (dirty) {
                event.preventDefault();
                event.returnValue = '';
            }
        });
    });

    const setText = (selector, value) => {
        const element = document.querySelector(selector);
        if (element) element.textContent = value || element.textContent;
    };
    document.querySelector('[data-preview-name]')?.addEventListener('input', (event) => setText('[data-preview-name-text]', event.target.value || 'New Record'));
    document.querySelector('[data-preview-value]')?.addEventListener('input', (event) => setText('[data-preview-value-text]', Number(event.target.value || 0).toLocaleString()));
    document.querySelector('[data-preview-probability]')?.addEventListener('input', (event) => setText('[data-preview-probability-text]', event.target.value || 0));
    document.querySelector('[data-preview-stage]')?.addEventListener('change', (event) => setText('[data-preview-stage-text]', event.target.options[event.target.selectedIndex].text));
})();
