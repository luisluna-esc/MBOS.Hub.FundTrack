using System.Net;
using FundTrack.Application.Dtos.Funds;
using FundTrack.Application.Dtos.StoredProcedures;
using FundTrack.Application.Interfaces.Funds;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Funds;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Funds;
using FundTrack.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class LocalFundCountService : ILocalFundCountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<LocalFundCountRequestDto> _validator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDataScopeService _dataScopeService;
    private readonly IFileStorage _fileStorage;

    public LocalFundCountService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<LocalFundCountRequestDto> validator,
        ICurrentUserService currentUserService,
        IDataScopeService dataScopeService,
        IFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
        _dataScopeService = dataScopeService;
        _fileStorage = fileStorage;
    }

    private const string GetLocalFundCountsSql = """
        SELECT
            local_fund_count_id AS "LocalFundCountId",
            church_id AS "ChurchId",
            church_code AS "ChurchCode",
            church_name AS "ChurchName",
            period_id AS "PeriodId",
            period_year AS "PeriodYear",
            period_month AS "PeriodMonth",
            period_status AS "PeriodStatus",
            district_id AS "DistrictId",
            district_name AS "DistrictName",
            region_id AS "RegionId",
            region_name AS "RegionName",
            pastor_id AS "PastorId",
            pastor_name AS "PastorName",
            church_type AS "ChurchType",
            submission_status AS "SubmissionStatus",
            acms_balance AS "AcmsBalance",
            cash_amount AS "CashAmount",
            bank_amount AS "BankAmount",
            total_amount AS "TotalAmount",
            difference AS "Difference",
            difference_status AS "DifferenceStatus",
            count_date AS "CountDate",
            treasurer_name AS "TreasurerName",
            elder_name AS "ElderName",
            signed_by_pastor AS "SignedByPastor",
            signed_by_elder AS "SignedByElder",
            observation AS "Observation",
            attachment_count AS "AttachmentCount",
            correction_count AS "CorrectionCount",
            created_by_id AS "CreatedById",
            created_by_full_name AS "CreatedByFullName",
            created_at AS "CreatedAt",
            updated_by_id AS "UpdatedById",
            updated_by_full_name AS "UpdatedByFullName",
            updated_at AS "UpdatedAt",
            total_records AS "TotalRecords"
        FROM sp_get_local_fund_counts(
            {0}::bigint, {1}::bigint, {2}::int, {3}::bigint, {4}::bigint, {5}::bigint, {6}::bigint,
            {7}::varchar, {8}::varchar, {9}::varchar, {10}::bigint, {11}::int, {12}::int
        )
        """;

    private const string GetPeriodCountsSql = """
        SELECT
            church_id AS "ChurchId",
            church_code AS "ChurchCode",
            church_name AS "ChurchName",
            church_type AS "ChurchType",
            district_id AS "DistrictId",
            district_name AS "DistrictName",
            region_id AS "RegionId",
            region_name AS "RegionName",
            pastor_id AS "PastorId",
            pastor_name AS "PastorName",
            period_year AS "PeriodYear",
            period_month AS "PeriodMonth",
            period_status AS "PeriodStatus",
            local_fund_count_id AS "LocalFundCountId",
            count_church_type AS "CountChurchType",
            submission_status AS "SubmissionStatus",
            acms_balance AS "AcmsBalance",
            cash_amount AS "CashAmount",
            bank_amount AS "BankAmount",
            total_amount AS "TotalAmount",
            difference AS "Difference",
            difference_status AS "DifferenceStatus",
            count_date AS "CountDate",
            treasurer_name AS "TreasurerName",
            elder_name AS "ElderName",
            signed_by_pastor AS "SignedByPastor",
            signed_by_elder AS "SignedByElder",
            observation AS "Observation",
            attachment_count AS "AttachmentCount",
            correction_count AS "CorrectionCount",
            created_by_id AS "CreatedById",
            created_by_full_name AS "CreatedByFullName",
            created_at AS "CreatedAt",
            updated_by_id AS "UpdatedById",
            updated_by_full_name AS "UpdatedByFullName",
            updated_at AS "UpdatedAt",
            filial_count AS "FilialCount"
        FROM sp_get_period_counts({0}::bigint, {1}::bigint, {2}::bigint, {3}::bigint)
        """;

    public async Task<ResponseGetObject> GetAllLocalFundCounts(PaginationQueryFilter paginationQueryFilter, LocalFundCountQueryFilter localFundCountQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Una sola consulta via sp_get_local_fund_counts (Scrips/storedProcedures.sql); antes eran
        // 9 (página, conteo, iglesias, períodos, distritos, pastores, usuarios, adjuntos, correcciones).
        var rows = await QueryLocalFundCounts(localFundCountQueryFilter, paginationQueryFilter.SearchCriteria, pageNumber, pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        return new ResponseGetObject
        {
            Data = new PagedResult<LocalFundCountDto>
            {
                Items = rows,
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetLocalFundCount(long id)
    {
        var rows = await QueryLocalFundCounts(new LocalFundCountQueryFilter { LocalFundCountId = id }, null, 1, 1);

        if (rows.Count == 0)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Local fund count not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        return new ResponseGetObject
        {
            Data = rows[0],
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetByPeriod(long periodId, long? regionId, long? districtId)
    {
        if (!await _unitOfWork.Repository<Period>().Query().AnyAsync(p => p.PeriodId == periodId))
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Period not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        // Una sola consulta via sp_get_period_counts (Scrips/storedProcedures.sql); antes eran 12.
        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();

        var rows = await _unitOfWork.SqlQueryAsync<PeriodCountGetAllRow>(
            GetPeriodCountsSql,
            periodId,
            (object?)regionId ?? DBNull.Value,
            (object?)districtId ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value);

        return new ResponseGetObject
        {
            Data = rows.Select(ToPeriodCountRow).ToList(),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<List<LocalFundCountGetAllRow>> QueryLocalFundCounts(
        LocalFundCountQueryFilter filter, string? searchCriteria, int pageNumber, int pageSize)
    {
        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();

        return await _unitOfWork.SqlQueryAsync<LocalFundCountGetAllRow>(
            GetLocalFundCountsSql,
            (object?)filter.LocalFundCountId ?? DBNull.Value,
            (object?)filter.PeriodId ?? DBNull.Value,
            (object?)filter.Year ?? DBNull.Value,
            (object?)filter.ChurchId ?? DBNull.Value,
            (object?)filter.DistrictId ?? DBNull.Value,
            (object?)filter.RegionId ?? DBNull.Value,
            (object?)filter.PastorId ?? DBNull.Value,
            (object?)filter.SubmissionStatus ?? DBNull.Value,
            (object?)filter.DifferenceStatus ?? DBNull.Value,
            (object?)searchCriteria ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value,
            pageNumber,
            pageSize);
    }

    private static PeriodCountRowDto ToPeriodCountRow(PeriodCountGetAllRow row) => new()
    {
        ChurchId = row.ChurchId,
        ChurchCode = row.ChurchCode,
        ChurchName = row.ChurchName,
        ChurchType = row.ChurchType,
        DistrictId = row.DistrictId,
        DistrictName = row.DistrictName,
        RegionId = row.RegionId,
        RegionName = row.RegionName,
        PastorName = row.PastorName,
        FilialCount = row.FilialCount,
        LocalFundCount = row.LocalFundCountId is null ? null : new LocalFundCountDto
        {
            LocalFundCountId = row.LocalFundCountId.Value,
            ChurchId = row.ChurchId,
            ChurchCode = row.ChurchCode,
            ChurchName = row.ChurchName,
            PeriodYear = row.PeriodYear,
            PeriodMonth = row.PeriodMonth,
            PeriodStatus = row.PeriodStatus,
            DistrictId = row.DistrictId,
            DistrictName = row.DistrictName,
            RegionId = row.RegionId,
            RegionName = row.RegionName,
            PastorId = row.PastorId,
            PastorName = row.PastorName,
            ChurchType = row.CountChurchType ?? row.ChurchType,
            SubmissionStatus = row.SubmissionStatus ?? string.Empty,
            AcmsBalance = row.AcmsBalance ?? 0,
            CashAmount = row.CashAmount ?? 0,
            BankAmount = row.BankAmount ?? 0,
            TotalAmount = row.TotalAmount ?? 0,
            Difference = row.Difference ?? 0,
            DifferenceStatus = row.DifferenceStatus,
            CountDate = row.CountDate,
            TreasurerName = row.TreasurerName,
            ElderName = row.ElderName,
            SignedByPastor = row.SignedByPastor ?? false,
            SignedByElder = row.SignedByElder ?? false,
            Observation = row.Observation,
            AttachmentCount = row.AttachmentCount,
            CorrectionCount = row.CorrectionCount,
            CreatedById = row.CreatedById,
            CreatedByFullName = row.CreatedByFullName,
            CreatedAt = row.CreatedAt ?? default,
            UpdatedById = row.UpdatedById,
            UpdatedByFullName = row.UpdatedByFullName,
            UpdatedAt = row.UpdatedAt
        }
    };

    public async Task<ResponsePost> InsertLocalFundCount(LocalFundCountRequestDto localFundCountRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(localFundCountRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        var church = localFundCountRequestDto.ChurchId > 0
            ? await _unitOfWork.Repository<Church>().GetByIdAsync(localFundCountRequestDto.ChurchId)
            : null;

        if (localFundCountRequestDto.ChurchId > 0 && church is null)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ChurchId does not reference an existing church." });
        }

        var period = localFundCountRequestDto.PeriodId > 0
            ? await _unitOfWork.Repository<Period>().GetByIdAsync(localFundCountRequestDto.PeriodId)
            : null;

        if (localFundCountRequestDto.PeriodId > 0 && period is null)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "PeriodId does not reference an existing period." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        // En el ACMS la Filial no tiene saldo propio: se cuenta dentro del arqueo de su madre.
        if (church!.Type == ChurchTypes.Branch)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Una Filial no tiene arqueo propio: va dentro del arqueo de su Iglesia o Grupo madre." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        if (period!.Status == PeriodStatuses.Closed)
        {
            return ClosedPeriodError(0);
        }

        if (await _unitOfWork.Repository<LocalFundCount>().Query()
                .AnyAsync(c => c.ChurchId == church!.ChurchId && c.PeriodId == period.PeriodId))
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Esta iglesia ya tiene un arqueo registrado en este período." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        // Foto del momento: el arqueo guarda el distrito, el pastor y el tipo que tenía la
        // iglesia ese mes, aunque después la iglesia cambie.
        var currentPastorId = await _unitOfWork.Repository<DistrictPastor>().Query()
            .Where(dp => dp.DistrictId == church!.DistrictId && dp.EndDate == null)
            .Select(dp => (long?)dp.PastorId)
            .FirstOrDefaultAsync();

        var count = new LocalFundCount
        {
            ChurchId = church!.ChurchId,
            PeriodId = period.PeriodId,
            DistrictId = church.DistrictId,
            PastorId = currentPastorId,
            ChurchType = church.Type,
            TreasurerName = localFundCountRequestDto.TreasurerName ?? church.TreasurerName,
            ElderName = localFundCountRequestDto.ElderName ?? church.ElderName,
            CreatedById = _currentUserService.AppUserId,
            CreatedAt = DateTime.UtcNow
        };

        ApplyAmounts(count, localFundCountRequestDto);

        await _unitOfWork.Repository<LocalFundCount>().AddAsync(count);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = count.LocalFundCountId,
            Messages = [new Message { Type = MessageType.Success, Description = "Local fund count created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateLocalFundCount(long id, LocalFundCountRequestDto localFundCountRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(localFundCountRequestDto);
        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<LocalFundCount>();
        var count = await repository.GetByIdAsync(id);

        if (count is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Local fund count not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (count.ChurchId != localFundCountRequestDto.ChurchId || count.PeriodId != localFundCountRequestDto.PeriodId)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "La iglesia y el período de un arqueo no se pueden cambiar. Elimínalo y regístralo de nuevo." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        if (await IsPeriodClosed(count.PeriodId))
        {
            return ClosedPeriodError(id);
        }

        count.TreasurerName = localFundCountRequestDto.TreasurerName ?? count.TreasurerName;
        count.ElderName = localFundCountRequestDto.ElderName ?? count.ElderName;
        count.UpdatedById = _currentUserService.AppUserId;
        count.UpdatedAt = DateTime.UtcNow;

        ApplyAmounts(count, localFundCountRequestDto);

        repository.Update(count);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = count.LocalFundCountId,
            Messages = [new Message { Type = MessageType.Success, Description = "Local fund count updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteLocalFundCount(long id)
    {
        var repository = _unitOfWork.Repository<LocalFundCount>();
        var count = await repository.GetByIdAsync(id);

        if (count is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Local fund count not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (await IsPeriodClosed(count.PeriodId))
        {
            return ClosedPeriodError(id);
        }

        // Las correcciones solo existen en períodos cerrados, así que aquí no hay; el
        // seguimiento y los adjuntos sí se borran junto con el arqueo.
        var followUpRepository = _unitOfWork.Repository<DifferenceFollowUp>();
        foreach (var followUp in await followUpRepository.Query().Where(f => f.LocalFundCountId == id).ToListAsync())
        {
            followUpRepository.Remove(followUp);
        }

        var attachmentRepository = _unitOfWork.Repository<LocalFundCountAttachment>();
        var attachments = await attachmentRepository.Query().Where(a => a.LocalFundCountId == id).ToListAsync();
        foreach (var attachment in attachments)
        {
            attachmentRepository.Remove(attachment);
        }

        repository.Remove(count);
        await _unitOfWork.SaveChangesAsync();

        foreach (var attachment in attachments)
        {
            _fileStorage.Delete(attachment.FilePath);
        }

        return new ResponsePost
        {
            Id = count.LocalFundCountId,
            Messages = [new Message { Type = MessageType.Success, Description = "Local fund count deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    /// <summary>
    /// Calcula total y diferencia en el servidor, sin redondear: el sistema registra exactamente
    /// lo que se contó. La diferencia es arqueo menos ACMS (positiva si sobra, negativa si falta).
    /// </summary>
    private static void ApplyAmounts(LocalFundCount count, LocalFundCountRequestDto request)
    {
        count.SubmissionStatus = request.SubmissionStatus;
        count.AcmsBalance = request.AcmsBalance;
        count.SignedByPastor = request.SignedByPastor;
        count.SignedByElder = request.SignedByElder;
        count.Observation = request.Observation;

        if (request.SubmissionStatus == SubmissionStatuses.NotSubmitted)
        {
            count.CashAmount = 0;
            count.BankAmount = 0;
            count.TotalAmount = 0;
            count.Difference = 0;
            count.DifferenceStatus = DifferenceStatuses.None;
            count.CountDate = null;
            return;
        }

        count.CashAmount = request.CashAmount;
        count.BankAmount = request.BankAmount;
        count.TotalAmount = request.CashAmount + request.BankAmount;
        count.Difference = count.TotalAmount - request.AcmsBalance;
        count.CountDate = request.CountDate;

        // Si la diferencia desaparece se limpia el estado; si aparece, queda pendiente. Si ya
        // estaba en seguimiento (justificada o resuelta) se respeta ese avance.
        if (count.Difference == 0)
        {
            count.DifferenceStatus = DifferenceStatuses.None;
        }
        else if (count.DifferenceStatus is null or DifferenceStatuses.None)
        {
            count.DifferenceStatus = DifferenceStatuses.Pending;
        }
    }

    private async Task<bool> IsPeriodClosed(long periodId) =>
        await _unitOfWork.Repository<Period>().Query()
            .AnyAsync(p => p.PeriodId == periodId && p.Status == PeriodStatuses.Closed);

    private static ResponsePost ClosedPeriodError(long id) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = "El período está cerrado y sus arqueos ya no se pueden modificar. Registra una corrección." }],
        StatusCode = HttpStatusCode.BadRequest
    };
}
