(function () {
    if (!window.Chart || window.gameHubDashboardLoaded) {
        return;
    }

    window.gameHubDashboardLoaded = true;

    var dataEl = document.getElementById("dashboard-data");
    if (!dataEl) {
        return;
    }

    var data;
    try {
        data = JSON.parse(dataEl.textContent);
    } catch (e) {
        return;
    }

    var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var defaults = Chart.defaults;
    defaults.font.family = "'Manrope', system-ui, sans-serif";
    defaults.color = "#7c7468";
    defaults.plugins.legend.display = false;
    defaults.responsive = true;
    defaults.maintainAspectRatio = false;

    var gridColor = "rgba(27, 24, 18, 0.08)";
    var gold = "#d6a950";
    var dark = "#17120a";

    var currencySymbol = data.currencySymbol || "Rs";
    var currencyFormatter = new Intl.NumberFormat("en-PK", {
        style: "currency",
        currency: "PKR",
        maximumFractionDigits: 0
    });

    var revenueCanvas = document.getElementById("revenueChart");
    if (revenueCanvas && data.revenueLabels && data.revenueData && data.revenueData.length) {
        var context = revenueCanvas.getContext("2d");
        var gradient = context.createLinearGradient(0, 0, 0, 320);
        gradient.addColorStop(0, "rgba(214, 169, 80, 0.34)");
        gradient.addColorStop(1, "rgba(214, 169, 80, 0.02)");

        new Chart(revenueCanvas, {
            type: "line",
            data: {
                labels: data.revenueLabels,
                datasets: [{
                    label: "Revenue",
                    data: data.revenueData,
                    borderColor: gold,
                    backgroundColor: gradient,
                    borderWidth: 3,
                    pointBackgroundColor: "#fffdf8",
                    pointBorderColor: gold,
                    pointBorderWidth: 3,
                    pointRadius: 4,
                    pointHoverRadius: 6,
                    tension: 0.42,
                    fill: true
                }]
            },
            options: {
                animation: reduceMotion ? false : { duration: 900 },
                interaction: { intersect: false, mode: "index" },
                scales: {
                    x: { grid: { display: false }, ticks: { font: { weight: 800 } } },
                    y: {
                        beginAtZero: true,
                        border: { display: false },
                        grid: { color: gridColor },
                        ticks: {
                            callback: function (value) { return currencySymbol + " " + (Number(value) / 1000) + "k"; },
                            font: { weight: 800 }
                        }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function (context) { return "Revenue: " + currencyFormatter.format(context.parsed.y); }
                        }
                    }
                }
            }
        });
    }

    var sportCanvas = document.getElementById("sportBookingsChart");
    if (sportCanvas && data.sportLabels && data.sportData && data.sportData.length) {
        var sportColors = [gold, dark, "#8c806e", "#cbb58a", "#6b5e4e", "#a0937e"];
        new Chart(sportCanvas, {
            type: "bar",
            data: {
                labels: data.sportLabels,
                datasets: [{
                    data: data.sportData,
                    backgroundColor: data.sportLabels.map(function (_, i) { return sportColors[i % sportColors.length]; }),
                    borderRadius: 12,
                    maxBarThickness: 42
                }]
            },
            options: {
                animation: reduceMotion ? false : { duration: 850 },
                scales: {
                    x: { grid: { display: false }, ticks: { font: { weight: 900 } } },
                    y: {
                        beginAtZero: true,
                        border: { display: false },
                        grid: { color: gridColor },
                        ticks: { precision: 0, font: { weight: 800 } }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function (context) { return context.parsed.y + " bookings"; }
                        }
                    }
                }
            }
        });
    }

    var statusCanvas = document.getElementById("bookingStatusChart");
    if (statusCanvas && data.bookingStatusLabels && data.bookingStatusData && data.bookingStatusData.length) {
        var statusColors = [gold, dark, "#8c806e", "#c95245", "#6b5e4e", "#a0937e", "#d4a85c", "#4a4338"];
        new Chart(statusCanvas, {
            type: "doughnut",
            data: {
                labels: data.bookingStatusLabels,
                datasets: [{
                    data: data.bookingStatusData,
                    backgroundColor: data.bookingStatusLabels.map(function (_, i) { return statusColors[i % statusColors.length]; }),
                    borderColor: "#fffdf8",
                    borderWidth: 6,
                    hoverOffset: 4
                }]
            },
            options: {
                animation: reduceMotion ? false : { duration: 900 },
                cutout: "68%",
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                var total = context.dataset.data.reduce(function (a, b) { return a + b; }, 0);
                                var pct = total === 0 ? 0 : Math.round(context.parsed / total * 100);
                                return context.label + ": " + pct + "%";
                            }
                        }
                    }
                }
            }
        });
    }
})();
