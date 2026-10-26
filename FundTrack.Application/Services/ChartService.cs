using System.Net;
using FundTrack.Application.Dtos.Charts;
using FundTrack.Application.Dtos.Reports;
using FundTrack.Application.Interfaces.Charts;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Charts;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

// Pantalla Gráficas: una consulta (sp_chart_counts, un mes y una región por fila) que aquí se suma
// por mes y por región, más las iglesias con más faltante del año (sp_report_differences, el mismo
// del reporte de diferencias). Los meses que todavía no empiezan no se cuentan. El Pastor solo ve
// su distrito. Si se elige una iglesia, en lugar del resumen se devuelve su fondo local mes a mes
// (sp_report_church_statement, el mismo del Reporte por iglesia).
public class ChartService : IChartService
{
    private const int TopShortageCount = 10;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataScopeService _dataScopeService;

    public ChartService(IUnitOfWork unitOfWork, IDataScopeService dataScopeService)
    {
        _unitOfWork = unitOfWork;
        _dataScopeService = dataScopeService;
    }

    private const string GetChartCountsSql = """
        SELECT
            period_month AS "PeriodMonth", region_id AS "RegionId", region_name AS "RegionName",
            expected AS "Expected", no_difference AS "NoDifference", with_difference AS "WithDifference",
            not_submitted AS "NotSubmitted", missing AS "Missing", acms_submitted AS "AcmsSubmitted",
            custody_total AS "CustodyTotal", shortage AS "Shortage", surplus AS "Surplus"
        FROM sp_chart_counts({0}::int, {1}::bigint, {2}::bigint, {3}::bigint)
        """;

    public async Task<ResponseGetObject> GetCharts(ChartsQueryFilter filter)
    {
        var today = BoliviaTime.Today;
        var year = filter.Year ?? today.Year;
        if (year < 2000 || year > today.Year)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Indica un año válido." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var lastMonth = year == today.Year ? today.Month : 12;
        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();

        if (filter.ChurchId.HasValue)
        {
            return Ok(new ChartsDto
            {
                Year = year,
                Church = await GetChurchDetail(filter.ChurchId.Value, year, lastMonth, restrictedDistrictId)
            });
        }

        var rows = (await _unitOfWork.SqlQueryAsync<ChartCountsRowDto>(
                GetChartCountsSql,
                year,
                (object?)filter.RegionId ?? DBNull.Value,
                (object?)filter.DistrictId ?? DBNull.Value,
                (object?)restrictedDistrictId ?? DBNull.Value))
            .Where(r => r.PeriodMonth <= lastMonth)
            .ToList();

        var shortages = (await _unitOfWork.SqlQueryAsync<DifferencesReportRowDto>(
                ReportService.GetDifferencesSql,
                year,
                DBNull.Value,
                (object?)filter.RegionId ?? DBNull.Value,
                (object?)filter.DistrictId ?? DBNull.Value,
                (object?)restrictedDistrictId ?? DBNull.Value,
                DBNull.Value))
            .Where(r => r.Difference < 0 && r.PeriodMonth <= lastMonth)
            .ToList();

        var charts = new ChartsDto
        {
            Year = year,
            Totals = Sum(rows, new ChartCountsDto()),
            Months = rows
                .GroupBy(r => r.PeriodMonth)
                .OrderBy(g => g.Key)
                .Select(g => Sum(g, new ChartMonthDto { Month = g.Key }))
                .ToList(),
            Regions = rows
                .GroupBy(r => (r.RegionId, r.RegionName))
                .OrderBy(g => g.Key.RegionName)
                .Select(g => Sum(g, new ChartRegionDto { RegionId = g.Key.RegionId, RegionName = g.Key.RegionName }))
                .ToList(),
            TopShortages = shortages
                .GroupBy(r => (r.ChurchCode, r.ChurchName, r.DistrictName))
                .Select(g => new ChartChurchDto
                {
                    ChurchCode = g.Key.ChurchCode,
                    ChurchName = g.Key.ChurchName,
                    DistrictName = g.Key.DistrictName,
                    Months = g.Count(),
                    Shortage = g.Sum(r => r.Difference)
                })
                .OrderBy(c => c.Shortage)
                .Take(TopShortageCount)
                .ToList()
        };

        return Ok(charts);
    }

    private static ResponseGetObject Ok(ChartsDto charts) => new()
    {
        Data = charts,
        Messages = [],
        StatusCode = HttpStatusCode.OK
    };

    /// <summary>La iglesia mes a mes, de enero al último mes que ya empezó. Null si no se puede ver.</summary>
    private async Task<ChartChurchDetailDto?> GetChurchDetail(long churchId, int year, int lastMonth, long? restrictedDistrictId)
    {
        var periods = await _unitOfWork.Repository<Period>().Query()
            .Where(p => p.Year == year && p.Month <= lastMonth)
            .OrderBy(p => p.Month)
            .ToListAsync();
        if (periods.Count == 0)
            return null;

        var rows = await _unitOfWork.SqlQueryAsync<ChurchStatementReportRowDto>(
            ReportService.GetChurchStatementSql,
            churchId,
            periods[0].PeriodId,
            periods[^1].PeriodId,
            (object?)restrictedDistrictId ?? DBNull.Value);
        if (rows.Count == 0)
            return null;

        var church = rows[^1];
        return new ChartChurchDetailDto
        {
            ChurchCode = church.ChurchCode,
            ChurchName = church.ChurchName,
            ChurchType = church.ChurchType,
            DistrictName = church.DistrictName,
            RegionName = church.RegionName,
            PastorName = church.PastorName,
            Months = rows.Select(r =>
            {
                var submitted = r.HasCount && r.SubmissionStatus == SubmissionStatuses.Submitted;
                return new ChartChurchMonthDto
                {
                    Month = r.PeriodMonth,
                    Status = !r.HasCount ? "missing"
                        : !submitted ? "notSubmitted"
                        : r.Difference == 0 ? "noDifference"
                        : r.DifferenceStatus == DifferenceStatuses.Resolved ? "resolved" : "withDifference",
                    Submitted = submitted,
                    SummaryOnly = r.SummaryOnly,
                    AcmsBalance = r.AcmsBalance,
                    TotalAmount = r.TotalAmount,
                    Difference = r.Difference,
                    Observation = r.Observation
                };
            }).ToList()
        };
    }

    private static T Sum<T>(IEnumerable<ChartCountsRowDto> rows, T target) where T : ChartCountsDto
    {
        foreach (var row in rows)
        {
            target.Expected += row.Expected;
            target.NoDifference += row.NoDifference;
            target.WithDifference += row.WithDifference;
            target.NotSubmitted += row.NotSubmitted;
            target.Missing += row.Missing;
            target.AcmsSubmitted += row.AcmsSubmitted;
            target.CustodyTotal += row.CustodyTotal;
            target.Shortage += row.Shortage;
            target.Surplus += row.Surplus;
        }
        return target;
    }
}
