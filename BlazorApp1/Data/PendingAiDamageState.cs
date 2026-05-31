namespace MyBlazorSite.Data;

public class PendingAiDamageState
{
    public List<AiDamageItem> Items { get; private set; } = new();

    public bool HasItems => Items.Count > 0;

    public void SetItems(IEnumerable<AiDamageItem> items)
    {
        Items = items
            .Where(x => !string.IsNullOrWhiteSpace(x.DetectedPart))
            .Select(x => new AiDamageItem
            {
                DetectedPart = x.DetectedPart,
                DamageType = x.DamageType,
                RepairType = x.RepairType,
                DamageLevel = x.DamageLevel,
                Confidence = x.Confidence,
                EstimatedCost = x.EstimatedCost,
                NeedsHumanReview = x.NeedsHumanReview,
                Comment = x.Comment
            })
            .ToList();
    }

    public List<AiDamageItem> TakeItems()
    {
        var copy = Items.ToList();
        Items.Clear();
        return copy;
    }

    public void Clear()
    {
        Items.Clear();
    }
}