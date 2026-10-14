// Public careers page interactions. Open positions and job details are rendered
// on the server; this hides the loading placeholder once the page is ready and
// adds lightweight UX helpers to the public application form. Every check here
// is repeated on the server, so these helpers only improve usability.
(function () {
    "use strict";

    const MAX_PDF_BYTES = 5 * 1024 * 1024;
    const MAX_DOCS = 3;

    // Uses the same data-*-loading + is-hidden convention as the booking calendar.
    const loadingState = document.querySelector("[data-careers-loading]");
    if (loadingState) {
        loadingState.classList.add("is-hidden");
    }

    const form = document.querySelector("[data-job-apply-form]");
    if (!form) {
        return;
    }

    const formatBytes = (bytes) => {
        if (bytes >= 1024 * 1024) {
            return (bytes / (1024 * 1024)).toFixed(2) + " MB";
        }
        return Math.max(1, Math.round(bytes / 1024)) + " KB";
    };

    const isPdf = (file) => {
        const name = (file.name || "").toLowerCase();
        return name.endsWith(".pdf") || file.type === "application/pdf";
    };

    const validatePdf = (file) => {
        if (!isPdf(file)) return "Only .pdf files are allowed.";
        if (file.size <= 0) return "The file is empty. Upload a non-empty PDF.";
        if (file.size > MAX_PDF_BYTES) return "File size exceeds the 5 MB limit.";
        return "";
    };

    const createFileItem = (file, issue, onRemove) => {
        const item = document.createElement("div");
        item.className = "job-file-item" + (issue ? " is-invalid" : "");

        const icon = document.createElement("i");
        icon.className = "bi " + (issue ? "bi-exclamation-circle" : "bi-file-earmark-pdf");

        const meta = document.createElement("div");
        meta.className = "job-file-meta";
        const name = document.createElement("strong");
        name.textContent = file.name;
        const detail = document.createElement("span");
        detail.textContent = issue || formatBytes(file.size) + " \u00b7 PDF";
        meta.append(name, detail);

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "job-file-remove";
        remove.setAttribute("aria-label", "Remove file");
        remove.innerHTML = "&times;";
        remove.addEventListener("click", onRemove);

        item.append(icon, meta, remove);
        return item;
    };

    document.querySelectorAll("[data-job-file-input]").forEach((input) => {
        const kind = input.dataset.jobFileInput;
        const list = document.querySelector('[data-job-file-list="' + kind + '"]');
        if (!list) {
            return;
        }

        const render = () => {
            list.innerHTML = "";
            const files = kind === "cv"
                ? (input.files && input.files[0] ? [input.files[0]] : [])
                : Array.from(input.files || []).slice(0, MAX_DOCS);

            files.forEach((file) => {
                list.appendChild(createFileItem(file, validatePdf(file), () => {
                    if (kind === "cv") {
                        input.value = "";
                    } else {
                        const remaining = Array.from(input.files || []).filter((f) => f !== file);
                        if (window.DataTransfer) {
                            const dataTransfer = new DataTransfer();
                            remaining.forEach((f) => dataTransfer.items.add(f));
                            input.files = dataTransfer.files;
                        }
                    }
                    render();
                }));
            });
        };

        input.addEventListener("change", render);
        render();
    });

    const counter = document.querySelector("[data-job-counter-target]");
    if (counter) {
        const target = document.getElementById(counter.dataset.jobCounterTarget)
            || document.querySelector('[name="' + counter.dataset.jobCounterTarget + '"]');
        if (target) {
            const update = () => {
                const max = Number(target.maxLength || 0);
                counter.textContent = max ? target.value.length + " / " + max + " characters" : "";
            };
            target.addEventListener("input", update);
            update();
        }
    }

    // Mirrors the server-side upload rules so obvious mistakes are caught early.
    const collectUploadIssues = () => {
        const issues = [];
        const cv = form.querySelector('[data-job-file-input="cv"]');
        const docs = form.querySelector('[data-job-file-input="docs"]');

        if (cv && cv.files && cv.files[0]) {
            const issue = validatePdf(cv.files[0]);
            if (issue) {
                issues.push("CV / Resume: " + issue);
            }
        }

        if (docs && docs.files) {
            const files = Array.from(docs.files);
            if (files.length > MAX_DOCS) {
                issues.push("A maximum of " + MAX_DOCS + " supporting documents are allowed.");
            }
            files.forEach((file) => {
                const issue = validatePdf(file);
                if (issue) {
                    issues.push(file.name + ": " + issue);
                }
            });
        }

        return issues;
    };

    form.addEventListener("submit", (event) => {
        const issues = collectUploadIssues();
        if (issues.length) {
            event.preventDefault();
            if (window.GameHubToast && typeof window.GameHubToast.show === "function") {
                window.GameHubToast.show({ type: "warning", title: "Check Uploads", message: issues[0] });
            } else {
                alert(issues[0]);
            }
            return;
        }

        const button = form.querySelector("[data-job-apply-submit]");
        if (button) {
            button.disabled = true;
            button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Submitting Application';
        }
    });
})();
