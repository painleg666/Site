let statusChartInstance = null;

function getChartTheme() {
    const isLight = document.body.classList.contains("light-theme");

    if (isLight) {
        return {
            textColor: "#102033",
            mutedTextColor: "#475569",
            gridColor: "rgba(15, 23, 42, 0.10)",
            borderColor: "rgba(31, 70, 130, 0.16)",
            chartBackground: "rgba(255, 255, 255, 0.06)",
            tooltipBackground: "rgba(255, 255, 255, 0.96)",
            tooltipTitleColor: "#102033",
            tooltipBodyColor: "#334155"
        };
    }

    return {
        textColor: "#d8e8ff",
        mutedTextColor: "#9fb3d9",
        gridColor: "rgba(255,255,255,0.08)",
        borderColor: "rgba(120,160,255,0.18)",
        chartBackground: "rgba(7,19,38,0.92)",
        tooltipBackground: "rgba(6,19,38,0.96)",
        tooltipTitleColor: "#ffffff",
        tooltipBodyColor: "#d8e8ff"
    };
}

const chartBackgroundPlugin = {
    id: "chartBackground",
    beforeDraw: function (chart) {
        const theme = getChartTheme();
        const ctx = chart.ctx;
        const width = chart.width;
        const height = chart.height;

        ctx.save();
        ctx.fillStyle = theme.chartBackground;
        ctx.fillRect(0, 0, width, height);
        ctx.restore();
    }
};

function normalizeStatusValues(values) {
    if (!Array.isArray(values)) {
        return [0, 0, 0, 0];
    }

    return [
        Number(values[0]) || 0,
        Number(values[1]) || 0,
        Number(values[2]) || 0,
        Number(values[3]) || 0
    ];
}

function renderStatusChartInternal(canvasId, values) {
    const canvas = document.getElementById(canvasId);

    if (!canvas) {
        return;
    }

    if (typeof Chart === "undefined") {
        console.error("Chart.js is not loaded.");
        return;
    }

    const theme = getChartTheme();
    const safeValues = normalizeStatusValues(values);

    if (statusChartInstance !== null) {
        statusChartInstance.destroy();
        statusChartInstance = null;
    }

    const ctx = canvas.getContext("2d");

    statusChartInstance = new Chart(ctx, {
        type: "bar",
        plugins: [chartBackgroundPlugin],
        data: {
            labels: ["Новая", "В обработке", "Оценена", "Завершена"],
            datasets: [
                {
                    label: "Количество заявок",
                    data: safeValues,
                    backgroundColor: [
                        "rgba(30, 139, 255, 0.85)",
                        "rgba(251, 191, 36, 0.85)",
                        "rgba(50, 213, 131, 0.85)",
                        "rgba(148, 163, 184, 0.85)"
                    ],
                    borderColor: [
                        "#1e8bff",
                        "#fbbf24",
                        "#32d583",
                        "#94a3b8"
                    ],
                    borderWidth: 1,
                    borderRadius: 10,
                    borderSkipped: false,
                    maxBarThickness: 80
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: {
                duration: 500,
                easing: "easeOutQuart"
            },
            layout: {
                padding: {
                    top: 18,
                    right: 22,
                    bottom: 12,
                    left: 14
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: "top",
                    labels: {
                        color: theme.textColor,
                        font: {
                            size: 13,
                            weight: "600"
                        },
                        boxWidth: 14,
                        boxHeight: 14,
                        padding: 18
                    }
                },
                tooltip: {
                    enabled: true,
                    backgroundColor: theme.tooltipBackground,
                    titleColor: theme.tooltipTitleColor,
                    bodyColor: theme.tooltipBodyColor,
                    borderColor: theme.borderColor,
                    borderWidth: 1,
                    cornerRadius: 12,
                    padding: 12,
                    displayColors: true,
                    titleFont: {
                        size: 13,
                        weight: "700"
                    },
                    bodyFont: {
                        size: 13
                    }
                }
            },
            scales: {
                x: {
                    ticks: {
                        color: theme.mutedTextColor,
                        font: {
                            size: 12,
                            weight: "600"
                        }
                    },
                    grid: {
                        color: document.body.classList.contains("light-theme")
                            ? "rgba(15, 23, 42, 0.06)"
                            : "rgba(255,255,255,0.04)",
                        drawBorder: false
                    },
                    border: {
                        color: theme.borderColor
                    }
                },
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: theme.mutedTextColor,
                        precision: 0,
                        stepSize: 1,
                        font: {
                            size: 12,
                            weight: "600"
                        }
                    },
                    grid: {
                        color: theme.gridColor,
                        drawBorder: false
                    },
                    border: {
                        color: theme.borderColor
                    }
                }
            }
        }
    });
}

window.dashboardCharts = window.dashboardCharts || {};

window.dashboardCharts.renderStatusChart = function (canvasId, values) {
    renderStatusChartInternal(canvasId, values);
};

window.renderStatusChart = function (newCount, inProgressCount, estimatedCount, completedCount) {
    renderStatusChartInternal("statusChart", [
        newCount,
        inProgressCount,
        estimatedCount,
        completedCount
    ]);
};

window.downloadFileFromBase64 = function (fileName, base64Data) {
    const link = document.createElement("a");
    link.download = fileName;
    link.href = "data:application/pdf;base64," + base64Data;

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.toggleTheme = function (isDark) {
    if (isDark) {
        document.body.classList.remove("light-theme");
        document.body.classList.add("dark-theme");
    } else {
        document.body.classList.remove("dark-theme");
        document.body.classList.add("light-theme");
    }

    if (statusChartInstance !== null) {
        statusChartInstance.update();
    }
};

window.scrollToElementById = function (elementId) {
    const element = document.getElementById(elementId);

    if (!element) {
        return;
    }

    element.scrollIntoView({
        behavior: "smooth",
        block: "start"
    });
};