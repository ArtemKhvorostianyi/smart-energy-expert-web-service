namespace SmartEnergyExpert.Api.Entities;

public sealed class Recommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EvaluationId { get; set; }
    public string DecisionText { get; set; } = string.Empty;
    public short Priority { get; set; }
    public bool IsExpertAdjusted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Evaluation? Evaluation { get; set; }
}
