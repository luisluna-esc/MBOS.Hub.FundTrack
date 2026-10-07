using System.Net;
using FundTrack.Application.Dtos.Organization;
using FundTrack.Application.Dtos.StoredProcedures;
using FundTrack.Application.Interfaces.Organization;
using FundTrack.core.Configuration;
using FundTrack.core.Entities.Funds;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class DistrictService : IDistrictService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<DistrictRequestDto> _validator;
    private readonly IValidator<DistrictPastorAssignRequestDto> _assignValidator;

    public DistrictService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<DistrictRequestDto> validator,
        IValidator<DistrictPastorAssignRequestDto> assignValidator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _assignValidator = assignValidator;
    }

    private const string GetDistrictsSql = """
        SELECT
            district_id AS "DistrictId",
            region_id AS "RegionId",
            region_name AS "RegionName",
            name AS "Name",
            is_missionary AS "IsMissionary",
            is_active AS "IsActive",
            created_at AS "CreatedAt",
            current_pastor_id AS "CurrentPastorId",
            current_pastor_name AS "CurrentPastorName",
            church_count AS "ChurchCount",
            total_records AS "TotalRecords"
        FROM sp_get_districts(
            {0}::bigint, {1}::bigint, {2}::boolean, {3}::boolean, {4}::varchar, {5}::int, {6}::int
        )
        """;

    public async Task<ResponseGetObject> GetAllDistricts(PaginationQueryFilter paginationQueryFilter, DistrictQueryFilter districtQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Una sola consulta (región, pastor vigente y conteo de iglesias en joins) via
        // sp_get_districts (Scrips/storedProcedures.sql), igual que CheckInvoice.
        var rows = await _unitOfWork.SqlQueryAsync<DistrictGetAllRow>(
            GetDistrictsSql,
            (object?)districtQueryFilter.DistrictId ?? DBNull.Value,
            (object?)districtQueryFilter.RegionId ?? DBNull.Value,
            (object?)districtQueryFilter.IsMissionary ?? DBNull.Value,
            (object?)districtQueryFilter.IsActive ?? DBNull.Value,
            (object?)paginationQueryFilter.SearchCriteria ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        return new ResponseGetObject
        {
            Data = new PagedResult<DistrictDto>
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

    public async Task<ResponsePost> InsertDistrict(DistrictRequestDto districtRequestDto)
    {
        var errors = await ValidateAsync(districtRequestDto, null);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var district = new District
        {
            RegionId = districtRequestDto.RegionId,
            Name = districtRequestDto.Name.Trim(),
            IsMissionary = districtRequestDto.IsMissionary,
            IsActive = districtRequestDto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<District>().AddAsync(district);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = district.DistrictId,
            Messages = [new Message { Type = MessageType.Success, Description = "District created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateDistrict(long id, DistrictRequestDto districtRequestDto)
    {
        var errors = await ValidateAsync(districtRequestDto, id);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<District>();
        var district = await repository.GetByIdAsync(id);

        if (district is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "District not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        district.RegionId = districtRequestDto.RegionId;
        district.Name = districtRequestDto.Name.Trim();
        district.IsMissionary = districtRequestDto.IsMissionary;
        district.IsActive = districtRequestDto.IsActive;

        repository.Update(district);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = district.DistrictId,
            Messages = [new Message { Type = MessageType.Success, Description = "District updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteDistrict(long id)
    {
        var repository = _unitOfWork.Repository<District>();
        var district = await repository.GetByIdAsync(id);

        if (district is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "District not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var hasRelatedRows =
            await _unitOfWork.Repository<Church>().Query().AnyAsync(c => c.DistrictId == id) ||
            await _unitOfWork.Repository<DistrictPastor>().Query().AnyAsync(dp => dp.DistrictId == id) ||
            await _unitOfWork.Repository<LocalFundCount>().Query().AnyAsync(lfc => lfc.DistrictId == id);

        if (hasRelatedRows)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Este distrito tiene iglesias, pastores o arqueos asociados y no se puede eliminar. Desactívalo en su lugar." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        repository.Remove(district);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = district.DistrictId,
            Messages = [new Message { Type = MessageType.Success, Description = "District deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetPastorHistory(long districtId)
    {
        var district = await _unitOfWork.Repository<District>().GetByIdAsync(districtId);
        if (district is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "District not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var assignments = await (
            from dp in _unitOfWork.Repository<DistrictPastor>().Query()
            join p in _unitOfWork.Repository<Pastor>().Query() on dp.PastorId equals p.PastorId
            where dp.DistrictId == districtId
            orderby dp.StartDate descending
            select new { dp, p.LastNames, p.FirstNames }
        ).ToListAsync();

        return new ResponseGetObject
        {
            Data = assignments.Select(a => new DistrictPastorDto
            {
                DistrictPastorId = a.dp.DistrictPastorId,
                DistrictId = a.dp.DistrictId,
                DistrictName = district.Name,
                PastorId = a.dp.PastorId,
                PastorName = PastorService.FormatFullName(a.LastNames, a.FirstNames),
                StartDate = a.dp.StartDate,
                EndDate = a.dp.EndDate,
                IsCurrent = a.dp.EndDate == null
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> AssignPastor(long districtId, DistrictPastorAssignRequestDto assignRequestDto)
    {
        var validationResult = await _assignValidator.ValidateAsync(assignRequestDto);
        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var district = await _unitOfWork.Repository<District>().GetByIdAsync(districtId);
        if (district is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "District not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var pastor = await _unitOfWork.Repository<Pastor>().GetByIdAsync(assignRequestDto.PastorId);
        if (pastor is null || !pastor.IsActive)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Pastor not found or inactive." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<DistrictPastor>();

        // Un distrito tiene un solo pastor y un pastor un solo distrito a la vez: se cierran
        // las dos asignaciones vigentes (la del distrito y la del pastor) el día anterior al
        // inicio de la nueva, así el historial dice qué pastor tuvo cada distrito cada mes.
        var openAssignments = await repository.Query()
            .Where(dp => dp.EndDate == null && (dp.DistrictId == districtId || dp.PastorId == assignRequestDto.PastorId))
            .ToListAsync();

        if (openAssignments.Any(dp => dp.DistrictId == districtId && dp.PastorId == assignRequestDto.PastorId))
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Este pastor ya está a cargo de este distrito." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        if (openAssignments.Any(dp => dp.StartDate >= assignRequestDto.StartDate))
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "La fecha de inicio debe ser posterior al inicio de la asignación vigente." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        foreach (var openAssignment in openAssignments)
        {
            openAssignment.EndDate = assignRequestDto.StartDate.AddDays(-1);
            repository.Update(openAssignment);
        }

        // Se guarda el cierre antes del alta: los índices únicos parciales no permiten
        // que convivan dos asignaciones abiertas ni por un instante.
        await _unitOfWork.SaveChangesAsync();

        var newAssignment = new DistrictPastor
        {
            DistrictId = districtId,
            PastorId = assignRequestDto.PastorId,
            StartDate = assignRequestDto.StartDate
        };

        await repository.AddAsync(newAssignment);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = newAssignment.DistrictPastorId,
            Messages = [new Message { Type = MessageType.Success, Description = "Pastor assigned to district successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    private async Task<List<Message>> ValidateAsync(DistrictRequestDto districtRequestDto, long? id)
    {
        var validationResult = await _validator.ValidateAsync(districtRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (districtRequestDto.RegionId > 0 &&
            !await _unitOfWork.Repository<Region>().Query().AnyAsync(r => r.RegionId == districtRequestDto.RegionId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "RegionId does not reference an existing region." });
        }

        var name = districtRequestDto.Name.Trim();
        if (await _unitOfWork.Repository<District>().Query()
                .AnyAsync(d => d.RegionId == districtRequestDto.RegionId && d.Name == name && d.DistrictId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "A district with this name already exists in the region." });
        }

        return errors;
    }
}
