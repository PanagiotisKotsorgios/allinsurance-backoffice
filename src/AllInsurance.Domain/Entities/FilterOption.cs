namespace AllInsurance.Domain.Entities;

public sealed class FilterOption
{
    public Guid Id { get; set; }
    public required string Category { get; set; }
    public required string Value { get; set; }
    public string CompanyScope { get; set; } = "";
    public string BranchScope { get; set; } = "";
    public int SortOrder { get; set; }
}
