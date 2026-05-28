let statusChartInstance = null;

window.renderStatusChart = function (newCount, inProgressCount, estimatedCount, completedCount) {
    const ctx = document.getElementById('statusChart');

    if (!ctx) {
        return;
    }

    if (statusChartInstance !== null) {
        statusChartInstance.destroy();
    }

    statusChartInstance = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: ['Новая', 'В обработке', 'Оценена', 'Завершена'],
            datasets: [{
                label: 'Количество заявок',
                data: [newCount, inProgressCount, estimatedCount, completedCount],
                backgroundColor: [
                    '#0d6efd',
                    '#ffc107',
                    '#198754',
                    '#6c757d'
                ]
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        precision: 0
                    }
                }
            }
        }
    });
};
window.downloadFileFromBase64 = function (fileName, base64Data) {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = 'data:application/pdf;base64,' + base64Data;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};  

window.toggleTheme = function (isDark) {

    if (isDark) {
        document.body.classList.remove("light-theme");
        document.body.classList.add("dark-theme");
    }
    else {
        document.body.classList.remove("dark-theme");
        document.body.classList.add("light-theme");
    }
}