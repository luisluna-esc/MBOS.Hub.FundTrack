using System.Net;
using FundTrack.Application.Dtos.Dashboard;
using FundTrack.Application.Dtos.Organization;
using FundTrack.Application.Interfaces.Dashboard;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataScopeService _dataScopeService;

    public DashboardService(IUnitOfWork unitOfWork, IDataScopeService dataScopeService)
    {
        _unitOfWork = unitOfWork;
        _dataScopeService = dataScopeService;
    }

    private const string GetDashboardByRegionSql = """
        SELECT
            region_id AS "RegionId",
            region_name AS "RegionName",
            active_churches AS "ActiveChurches",
            submitted AS "Submitted",
            not_submitted AS "NotSubmitted",
            missing AS "Missing",
            differences_pending AS "DifferencesPending",
            differences_justified AS "DifferencesJustified",
            differences_resolved AS "DifferencesResolved",
            negative_acms AS "NegativeAcms",
            total_acms_balance AS "TotalAcmsBalance",
            total_counted AS "TotalCounted",
            total_difference AS "TotalDifference"
        FROM sp_get_dashboard_by_region({0}::bigint, {1}::bigint)
        """;

    public async Task<ResponseGetObject> GetSummary(long? periodId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodRepository = _unitOfWork.Repository<Period>();

        // Sin período indicado se muestra el mes en curso, o el último que exista antes de hoy.
        var period = periodId.HasValue
            ? await periodRepository.Query().FirstOrDefaultAsync(p => p.PeriodId == periodId.Value)
            : await periodRepository.Query()
                .Where(p => p.Year < today.Year || (p.Year == today.Year && p.Month <= today.Month))
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .FirstOrDefaultAsync();

        if (periodId.HasValue && period is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Period not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var overduePeriods = await periodRepository.Query()
            .Where(p => p.Status == PeriodStatuses.Open && p.DueDate < today)
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToListAsync();

        var dashboard = new DashboardDto
        {
            Period = period is null ? null : ToPeriodDto(period),
            IsOverdue = period is not null && period.Status == PeriodStatuses.Open && period.DueDate < today,
            OverduePeriods = overduePeriods.Select(ToPeriodDto).ToList()
        };

        if (period is not null)
        {
            var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();

            dashboard.Regions = await _unitOfWork.SqlQueryAsync<DashboardRegionRowDto>(
                GetDashboardByRegionSql,
                period.PeriodId,
                (object?)restrictedDistrictId ?? DBNull.Value);

            dashboard.Totals = new DashboardRegionRowDto
            {
                RegionName = "Total",
                ActiveChurches = dashboard.Regions.Sum(r => r.ActiveChurches),
                Submitted = dashboard.Regions.Sum(r => r.Submitted),
                NotSubmitted = dashboard.Regions.Sum(r => r.NotSubmitted),
                Missing = dashboard.Regions.Sum(r => r.Missing),
                DifferencesPending = dashboard.Regions.Sum(r => r.DifferencesPending),
                DifferencesJustified = dashboard.Regions.Sum(r => r.DifferencesJustified),
                DifferencesResolved = dashboard.Regions.Sum(r => r.DifferencesResolved),
                NegativeAcms = dashboard.Regions.Sum(r => r.NegativeAcms),
                TotalAcmsBalance = dashboard.Regions.Sum(r => r.TotalAcmsBalance),
                TotalCounted = dashboard.Regions.Sum(r => r.TotalCounted),
                TotalDifference = dashboard.Regions.Sum(r => r.TotalDifference)
            };
        }

        return new ResponseGetObject
        {
            Data = dashboard,
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static PeriodDto ToPeriodDto(Period period) => new()
    {
        PeriodId = period.PeriodId,
        Year = period.Year,
        Month = period.Month,
        DueDate = period.DueDate,
        Status = period.Status,
        ClosedAt = period.ClosedAt,
        ClosedById = period.ClosedById
    };
}
