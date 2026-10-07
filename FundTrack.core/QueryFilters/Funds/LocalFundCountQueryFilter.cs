namespace FundTrack.core.QueryFilters.Funds;

public class LocalFundCountQueryFilter
{
    public long? LocalFundCountId { get; set; }
    public long? PeriodId { get; set; }
    public int? Year { get; set; }
    public long? ChurchId { get; set; }
    public long? DistrictId { get; set; }
    public long? RegionId { get; set; }
    public long? PastorId { get; set; }
    public string? SubmissionStatus { get; set; }
    public string? DifferenceStatus { get; set; }
}
