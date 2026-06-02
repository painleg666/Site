namespace MyBlazorSite.Data;

public class RepairRequest
{
    public int Id { get; set; }

    public string ClientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string CarGeneration { get; set; } = string.Empty;
    public string CarBrand { get; set; } = "";
    public string CarModel { get; set; } = "";
    public int CarYear { get; set; }

    public string DamagedPart { get; set; } = "";
    public string RepairType { get; set; } = "";
    public string DamageLevel { get; set; } = "";

    public decimal CalculatedCost { get; set; }
    public string? PhotoPath { get; set; }

    public string Status { get; set; } = "Новая";
    public string OwnerKey { get; set; } = "";

    public string PublicCode { get; set; } = "";
    public string? ClientUserId { get; set; }
    public string? ClientComment { get; set; }
    public string? ManagerComment { get; set; }
    public DateTime? StatusUpdatedAt { get; set; }
    public string? CreatedFromIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}