namespace SmartEnergyExpert.Client.Entities;

public sealed class Dataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "simulation";
    public string SourceSystem { get; set; } = "synthetic";
    public string Version { get; set; } = "v1";
    public DateTimeOffset TimeRangeStart { get; set; }
    public DateTimeOffset TimeRangeEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>null — системний каталог (гостьова пара ARLUT).</summary>
    public Guid? OwnerUserId { get; set; }

    public User? Owner { get; set; }

    /// <summary>Спільна демо-пара ARLUT (гість + аналітик для порівняння).</summary>
    public bool IsGuestCatalog { get; set; }

    public ICollection<AcousticSample> Samples { get; set; } = new List<AcousticSample>();
}
