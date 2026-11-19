/* ==========================================================================
   HR Module — HR Dashboard (Screen 1)
   Renders the dashboard charts from the server-serialized payload in
   #hrDashboardChartData. Every value is aggregated in HRController; this file
   only presents them with the shared HR Reports visual language.
   ========================================================================== */
(() => {
    'use strict';
    if (window.__gameHubHrDashboardInitialized) return;
    window.__gameHubHrDashboardInitialized = true;

    const source = document.getElementById('hrDashboardChartData');
    if (!source || !window.Chart) return;

    const fallbackColors = ['#d6a652', '#171614', '#4f8b6d', '#c77b4f', '#7b8494', '#ead09a', '#8e6640', '#9d4f4f'];
    const axisText = '#827c73';
    const gridColor = 'rgba(30,26,20,.06)';

    Chart.defaults.font.family = "'Manrope', system-ui, sans-serif";

    const toRgba = (hex, alpha) => {
        const value = String(hex || '#c6923a').replace('#', '');
        const red = parseInt(value.substring(0, 2), 16);
        const green = parseInt(value.substring(2, 4), 16);
        const blue = parseInt(value.substring(4, 6), 16);
        return `rgba(${red}, ${green}, ${blue}, ${alpha})`;
    };

    const countAxis = {
        beginAtZero: true,
        grid: { color: gridColor },
        border: { display: false },
        ticks: { color: axisText, precision: 0, font: { size: 9.5 } }
    };

    const categoryAxis = {
        grid: { display: false },
        border: { display: false },
        ticks: { color: axisText, maxRotation: 0, autoSkip: true, font: { size: 9.5 } }
    };

    let charts = [];
    try {
        charts = JSON.parse(source.textContent || '[]');
    } catch (error) {
        console.warn('HR dashboard chart payload could not be parsed.', error);
        return;
    }

    charts.forEach((item) => {
        const canvas = document.getElementById(item.Id);
        if (!canvas) return;

        const isDoughnut = item.Type === 'doughnut';
        const colors = item.Colors && item.Colors.length ? item.Colors : fallbackColors;
        const hideLegend = canvas.dataset.htmlLegend === 'true' || !item.ShowLegend;

        const datasets = (item.Series || []).map((series) => {
            const seriesType = series.Type || item.Type;
            const seriesColor = series.Color || '#c6923a';

            if (seriesType === 'doughnut') {
                return {
                    label: series.Label || item.Title || '',
                    type: 'doughnut',
                    data: series.Values,
                    backgroundColor: colors,
                    borderColor: '#fff',
                    borderWidth: 2,
                    hoverOffset: 5
                };
            }

            if (seriesType === 'line') {
                return {
                    label: series.Label || '',
                    type: 'line',
                    data: series.Values,
                    borderColor: seriesColor,
                    backgroundColor: series.Fill ? toRgba(seriesColor, 0.16) : 'transparent',
                    borderWidth: 2,
                    tension: 0.34,
                    fill: Boolean(series.Fill),
                    pointRadius: 3,
                    pointHoverRadius: 4,
                    pointBackgroundColor: seriesColor,
                    pointBorderColor: '#fff',
                    pointBorderWidth: 1.5
                };
            }

            return {
                label: series.Label || '',
                type: 'bar',
                data: series.Values,
                backgroundColor: toRgba(seriesColor, 0.9),
                borderColor: seriesColor,
                borderWidth: 0,
                borderRadius: 6,
                maxBarThickness: 34,
                hoverBackgroundColor: seriesColor
            };
        });

        const horizontal = !isDoughnut && item.Horizontal;

        new Chart(canvas, {
            type: item.Type,
            data: { labels: item.Labels, datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 650 },
                cutout: isDoughnut ? '68%' : undefined,
                interaction: isDoughnut
                    ? { mode: 'nearest', intersect: true }
                    : { mode: 'index', intersect: false },
                plugins: {
                    legend: {
                        display: !isDoughnut && !hideLegend,
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            boxWidth: 7,
                            boxHeight: 7,
                            color: '#69635b',
                            font: { family: 'Manrope', size: 10, weight: '700' }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(23,22,20,.94)',
                        padding: 10,
                        cornerRadius: 10,
                        titleFont: { family: 'Manrope', size: 11, weight: '800' },
                        bodyFont: { family: 'Manrope', size: 11, weight: '600' },
                        displayColors: isDoughnut,
                        callbacks: {
                            label: (context) => {
                                const value = Number(context.raw || 0);
                                if (isDoughnut) {
                                    const total = (context.dataset.data || []).reduce((sum, entry) => sum + Number(entry || 0), 0);
                                    const share = total > 0 ? Math.round(value * 1000 / total) / 10 : 0;
                                    return ` ${value} (${share}%)`;
                                }

                                return ` ${context.dataset.label || 'Total'}: ${value.toLocaleString()}`;
                            }
                        }
                    }
                },
                scales: isDoughnut
                    ? {}
                    : horizontal
                        ? { x: countAxis, y: categoryAxis }
                        : { x: categoryAxis, y: countAxis }
            }
        });
    });
})();
