/* ==========================================================================
   HR Module — HR Reports (Screen 7)
   Client-side helpers layered on top of the server-rendered reports:
     - Chart.js rendering from the serialized chart payload
     - filter auto-submit for the drop-downs
     - print-friendly output
   All figures are computed server-side; these helpers only present them.
   ========================================================================== */
(() => {
    'use strict';
    if (window.__gameHubHrReportsInitialized) return;
    window.__gameHubHrReportsInitialized = true;

    const fallbackColors = ['#d6a652', '#171614', '#4f8b6d', '#c77b4f', '#7b8494', '#ead09a', '#8e6640', '#9d4f4f'];

    /* ---- Charts -------------------------------------------------------- */
    const source = document.getElementById('hrReportChartData');
    if (source && window.Chart) {
        try {
            const charts = JSON.parse(source.textContent || '[]');
            charts.forEach((item) => {
                const canvas = document.getElementById(item.Id);
                if (!canvas) return;
                const isDoughnut = item.Type === 'doughnut';
                const colors = item.Colors && item.Colors.length ? item.Colors : fallbackColors;
                new Chart(canvas, {
                    type: item.Type,
                    data: {
                        labels: item.Labels,
                        datasets: [{
                            data: item.Values,
                            borderColor: isDoughnut ? '#fff' : '#c6923a',
                            backgroundColor: isDoughnut ? colors : 'rgba(214,166,82,.28)',
                            borderWidth: 2,
                            borderRadius: 5,
                            tension: 0.34,
                            fill: item.Type === 'line'
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        animation: { duration: 650 },
                        plugins: {
                            legend: {
                                display: isDoughnut,
                                position: 'bottom',
                                labels: { usePointStyle: true, boxWidth: 7, color: '#69635b', font: { family: 'Manrope', size: 10 } }
                            },
                            tooltip: { callbacks: item.Currency ? { label: (ctx) => `${ctx.label || ''}: ${ctx.raw.toLocaleString()}` } : {} }
                        },
                        scales: isDoughnut ? {} : {
                            x: { grid: { display: false }, ticks: { color: '#827c73', maxRotation: 0, autoSkip: true, font: { size: 9 } } },
                            y: { beginAtZero: true, grid: { color: 'rgba(30,26,20,.06)' }, ticks: { color: '#827c73', font: { size: 9 } } }
                        }
                    }
                });
            });
        } catch (error) {
            console.warn('HR report charts could not be initialized.', error);
        }
    }

    /* ---- Filters ------------------------------------------------------- */
    const form = document.querySelector('[data-hr-reports-filters]');
    if (form) {
        let submitted = false;
        const submit = () => {
            if (submitted) return;
            submitted = true;
            if (typeof form.requestSubmit === 'function') form.requestSubmit();
            else form.submit();
        };

        form.querySelectorAll('[data-filter-select]').forEach((select) => {
            select.addEventListener('change', submit);
        });

        // Manually editing a date implies a custom range, so keep the preset in sync
        // without submitting immediately (the user still confirms with Apply).
        const preset = form.querySelector('select[name="preset"]');
        form.querySelectorAll('input[type="date"]').forEach((input) => {
            input.addEventListener('change', () => {
                if (preset && preset.value !== 'custom') {
                    preset.value = 'custom';
                }
            });
        });

        form.addEventListener('submit', () => { submitted = true; });
    }

    /* ---- Print --------------------------------------------------------- */
    document.querySelectorAll('[data-report-print]').forEach((button) => {
        button.addEventListener('click', () => window.print());
    });
})();
