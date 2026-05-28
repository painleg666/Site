namespace MyBlazorSite.Data;

public class RepairPriceItem
{
    public int Id { get; set; }

    public string PartName { get; set; } = "";
    public string RepairType { get; set; } = "";

    public decimal BasePrice { get; set; }
}