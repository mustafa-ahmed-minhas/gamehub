/* ==========================================================================
   HR Module — Job Posting & Application forms
   Client-side helpers layered on top of jQuery Validation Unobtrusive:
     - skills chip input (posting form)
     - screening questions repeater (posting form)
     - secure PDF upload lists with size/type/max-count guards (application form)
     - speculative character counters
   All checks are re-validated on the server; these helpers only improve UX.
   ========================================================================== */
(() => {
    if (window.__gameHubHrFormsInitialized) return;
    window.__gameHubHrFormsInitialized = true;

    const MAX_PDF_BYTES = 5 * 1024 * 1024;
    const MAX_DOCS = 3;
    const q = (selector, root = document) => root.querySelector(selector);
    const qa = (selector, root = document) => Array.from(root.querySelectorAll(selector));

    const isPdfFile = (file) => {
        const name = (file.name || "").toLowerCase();
        return name.endsWith(".pdf") || file.type === "application/pdf";
    };

    const formatBytes = (bytes) => {
        if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
        return `${Math.max(1, Math.round(bytes / 1024))} KB`;
    };

    /* ------------------------------------------------------------------
       Skills chip input
       ------------------------------------------------------------------ */
    const initSkillsInput = (field) => {
        const entry = q("[data-skills-entry]", field);
        const addButton = q("[data-skills-add]", field);
        const list = q("[data-skills-list]", field);
        const message = q("[data-skills-message]", field);
        if (!entry || !list) return;

        const existing = new Set(qa('[data-chip] input[type="hidden"]', list).map(input => input.value.toLowerCase()));

        const showMessage = (text) => {
            if (!message) return;
            message.textContent = text;
            setTimeout(() => { message.textContent = ""; }, 2600);
        };

        const addChip = (value) => {
            const skill = value.trim().replace(/\s+/g, " ");
            if (!skill) return;
            if (skill.length > 80) {
                showMessage("Each skill cannot exceed 80 characters.");
                return;
            }
            const key = skill.toLowerCase();
            if (existing.has(key)) {
                showMessage(`"${skill}" is already listed.`);
                return;
            }

            const chip = document.createElement("li");
            chip.setAttribute("data-chip", "");
            const label = document.createElement("span");
            label.textContent = skill;
            const remove = document.createElement("button");
            remove.type = "button";
            remove.setAttribute("data-chip-remove", "");
            remove.setAttribute("aria-label", "Remove skill");
            remove.innerHTML = "&times;";
            remove.addEventListener("click", () => {
                chip.remove();
                existing.delete(key);
                list.closest("form")?.dispatchEvent(new Event("change", { bubbles: true }));
            });
            const hidden = document.createElement("input");
            hidden.type = "hidden";
            hidden.name = "RequiredSkills";
            hidden.value = skill;
            chip.append(label, remove, hidden);
            list.appendChild(chip);
            existing.add(key);
            list.closest("form")?.dispatchEvent(new Event("change", { bubbles: true }));
        };

        addButton?.addEventListener("click", () => {
            addChip(entry.value);
            entry.value = "";
            entry.focus();
        });
        entry.addEventListener("keydown", (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                addChip(entry.value);
                entry.value = "";
            }
            if (event.key === "," || event.key === "Tab") {
                event.preventDefault();
                addChip(entry.value);
                entry.value = "";
            }
        });
    };

    /* ------------------------------------------------------------------
       Screening questions repeater
       ------------------------------------------------------------------ */
    const initQuestionsRepeater = (repeater) => {
        const addButton = q("[data-question-add]", repeater.parentElement) || q("[data-question-add]");
        if (!addButton) return;

        const createRow = (value = "") => {
            const row = document.createElement("div");
            row.className = "hr-repeater-row";
            row.setAttribute("data-question-row", "");
            const textarea = document.createElement("textarea");
            textarea.name = "ScreeningQuestions";
            textarea.rows = 2;
            textarea.maxLength = 500;
            textarea.placeholder = "e.g. Do you have prior experience with customer-facing roles?";
            textarea.value = value;
            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "membership-icon-button is-danger";
            remove.setAttribute("data-question-remove", "");
            remove.setAttribute("aria-label", "Remove question");
            remove.innerHTML = '<i class="bi bi-trash"></i>';
            remove.addEventListener("click", () => row.remove());
            row.append(textarea, remove);
            return row;
        };

        addButton.addEventListener("click", () => {
            repeater.appendChild(createRow());
            const last = repeater.lastElementChild;
            last?.querySelector("textarea")?.focus();
        });

        qa("[data-question-row]", repeater).forEach(row => {
            q("[data-question-remove]", row)?.addEventListener("click", () => row.remove());
        });
    };

    /* ------------------------------------------------------------------
       PDF file lists (CV + supporting documents)
       ------------------------------------------------------------------ */
    const initFileInput = (input) => {
        const kind = input.dataset.fileInput; // "cv" | "docs"
        const list = document.querySelector(`[data-file-list="${kind}"]`);
        if (!list) return;

        const render = () => {
            list.innerHTML = "";
            if (kind === "cv") {
                const file = input.files?.[0];
                if (!file) return;
                const verdict = validatePdf(file);
                list.appendChild(createFileItem(file, verdict, () => {
                    input.value = "";
                    render();
                }));
                return;
            }

            // docs: track multi-selection
            const files = Array.from(input.files || []).slice(0, MAX_DOCS);
            files.forEach(file => {
                const verdict = validatePdf(file);
                list.appendChild(createFileItem(file, verdict, () => removeDoc(file)));
            });
        };

        const removeDoc = (file) => {
            const remaining = Array.from(input.files || []).filter(f => f !== file);
            if (window.DataTransfer) {
                const dt = new DataTransfer();
                remaining.forEach(f => dt.items.add(f));
                input.files = dt.files;
            }
            render();
        };

        input.addEventListener("change", render);
        render();
    };

    const validatePdf = (file) => {
        const name = (file.name || "").toLowerCase();
        if (!name.endsWith(".pdf")) return "Only .pdf files are allowed.";
        if (file.size <= 0) return "The file is empty. Upload a non-empty PDF.";
        if (file.size > MAX_PDF_BYTES) return "File size exceeds the 5 MB limit.";
        return "";
    };

    const createFileItem = (file, issue, onRemove) => {
        const item = document.createElement("div");
        item.className = "hr-file-item" + (issue ? " is-invalid" : "");
        const icon = document.createElement("i");
        icon.className = "bi " + (issue ? "bi-exclamation-circle" : "bi-file-earmark-pdf");
        const meta = document.createElement("div");
        meta.className = "hr-file-meta";
        const name = document.createElement("strong");
        name.textContent = file.name;
        const size = document.createElement("span");
        size.textContent = issue || `${formatBytes(file.size)} · PDF`;
        meta.append(name, size);
        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "membership-icon-button is-danger";
        remove.setAttribute("aria-label", "Remove file");
        remove.innerHTML = '<i class="bi bi-x-lg"></i>';
        remove.addEventListener("click", onRemove);
        item.append(icon, meta, remove);
        return item;
    };

    /* ------------------------------------------------------------------
       Form submit guards (run inside jQuery Validation's submitHandler)
       ------------------------------------------------------------------ */
    const collectFileIssues = (form) => {
        const issues = [];
        const cv = q('[data-file-input="cv"]', form);
        const docs = q('[data-file-input="docs"]', form);

        if (cv?.files?.[0]) {
            const issue = validatePdf(cv.files[0]);
            if (issue) issues.push(`CV / Resume: ${issue}`);
        }
        if (docs?.files) {
            const files = Array.from(docs.files);
            if (files.length > MAX_DOCS) issues.push(`A maximum of ${MAX_DOCS} supporting documents are allowed.`);
            files.forEach(file => {
                const issue = validatePdf(file);
                if (issue) issues.push(`${file.name}: ${issue}`);
            });
        }
        return issues;
    };

    const patchSubmitHandler = (form) => {
        const $form = $(form);
        if (!$form.length || typeof $form.validate !== "function" || !$form.validate()) return;

        const validator = $form.validate();
        const previous = validator.settings.submitHandler;
        validator.settings.submitHandler = function (target, event) {
            const issues = collectFileIssues(target);
            const skillsField = q("[data-skills-field]", target);
            const skillsCount = qa('[data-chip]', target).length;
            if (skillsField && skillsCount === 0) {
                const msg = q("[data-skills-message]", target);
                if (msg) {
                    msg.textContent = "Add at least one required skill.";
                    msg.scrollIntoView({ behavior: "smooth", block: "center" });
                }
                target.querySelector("[data-skills-entry]")?.focus();
                return;
            }
            if (issues.length) {
                window.GameHubToasts?.show?.({ type: "warning", title: "Check Uploads", message: issues[0] }) || alert(issues[0]);
                return;
            }
            target.querySelector("[data-loading-button]")?.setAttribute("disabled", "disabled");
            if (typeof previous === "function") {
                previous.call(this, target, event);
            } else {
                target.submit();
            }
        };
    };

    /* ------------------------------------------------------------------
       Character counters
       ------------------------------------------------------------------ */
    const initCounter = (small) => {
        const target = document.getElementById(small.dataset.counterTarget) || q(`[name="${small.dataset.counterTarget}"]`);
        if (!target) return;
        const update = () => {
            const max = Number(target.maxLength || 0);
            small.textContent = max ? `${target.value.length} / ${max}` : "";
        };
        target.addEventListener("input", update);
        update();
    };

    document.addEventListener("DOMContentLoaded", () => {
        qa("[data-skills-field]").forEach(initSkillsInput);
        qa("[data-questions-repeater]").forEach(initQuestionsRepeater);
        qa("[data-file-input]").forEach(initFileInput);
        qa("[data-job-posting-form]").forEach(patchSubmitHandler);
        qa("[data-job-application-form]").forEach(patchSubmitHandler);
        qa("[data-counter-target]").forEach(initCounter);

        // Disable the application submit button when no open postings exist.
        const appForm = q("[data-job-application-form]");
        if (appForm) {
            const postingSelect = q('[name="JobPostingId"]', appForm);
            const submit = q("[data-application-submit]", appForm);
            const sync = () => {
                if (!submit || !postingSelect) return;
                const hasOptions = Array.from(postingSelect.options).some(option => option.value);
                submit.disabled = !hasOptions;
            };
            postingSelect?.addEventListener("change", sync);
            sync();
        }
    });
})();