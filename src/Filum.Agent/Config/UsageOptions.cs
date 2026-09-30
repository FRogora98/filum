namespace Filum.Agent;

public sealed class UsageOptions
{
    public const string SectionName = "Usage";

    public decimal MonthlyBudgetUsd { get; set; } = 10m;
}
