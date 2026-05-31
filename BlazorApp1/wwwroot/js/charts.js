let statusChartInstance = null;

const chartDarkTheme = {
    textColor: "#d8e8ff",
    mutedTextColor: "#9fb3d9",
    gridColor: "rgba(255,255,255,0.08)",
    borderColor: "rgba(120,160,255,0.18)",
    chartBackground: "rgba(7,19,38,0.92)",
    tooltipBackground: "rgba(6,19,38,0.96)"
};

const darkChartBackgroundPlugin = {
    id: "darkChartBackground",
    beforeDraw: function (chart) {
        const ctx = chart.ctx;
        const width = chart.width;
        const height = chart.height;

        ctx.save();
        ctx.fillStyle = chartDarkTheme.chartBackground;
        ctx.fillRect(0, 0, width, height);
        ctx.restore();
    }
};

window.renderStatusChart = function (newCount, inProgressCount, estimatedCount, completedCount) {
    const ctx = document.getElementById("statusChart");

    if (!ctx) {
        return;
    }

    if (statusChartInstance !== null) {
        statusChartInstance.destroy();
    }

    statusChartInstance = new Chart(ctx, {
        type: "bar",
        plugins: [darkChartBackgroundPlugin],
        data: {
            labels: ["Новая", "В обработке", "Оценена", "Завершена"],
            datasets: [
                {
                    label: "Количество заявок",
                    data: [newCount, inProgressCount, estimatedCount, completedCount],
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
                duration: 700,
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
                        color: chartDarkTheme.textColor,
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
                    backgroundColor: chartDarkTheme.tooltipBackground,
                    titleColor: "#ffffff",
                    bodyColor: chartDarkTheme.textColor,
                    borderColor: chartDarkTheme.borderColor,
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
                        color: chartDarkTheme.mutedTextColor,
                        font: {
                            size: 12,
                            weight: "600"
                        }
                    },
                    grid: {
                        color: "rgba(255,255,255,0.04)",
                        drawBorder: false
                    },
                    border: {
                        color: chartDarkTheme.borderColor
                    }
                },
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: chartDarkTheme.mutedTextColor,
                        precision: 0,
                        font: {
                            size: 12,
                            weight: "600"
                        }
                    },
                    grid: {
                        color: chartDarkTheme.gridColor,
                        drawBorder: false
                    },
                    border: {
                        color: chartDarkTheme.borderColor
                    }
                }
            }
        }
    });
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