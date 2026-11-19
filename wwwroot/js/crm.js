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

    document.querySelectorAll('[data-fv-status-select]').forEach((select) => {
        // The lead status workflow is enforced server-side (LeadStatusRules);
        // the form only ever offers valid options, nothing to patch here.
        select.addEventListener('change', () => setText('[data-preview-status-text]', select.options[select.selectedIndex]?.text || ''));
    });

    const convertModal = document.querySelector('[data-crm-convert-modal]');
    if (convertModal && window.bootstrap?.Modal) {
        const modal = bootstrap.Modal.getOrCreateInstance(convertModal);
        const continueLink = convertModal.querySelector('[data-convert-continue]');
        document.querySelectorAll('[data-crm-convert]').forEach((button) => {
            button.addEventListener('click', () => {
                convertModal.querySelector('[data-convert-lead-number]').textContent = button.dataset.leadNumber || '';
                convertModal.querySelector('[data-convert-lead-name]').textContent = button.dataset.leadName || '';
                if (continueLink) continueLink.href = `/CRM/ConvertToOpportunity/${encodeURIComponent(button.dataset.leadId)}`;
                modal.show();
            });
        });
        continueLink?.addEventListener('click', () => modal.hide());
    }

    // Qualify / Disqualify quick actions from the lead three-dot menu. The
    // same controller actions power the lead page forms; this modal just
    // collects the required notes or reason without leaving the listing.
    const leadActionModal = document.querySelector('[data-crm-lead-action-modal]');
    const leadActionButtons = document.querySelectorAll('[data-crm-lead-action]');
    if (leadActionButtons.length) {
        // The dialog must share the stacking context of the backdrop that
        // Bootstrap appends to <body>; a transformed page container would
        // otherwise size the fixed dialog against itself and cover it.
        if (leadActionModal && leadActionModal.parentElement !== document.body) {
            document.body.appendChild(leadActionModal);
        }
        const instance = leadActionModal && window.bootstrap?.Modal ? bootstrap.Modal.getOrCreateInstance(leadActionModal) : null;
        const form = leadActionModal?.querySelector('[data-crm-lead-action-form]');
        const input = leadActionModal?.querySelector('[data-lead-action-input]');
        if (instance && form && input) {
            const setModalText = (selector, text) => {
                const element = leadActionModal.querySelector(selector);
                if (element) element.textContent = text;
            };
            leadActionButtons.forEach((button) => {
                if (button.dataset.crmLeadActionBound === 'true') {
                    return;
                }
                button.dataset.crmLeadActionBound = 'true';
                button.addEventListener('click', () => {
                    const isQualify = button.dataset.crmLeadAction === 'qualify';
                    const baseUrl = isQualify ? leadActionModal.dataset.qualifyUrl : leadActionModal.dataset.disqualifyUrl;
                    form.action = `${baseUrl}/${encodeURIComponent(button.dataset.leadId)}`;
                    input.name = isQualify ? 'qualificationNotes' : 'reason';
                    input.value = '';
                    input.required = true;
                    input.minLength = isQualify ? 0 : 10;
                    input.placeholder = isQualify ? 'Capture needs, budget and timeline' : 'Reason (min 10 characters)';
                    setModalText('[data-lead-action-title]', isQualify ? 'Qualify Lead' : 'Disqualify Lead');
                    setModalText('[data-lead-action-label]', isQualify ? 'Qualification Notes' : 'Disqualification Reason');
                    setModalText('[data-lead-action-number]', button.dataset.leadNumber || '');
                    setModalText('[data-lead-action-name]', button.dataset.leadName || '');
                    const icon = leadActionModal.querySelector('[data-lead-action-icon]');
                    if (icon) icon.className = isQualify ? 'bi bi-patch-check' : 'bi bi-x-circle';
                    const submit = leadActionModal.querySelector('[data-lead-action-submit]');
                    if (submit) submit.innerHTML = isQualify ? '<i class="bi bi-patch-check"></i> Qualify Lead' : '<i class="bi bi-x-circle"></i> Disqualify Lead';
                    const note = leadActionModal.querySelector('[data-lead-action-note]');
                    if (note) note.innerHTML = isQualify
                        ? '<i class="bi bi-info-circle"></i> Qualifying marks this lead as Qualified and makes it available for opportunity conversion.'
                        : '<i class="bi bi-info-circle"></i> Disqualifying blocks conversion into an opportunity. The reason is kept in the activity timeline.';
                    delete form.dataset.submitting;
                    // The dropdown stays open behind the backdrop otherwise.
                    const toggle = button.closest('.dropdown')?.querySelector('[data-bs-toggle="dropdown"]');
                    if (toggle && window.bootstrap?.Dropdown) {
                        bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
                    }
                    instance.show();
                });
            });
            leadActionModal.addEventListener('shown.bs.modal', () => input.focus());
            leadActionModal.addEventListener('hide.bs.modal', () => {
                // Release focus before Bootstrap marks the dialog aria-hidden.
                if (leadActionModal.contains(document.activeElement)) {
                    document.activeElement.blur();
                }
            });
            form.addEventListener('submit', (event) => {
                if (form.dataset.submitting === 'true') {
                    event.preventDefault();
                    return;
                }
                form.dataset.submitting = 'true';
            });
            leadActionModal.addEventListener('hidden.bs.modal', () => {
                delete form.dataset.submitting;
                input.value = '';
            });
        } else {
            // Without the Bootstrap bundle the modal cannot open; fall back to
            // the equivalent forms on the lead page instead of doing nothing.
            leadActionButtons.forEach((button) => {
                button.addEventListener('click', () => {
                    if (button.dataset.leadDetailsUrl) window.location.href = `${button.dataset.leadDetailsUrl}#lead-actions`;
                });
            });
        }
    }

    // Mark-as-Lost dialog on the opportunity page. It posts to the same
    // ChangeOpportunityStage action as the stage buttons; this only collects
    // the reason the workflow requires.
    const stageLostModal = document.querySelector('[data-crm-stage-modal]');
    if (stageLostModal && window.bootstrap?.Modal) {
        // Share the stacking context of the backdrop Bootstrap appends to
        // <body> so the dialog cannot be covered by the page shell.
        if (stageLostModal.parentElement !== document.body) {
            document.body.appendChild(stageLostModal);
        }
        const instance = bootstrap.Modal.getOrCreateInstance(stageLostModal);
        const form = stageLostModal.querySelector('[data-crm-stage-form]');
        const reason = stageLostModal.querySelector('[data-crm-stage-reason]');
        document.querySelectorAll('[data-crm-stage-lost]').forEach((button) => {
            if (button.dataset.crmStageBound === 'true') return;
            button.dataset.crmStageBound = 'true';
            button.addEventListener('click', () => {
                const number = stageLostModal.querySelector('[data-stage-number]');
                const name = stageLostModal.querySelector('[data-stage-name]');
                if (number) number.textContent = button.dataset.stageNumber || '';
                if (name) name.textContent = button.dataset.stageName || '';
                if (reason) reason.value = '';
                if (form) delete form.dataset.submitting;
                instance.show();
            });
        });
        stageLostModal.addEventListener('shown.bs.modal', () => reason?.focus());
        stageLostModal.addEventListener('hide.bs.modal', () => {
            if (stageLostModal.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });
        form?.addEventListener('submit', (event) => {
            if (form.dataset.submitting === 'true') {
                event.preventDefault();
                return;
            }
            form.dataset.submitting = 'true';
        });
        stageLostModal.addEventListener('hidden.bs.modal', () => {
            if (form) delete form.dataset.submitting;
            if (reason) reason.value = '';
        });
    }
})();
