namespace MyBlazorSite.Data;

public class AiDamageResult
{
    public string DetectedPart { get; set; } = "";
    public string DamageType { get; set; } = "";
    public string RepairType { get; set; } = "";
    public string DamageLevel { get; set; } = "";

    public double Confidence { get; set; }
    public decimal EstimatedCost { get; set; }
    public bool IsCarDamagePhoto { get; set; }
    public bool NeedsHumanReview { get; set; }
    public string Comment { get; set; } = "";

    public List<AiDamageItem> Items { get; set; } = new();
}

public class AiDamageItem
{
    public string DetectedPart { get; set; } = "";
    public string DamageType { get; set; } = "";
    public string RepairType { get; set; } = "";
    public string DamageLevel { get; set; } = "";

    public double Confidence { get; set; }
    public decimal EstimatedCost { get; set; }
    public bool NeedsHumanReview { get; set; }
    public string Comment { get; set; } = "";
}