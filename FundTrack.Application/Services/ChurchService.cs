using System.Net;
using FundTrack.Application.Dtos.Organization;
using FundTrack.Application.Dtos.StoredProcedures;
using FundTrack.Application.Interfaces.Organization;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class ChurchService : IChurchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ChurchRequestDto> _validator;
    private readonly IValidator<ChurchCloseRequestDto> _closeValidator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDataScopeService _dataScopeService;

    public ChurchService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<ChurchRequestDto> validator,
        IValidator<ChurchCloseRequestDto> closeValidator,
        ICurrentUserService currentUserService,
        IDataScopeService dataScopeService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _closeValidator = closeValidator;
        _currentUserService = currentUserService;
        _dataScopeService = dataScopeService;
    }

    private const string GetChurchesSql = """
        SELECT
            church_id AS "ChurchId",
            code AS "Code",
            name AS "Name",
            type AS "Type",
            district_id AS "DistrictId",
            district_name AS "DistrictName",
            region_id AS "RegionId",
            region_name AS "RegionName",
            current_pastor_name AS "CurrentPastorName",
            address AS "Address",
            phone AS "Phone",
            treasurer_name AS "TreasurerName",
            treasurer_phone AS "TreasurerPhone",
            elder_name AS "ElderName",
            elder_phone AS "ElderPhone",
            status AS "Status",
            merged_into_church_id AS "MergedIntoChurchId",
            merged_into_church_name AS "MergedIntoChurchName",
            closed_at AS "ClosedAt",
            created_at AS "CreatedAt",
            parent_church_id AS "ParentChurchId",
            parent_church_name AS "ParentChurchName",
            filial_count AS "FilialCount",
            total_records AS "TotalRecords"
        FROM sp_get_churches(
            {0}::bigint, {1}::varchar, {2}::bigint, {3}::bigint, {4}::varchar,
            {5}::varchar, {6}::bigint, {7}::varchar, {8}::bigint, {9}::int, {10}::int
        )
        """;

    public async Task<ResponseGetObject> GetAllChurches(PaginationQueryFilter paginationQueryFilter, ChurchQueryFilter churchQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Una sola consulta (distrito, región, pastor vigente e iglesia de fusión en joins) via
        // sp_get_churches (Scrips/storedProcedures.sql). El filtro del pastor va dentro de la función.
        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();

        var rows = await _unitOfWork.SqlQueryAsync<ChurchGetAllRow>(
            GetChurchesSql,
            (object?)churchQueryFilter.ChurchId ?? DBNull.Value,
            (object?)churchQueryFilter.Code ?? DBNull.Value,
            (object?)churchQueryFilter.DistrictId ?? DBNull.Value,
            (object?)churchQueryFilter.RegionId ?? DBNull.Value,
            (object?)churchQueryFilter.Type ?? DBNull.Value,
            (object?)churchQueryFilter.Status ?? DBNull.Value,
            (object?)churchQueryFilter.ParentChurchId ?? DBNull.Value,
            (object?)paginationQueryFilter.SearchCriteria ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        return new ResponseGetObject
        {
            Data = new PagedResult<ChurchDto>
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

    public async Task<ResponsePost> InsertChurch(ChurchRequestDto churchRequestDto)
    {
        var (errors, parent) = await ValidateAsync(churchRequestDto, null);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        // Una Filial lleva siempre el código y el distrito de su madre.
        var church = new Church
        {
            Code = parent?.Code ?? churchRequestDto.Code.Trim(),
            Name = churchRequestDto.Name.Trim(),
            Type = churchRequestDto.Type,
            DistrictId = parent?.DistrictId ?? churchRequestDto.DistrictId,
            ParentChurchId = parent?.ChurchId,
            Address = churchRequestDto.Address,
            Phone = churchRequestDto.Phone,
            TreasurerName = churchRequestDto.TreasurerName,
            TreasurerPhone = churchRequestDto.TreasurerPhone,
            ElderName = churchRequestDto.ElderName,
            ElderPhone = churchRequestDto.ElderPhone,
            Status = ChurchStatuses.Active,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Church>().AddAsync(church);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = church.ChurchId,
            Messages = [new Message { Type = MessageType.Success, Description = "Church created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateChurch(long id, ChurchRequestDto churchRequestDto)
    {
        var (errors, parent) = await ValidateAsync(churchRequestDto, id);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Church>();
        var church = await repository.GetByIdAsync(id);

        if (church is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Church not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var filials = await repository.Query()
            .Where(c => c.ParentChurchId == id && c.Status == ChurchStatuses.Active)
            .ToListAsync();

        if (churchRequestDto.Type == ChurchTypes.Branch && filials.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"Esta iglesia tiene {filials.Count} filial(es) activa(s) y no puede pasar a ser Filial. Reasígnalas o ciérralas primero." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var newCode = parent?.Code ?? churchRequestDto.Code.Trim();
        var newDistrictId = parent?.DistrictId ?? churchRequestDto.DistrictId;

        // Los cambios de tipo, distrito y nombre quedan en el historial de la iglesia,
        // porque los reportes de meses pasados deben mostrar cómo era en ese momento.
        var changeDate = churchRequestDto.ChangeDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var historyRows = new List<ChurchHistory>();

        if (church.Type != churchRequestDto.Type)
        {
            historyRows.Add(NewHistory(church.ChurchId, ChurchChangeTypes.Type, church.Type, churchRequestDto.Type, changeDate, churchRequestDto.ChangeReason));
        }

        if (church.DistrictId != newDistrictId)
        {
            var districtNames = await _unitOfWork.Repository<District>().Query()
                .Where(d => d.DistrictId == church.DistrictId || d.DistrictId == newDistrictId)
                .ToDictionaryAsync(d => d.DistrictId, d => d.Name);

            historyRows.Add(NewHistory(
                church.ChurchId,
                ChurchChangeTypes.District,
                districtNames.GetValueOrDefault(church.DistrictId),
                districtNames.GetValueOrDefault(newDistrictId),
                changeDate,
                churchRequestDto.ChangeReason));

            // Las filiales están en el distrito de su madre: se mudan con ella.
            foreach (var filial in filials)
            {
                historyRows.Add(NewHistory(
                    filial.ChurchId,
                    ChurchChangeTypes.District,
                    districtNames.GetValueOrDefault(filial.DistrictId),
                    districtNames.GetValueOrDefault(newDistrictId),
                    changeDate,
                    $"Se movió con su iglesia madre {church.Name}."));
            }
        }

        var newName = churchRequestDto.Name.Trim();
        if (church.Name != newName)
        {
            historyRows.Add(NewHistory(church.ChurchId, ChurchChangeTypes.Name, church.Name, newName, changeDate, churchRequestDto.ChangeReason));
        }

        foreach (var filial in filials.Where(f => f.Code != newCode || f.DistrictId != newDistrictId))
        {
            filial.Code = newCode;
            filial.DistrictId = newDistrictId;
            repository.Update(filial);
        }

        church.Code = newCode;
        church.Name = newName;
        church.Type = churchRequestDto.Type;
        church.DistrictId = newDistrictId;
        church.ParentChurchId = parent?.ChurchId;
        church.Address = churchRequestDto.Address;
        church.Phone = churchRequestDto.Phone;
        church.TreasurerName = churchRequestDto.TreasurerName;
        church.TreasurerPhone = churchRequestDto.TreasurerPhone;
        church.ElderName = churchRequestDto.ElderName;
        church.ElderPhone = churchRequestDto.ElderPhone;

        repository.Update(church);

        foreach (var historyRow in historyRows)
        {
            await _unitOfWork.Repository<ChurchHistory>().AddAsync(historyRow);
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = church.ChurchId,
            Messages = [new Message { Type = MessageType.Success, Description = "Church updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> CloseChurch(long id, ChurchCloseRequestDto churchCloseRequestDto)
    {
        var validationResult = await _closeValidator.ValidateAsync(churchCloseRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (churchCloseRequestDto.MergedIntoChurchId == id)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "A church cannot be merged into itself." });
        }
        else if (churchCloseRequestDto.MergedIntoChurchId.HasValue &&
            !await _unitOfWork.Repository<Church>().Query().AnyAsync(c =>
                c.ChurchId == churchCloseRequestDto.MergedIntoChurchId.Value && c.Status == ChurchStatuses.Active))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "MergedIntoChurchId must reference an active church." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Church>();
        var church = await repository.GetByIdAsync(id);

        if (church is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Church not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (church.Status != ChurchStatuses.Active)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Esta iglesia ya está cerrada o fusionada." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var activeFilials = await repository.Query()
            .CountAsync(c => c.ParentChurchId == id && c.Status == ChurchStatuses.Active);

        if (activeFilials > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"Esta iglesia tiene {activeFilials} filial(es) activa(s). Ciérralas o pásalas a otra iglesia antes de cerrarla." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        await _unitOfWork.Repository<ChurchHistory>().AddAsync(NewHistory(
            church.ChurchId,
            ChurchChangeTypes.Status,
            church.Status,
            churchCloseRequestDto.Status,
            churchCloseRequestDto.ClosedAt,
            churchCloseRequestDto.Reason));

        church.Status = churchCloseRequestDto.Status;
        church.ClosedAt = churchCloseRequestDto.ClosedAt;
        church.MergedIntoChurchId = churchCloseRequestDto.Status == ChurchStatuses.Merged ? churchCloseRequestDto.MergedIntoChurchId : null;

        repository.Update(church);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = church.ChurchId,
            Messages = [new Message { Type = MessageType.Success, Description = "Church closed successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetHistory(long churchId)
    {
        if (!await _unitOfWork.Repository<Church>().Query().AnyAsync(c => c.ChurchId == churchId))
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Church not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var history = await _unitOfWork.Repository<ChurchHistory>().Query()
            .Where(h => h.ChurchId == churchId)
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.RegisteredAt)
            .ToListAsync();

        var appUserIds = history.Where(h => h.AppUserId.HasValue).Select(h => h.AppUserId!.Value).Distinct().ToList();
        var fullNameById = await _unitOfWork.Repository<AppUser>().Query()
            .Where(u => appUserIds.Contains(u.AppUserId))
            .ToDictionaryAsync(u => u.AppUserId, u => u.FullName);

        return new ResponseGetObject
        {
            Data = history.Select(h => new ChurchHistoryDto
            {
                ChurchHistoryId = h.ChurchHistoryId,
                ChurchId = h.ChurchId,
                ChangeType = h.ChangeType,
                OldValue = h.OldValue,
                NewValue = h.NewValue,
                ChangedAt = h.ChangedAt,
                Reason = h.Reason,
                AppUserId = h.AppUserId,
                AppUserFullName = h.AppUserId.HasValue ? fullNameById.GetValueOrDefault(h.AppUserId.Value) : null,
                RegisteredAt = h.RegisteredAt
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private ChurchHistory NewHistory(long churchId, string changeType, string? oldValue, string? newValue, DateOnly changedAt, string? reason) => new()
    {
        ChurchId = churchId,
        ChangeType = changeType,
        OldValue = oldValue,
        NewValue = newValue,
        ChangedAt = changedAt,
        Reason = reason,
        AppUserId = _currentUserService.AppUserId,
        RegisteredAt = DateTime.UtcNow
    };

    /// <summary>Valida la iglesia y, si es Filial, devuelve su madre (de la que toma código y distrito).</summary>
    private async Task<(List<Message> Errors, Church? Parent)> ValidateAsync(ChurchRequestDto churchRequestDto, long? id)
    {
        var validationResult = await _validator.ValidateAsync(churchRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        var repository = _unitOfWork.Repository<Church>();

        if (churchRequestDto.Type == ChurchTypes.Branch)
        {
            if (!churchRequestDto.ParentChurchId.HasValue)
                return (errors, null);

            var parent = await repository.Query().FirstOrDefaultAsync(c => c.ChurchId == churchRequestDto.ParentChurchId.Value);

            if (parent is null || parent.ChurchId == id)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "ParentChurchId does not reference another existing church." });
            }
            else if (parent.Type == ChurchTypes.Branch)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "La madre de una Filial debe ser una Iglesia o un Grupo, no otra Filial." });
            }
            else if (parent.Status != ChurchStatuses.Active)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "La iglesia madre está cerrada o fusionada." });
            }

            return (errors, errors.Count == 0 ? parent : null);
        }

        if (churchRequestDto.DistrictId > 0 &&
            !await _unitOfWork.Repository<District>().Query().AnyAsync(d => d.DistrictId == churchRequestDto.DistrictId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "DistrictId does not reference an existing district." });
        }

        // El código ACMS es único entre Iglesias y Grupos; las Filiales repiten el de su madre.
        var code = churchRequestDto.Code.Trim();
        if (await repository.Query().AnyAsync(c => c.Code == code && c.Type != ChurchTypes.Branch && c.ChurchId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "A church with this code already exists." });
        }

        return (errors, null);
    }
}
