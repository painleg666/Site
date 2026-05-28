using Microsoft.EntityFrameworkCore;

namespace MyBlazorSite.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<RepairPriceItem> RepairPriceItems => Set<RepairPriceItem>();
    public DbSet<RepairRequest> RepairRequests => Set<RepairRequest>();
    public DbSet<CarModelGeneration> CarModelGenerations => Set<CarModelGeneration>();
}