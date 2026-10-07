using System.Net;
using FundTrack.Application.Dtos.Organization;
using FundTrack.Application.Interfaces.Organization;
using FundTrack.core.Configuration;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class RegionService : IRegionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<RegionDto> _validator;

    public RegionService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<RegionDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllRegions(PaginationQueryFilter paginationQueryFilter, RegionQueryFilter regionQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Region>().Query();

        if (regionQueryFilter.RegionId.HasValue)
        {
            query = query.Where(r => r.RegionId == regionQueryFilter.RegionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(r => r.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (regionQueryFilter.IsActive.HasValue)
        {
            query = query.Where(r => r.IsActive == regionQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var regions = await query
            .OrderBy(r => r.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<RegionDto>
            {
                Items = regions.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertRegion(RegionDto regionDto)
    {
        var errors = await ValidateAsync(regionDto, null);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var region = new Region
        {
            Name = regionDto.Name.Trim(),
            IsActive = regionDto.IsActive,
            PendingBalance = regionDto.PendingBalance,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Region>().AddAsync(region);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = region.RegionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Region created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateRegion(long id, RegionDto regionDto)
    {
        var errors = await ValidateAsync(regionDto, id);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Region>();
        var region = await repository.GetByIdAsync(id);

        if (region is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Region not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        region.Name = regionDto.Name.Trim();
        region.IsActive = regionDto.IsActive;
        region.PendingBalance = regionDto.PendingBalance;

        repository.Update(region);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = region.RegionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Region updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteRegion(long id)
    {
        var repository = _unitOfWork.Repository<Region>();
        var region = await repository.GetByIdAsync(id);

        if (region is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Region not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (await _unitOfWork.Repository<District>().Query().AnyAsync(d => d.RegionId == id))
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Esta región tiene distritos y no se puede eliminar. Desactívala en su lugar." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        repository.Remove(region);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = region.RegionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Region deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<List<Message>> ValidateAsync(RegionDto regionDto, long? id)
    {
        var validationResult = await _validator.ValidateAsync(regionDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        var name = regionDto.Name.Trim();
        if (await _unitOfWork.Repository<Region>().Query().AnyAsync(r => r.Name == name && r.RegionId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "A region with this name already exists." });
        }

        return errors;
    }

    private static RegionDto ToDto(Region region) => new()
    {
        RegionId = region.RegionId,
        Name = region.Name,
        IsActive = region.IsActive,
        CreatedAt = region.CreatedAt,
        PendingBalance = region.PendingBalance
    };
}
