namespace FundTrack.Application.Dtos.Organization;

public class PeriodRequestDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>Si no se manda, se usa el día 15 del mes siguiente.</summary>
    public DateOnly? DueDate { get; set; }
}
