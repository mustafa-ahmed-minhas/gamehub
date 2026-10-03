(() => {
    'use strict';
    const shell = document.querySelector('[data-report-filter-shell]');
    const toggle = shell?.querySelector('[data-report-filter-toggle]');
    const panel = shell?.querySelector('[data-report-filter-panel]');
    toggle?.addEventListener('click', () => {
        const open = shell.classList.toggle('open');
        toggle.setAttribute('aria-expanded', String(open));
        if (open) panel?.querySelector('input,select')?.focus();
    });
    shell?.querySelector('[data-report-filter-form]')?.addEventListener('submit', () => shell.classList.add('is-loading'));

    const source = document.getElementById('reportChartData');
    if (source && window.Chart) {
        try {
            const charts = JSON.parse(source.textContent || '[]');
            charts.forEach((item) => {
                const canvas = document.getElementById(item.Id);
                if (!canvas) return;
                const isDoughnut = item.Type === 'doughnut';
                const colors = item.Colors?.length ? item.Colors : ['#d6a652', '#171614', '#4f8b6d', '#c77b4f'];
                new Chart(canvas, {
                    type: item.Type,
                    data: { labels: item.Labels, datasets: [{ data: item.Values, borderColor: isDoughnut ? '#fff' : '#c6923a', backgroundColor: isDoughnut ? colors : 'rgba(214,166,82,.28)', borderWidth: isDoughnut ? 2 : 2, borderRadius: 5, tension: .34, fill: item.Type === 'line' }] },
                    options: { responsive: true, maintainAspectRatio: false, animation: { duration: 650 }, plugins: { legend: { display: isDoughnut, position: 'bottom', labels: { usePointStyle: true, boxWidth: 7, color: '#69635b', font: { family: 'Manrope', size: 10 } } }, tooltip: { callbacks: item.Currency ? { label: (ctx) => `${ctx.label || ''}: ${ctx.raw.toLocaleString()}` } : {} } }, scales: isDoughnut ? {} : { x: { grid: { display: false }, ticks: { color: '#827c73', maxRotation: 0, autoSkip: true, font: { size: 9 } } }, y: { beginAtZero: true, grid: { color: 'rgba(30,26,20,.06)' }, ticks: { color: '#827c73', font: { size: 9 } } } } }
                });
            });
        } catch (error) { console.warn('Reports charts could not be initialized.', error); }
    }
    document.querySelector('[data-report-print]')?.addEventListener('click', () => window.print());
})();
