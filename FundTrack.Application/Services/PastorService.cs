using System.Net;
using FundTrack.Application.Dtos.Organization;
using FundTrack.Application.Dtos.StoredProcedures;
using FundTrack.Application.Interfaces.Organization;
using FundTrack.core.Configuration;
using FundTrack.core.Entities.Funds;
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

public class PastorService : IPastorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<PastorRequestDto> _validator;

    public PastorService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<PastorRequestDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    /// <summary>Mismo formato que el Excel: "APELLIDOS, Nombres".</summary>
    public static string FormatFullName(string lastNames, string firstNames) => $"{lastNames}, {firstNames}";

    private const string GetPastorsSql = """
        SELECT
            pastor_id AS "PastorId",
            ci AS "Ci",
            first_names AS "FirstNames",
            last_names AS "LastNames",
            full_name AS "FullName",
            email AS "Email",
            is_active AS "IsActive",
            created_at AS "CreatedAt",
            current_district_id AS "CurrentDistrictId",
            current_district_name AS "CurrentDistrictName",
            total_records AS "TotalRecords"
        FROM sp_get_pastors(
            {0}::bigint, {1}::varchar, {2}::boolean, {3}::varchar, {4}::int, {5}::int
        )
        """;

    public async Task<ResponseGetObject> GetAllPastors(PaginationQueryFilter paginationQueryFilter, PastorQueryFilter pastorQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Una sola consulta (distrito vigente en join) via sp_get_pastors (Scrips/storedProcedures.sql).
        var rows = await _unitOfWork.SqlQueryAsync<PastorGetAllRow>(
            GetPastorsSql,
            (object?)pastorQueryFilter.PastorId ?? DBNull.Value,
            (object?)pastorQueryFilter.Ci ?? DBNull.Value,
            (object?)pastorQueryFilter.IsActive ?? DBNull.Value,
            (object?)paginationQueryFilter.SearchCriteria ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        return new ResponseGetObject
        {
            Data = new PagedResult<PastorDto>
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

    public async Task<ResponsePost> InsertPastor(PastorRequestDto pastorRequestDto)
    {
        var errors = await ValidateAsync(pastorRequestDto, null);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var pastor = new Pastor
        {
            Ci = NormalizeCi(pastorRequestDto.Ci),
            FirstNames = pastorRequestDto.FirstNames.Trim(),
            LastNames = pastorRequestDto.LastNames.Trim(),
            Email = pastorRequestDto.Email?.Trim().ToLower(),
            IsActive = pastorRequestDto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Pastor>().AddAsync(pastor);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = pastor.PastorId,
            Messages = [new Message { Type = MessageType.Success, Description = "Pastor created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdatePastor(long id, PastorRequestDto pastorRequestDto)
    {
        var errors = await ValidateAsync(pastorRequestDto, id);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Pastor>();
        var pastor = await repository.GetByIdAsync(id);

        if (pastor is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Pastor not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        pastor.Ci = NormalizeCi(pastorRequestDto.Ci);
        pastor.FirstNames = pastorRequestDto.FirstNames.Trim();
        pastor.LastNames = pastorRequestDto.LastNames.Trim();
        pastor.Email = pastorRequestDto.Email?.Trim().ToLower();
        pastor.IsActive = pastorRequestDto.IsActive;

        repository.Update(pastor);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = pastor.PastorId,
            Messages = [new Message { Type = MessageType.Success, Description = "Pastor updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeletePastor(long id)
    {
        var repository = _unitOfWork.Repository<Pastor>();
        var pastor = await repository.GetByIdAsync(id);

        if (pastor is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Pastor not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var hasRelatedRows =
            await _unitOfWork.Repository<DistrictPastor>().Query().AnyAsync(dp => dp.PastorId == id) ||
            await _unitOfWork.Repository<LocalFundCount>().Query().AnyAsync(lfc => lfc.PastorId == id) ||
            await _unitOfWork.Repository<AppUser>().Query().AnyAsync(u => u.PastorId == id);

        if (hasRelatedRows)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Este pastor tiene distritos, arqueos o un usuario asociados y no se puede eliminar. Desactívalo en su lugar." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        repository.Remove(pastor);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = pastor.PastorId,
            Messages = [new Message { Type = MessageType.Success, Description = "Pastor deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<List<Message>> ValidateAsync(PastorRequestDto pastorRequestDto, long? id)
    {
        var validationResult = await _validator.ValidateAsync(pastorRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        var ci = NormalizeCi(pastorRequestDto.Ci);
        if (ci is not null && await _unitOfWork.Repository<Pastor>().Query().AnyAsync(p => p.Ci == ci && p.PastorId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "A pastor with this CI already exists." });
        }

        return errors;
    }

    // El CI es opcional: vacío se guarda como null, así no choca con el índice único.
    private static string? NormalizeCi(string? ci) => string.IsNullOrWhiteSpace(ci) ? null : ci.Trim();
}
