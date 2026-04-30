namespace SmartEnergyExpert.Api.Entities;

public sealed class AcousticSample
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public decimal FrequencyBand { get; set; }
    public decimal AmplitudeDb { get; set; }
    public decimal DepthMeters { get; set; }
    public decimal RangeMeters { get; set; }
    public decimal? SoundSpeed { get; set; }
    public decimal? NoiseLevelDb { get; set; }

    public Dataset? Dataset { get; set; }
}
