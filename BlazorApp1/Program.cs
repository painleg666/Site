using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MyBlazorSite.Data;
using MyBlazorSite.Components;
using MyBlazorSite.Services;
using QuestPDF.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<AppUserState>();
builder.Services.AddScoped<DashboardStatsService>();
builder.Services.AddScoped<DashboardStatsNotifier>();
builder.Services.AddScoped<PendingAiDamageState>();
builder.Services.AddHttpClient<GeminiDamageAssessmentService>();
QuestPDF.Settings.License = LicenseType.Community;
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 20 * 1024 * 1024;
});
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.WebHost.UseUrls("http://0.0.0.0:5063");

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.RepairPriceItems.Any())
    {
        db.RepairPriceItems.AddRange(
            new RepairPriceItem { PartName = "Передний бампер", RepairType = "Ремонт", BasePrice = 8000 },
            new RepairPriceItem { PartName = "Передний бампер", RepairType = "Покраска", BasePrice = 10000 },
            new RepairPriceItem { PartName = "Передний бампер", RepairType = "Замена", BasePrice = 18000 },

            new RepairPriceItem { PartName = "Задний бампер", RepairType = "Ремонт", BasePrice = 8000 },
            new RepairPriceItem { PartName = "Задний бампер", RepairType = "Покраска", BasePrice = 10000 },
            new RepairPriceItem { PartName = "Задний бампер", RepairType = "Замена", BasePrice = 18000 },

            new RepairPriceItem { PartName = "Капот", RepairType = "Ремонт", BasePrice = 12000 },
            new RepairPriceItem { PartName = "Капот", RepairType = "Покраска", BasePrice = 15000 },
            new RepairPriceItem { PartName = "Капот", RepairType = "Замена", BasePrice = 30000 },

            new RepairPriceItem { PartName = "Дверь", RepairType = "Ремонт", BasePrice = 10000 },
            new RepairPriceItem { PartName = "Дверь", RepairType = "Покраска", BasePrice = 12000 },
            new RepairPriceItem { PartName = "Дверь", RepairType = "Замена", BasePrice = 25000 },

            new RepairPriceItem { PartName = "Крыло", RepairType = "Ремонт", BasePrice = 9000 },
            new RepairPriceItem { PartName = "Крыло", RepairType = "Покраска", BasePrice = 11000 },
            new RepairPriceItem { PartName = "Крыло", RepairType = "Замена", BasePrice = 20000 },

            new RepairPriceItem { PartName = "Фара", RepairType = "Ремонт", BasePrice = 5000 },
            new RepairPriceItem { PartName = "Фара", RepairType = "Замена", BasePrice = 15000 },

            new RepairPriceItem { PartName = "Крышка багажника", RepairType = "Ремонт", BasePrice = 11000 },
            new RepairPriceItem { PartName = "Крышка багажника", RepairType = "Покраска", BasePrice = 14000 },
            new RepairPriceItem { PartName = "Крышка багажника", RepairType = "Замена", BasePrice = 28000 },

            new RepairPriceItem { PartName = "Порог", RepairType = "Ремонт", BasePrice = 9000 },
            new RepairPriceItem { PartName = "Порог", RepairType = "Покраска", BasePrice = 10000 },
            new RepairPriceItem { PartName = "Порог", RepairType = "Замена", BasePrice = 22000 }
        );

        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapPost("/api/upload-photo", async (HttpRequest request, IWebHostEnvironment env) =>
{
    var form = await request.ReadFormAsync();
    var file = form.Files["photo"];

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Файл не выбран." });
    }

    if (file.Length > 3 * 1024 * 1024)
    {
        return Results.BadRequest(new { error = "Размер фото не должен превышать 3 МБ." });
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

    if (!allowedExtensions.Contains(extension))
    {
        return Results.BadRequest(new { error = "Можно загрузить только JPG, PNG или WEBP." });
    }

    var webRootPath = env.WebRootPath;

    if (string.IsNullOrWhiteSpace(webRootPath))
    {
        webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    var uploadsFolder = Path.Combine(webRootPath, "uploads");
    Directory.CreateDirectory(uploadsFolder);

    var fileName = $"{Guid.NewGuid()}{extension}";
    var filePath = Path.Combine(uploadsFolder, fileName);

    await using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);

    var publicPath = $"/uploads/{fileName}";

    return Results.Ok(new { path = publicPath });
})
.DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
