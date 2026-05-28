using System.ComponentModel.DataAnnotations;

namespace MyBlazorSite.Data;

public class CarModelGeneration
{
    public int Id { get; set; }

    [Required]
    public string Brand { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = string.Empty;

    [Required]
    public string Generation { get; set; } = string.Empty;

    public int StartYear { get; set; }

    public int EndYear { get; set; }
}