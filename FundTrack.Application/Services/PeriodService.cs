using System.Net;
using FundTrack.Application.Dtos.Organization;
using FundTrack.Application.Interfaces.Organization;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Constants;
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

public class PeriodService : IPeriodService
{
    // El ACMS cierra cada mes el día 15 del mes siguiente.
    private const int DueDayOfNextMonth = 15;

    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<PeriodRequestDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public PeriodService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<PeriodRequestDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllPeriods(PaginationQueryFilter paginationQueryFilter, PeriodQueryFilter periodQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Period>().Query();

        if (periodQueryFilter.PeriodId.HasValue)
        {
            query = query.Where(p => p.PeriodId == periodQueryFilter.PeriodId.Value);
        }

        if (periodQueryFilter.Year.HasValue)
        {
            query = query.Where(p => p.Year == periodQueryFilter.Year.Value);
        }

        if (!string.IsNullOrWhiteSpace(periodQueryFilter.Status))
        {
            query = query.Where(p => p.Status == periodQueryFilter.Status);
        }

        var totalRecords = await query.CountAsync();

        var periods = await query
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var closedByIds = periods.Where(p => p.ClosedById.HasValue).Select(p => p.ClosedById!.Value).Distinct().ToList();
        var fullNameById = await _unitOfWork.Repository<AppUser>().Query()
            .Where(u => closedByIds.Contains(u.AppUserId))
            .ToDictionaryAsync(u => u.AppUserId, u => u.FullName);

        return new ResponseGetObject
        {
            Data = new PagedResult<PeriodDto>
            {
                Items = periods.Select(p => new PeriodDto
                {
                    PeriodId = p.PeriodId,
                    Year = p.Year,
                    Month = p.Month,
                    DueDate = p.DueDate,
                    Status = p.Status,
                    ClosedAt = p.ClosedAt,
                    ClosedById = p.ClosedById,
                    ClosedByFullName = p.ClosedById.HasValue ? fullNameById.GetValueOrDefault(p.ClosedById.Value) : null
                }),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertPeriod(PeriodRequestDto periodRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(periodRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (errors.Count == 0 && await _unitOfWork.Repository<Period>().Query()
                .AnyAsync(p => p.Year == periodRequestDto.Year && p.Month == periodRequestDto.Month))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This period already exists." });
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

        var period = NewPeriod(periodRequestDto.Year, periodRequestDto.Month, periodRequestDto.DueDate);

        await _unitOfWork.Repository<Period>().AddAsync(period);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = period.PeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "Period created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> GenerateYear(int year)
    {
        if (year is < 2000 or > 2100)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Year must be between 2000 and 2100." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Period>();
        var existingMonths = await repository.Query()
            .Where(p => p.Year == year)
            .Select(p => p.Month)
            .ToListAsync();

        var missingMonths = Enumerable.Range(1, 12).Except(existingMonths).ToList();

        foreach (var month in missingMonths)
        {
            await repository.AddAsync(NewPeriod(year, month, null));
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = 0,
            Messages = [new Message { Type = MessageType.Success, Description = $"{missingMonths.Count} period(s) created for {year}." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateDueDate(long id, DateOnly dueDate)
    {
        var repository = _unitOfWork.Repository<Period>();
        var period = await repository.GetByIdAsync(id);

        if (period is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Period not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (period.Status == PeriodStatuses.Closed)
        {
            return ClosedPeriodError(id);
        }

        period.DueDate = dueDate;
        repository.Update(period);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = period.PeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "Period updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> ClosePeriod(long id)
    {
        var repository = _unitOfWork.Repository<Period>();
        var period = await repository.GetByIdAsync(id);

        if (period is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Period not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (period.Status == PeriodStatuses.Closed)
        {
            return ClosedPeriodError(id);
        }

        // No existe la operación inversa: un período cerrado no se vuelve a abrir.
        period.Status = PeriodStatuses.Closed;
        period.ClosedAt = DateTime.UtcNow;
        period.ClosedById = _currentUserService.AppUserId;

        repository.Update(period);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = period.PeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "Period closed successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeletePeriod(long id)
    {
        var repository = _unitOfWork.Repository<Period>();
        var period = await repository.GetByIdAsync(id);

        if (period is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Period not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (period.Status == PeriodStatuses.Closed)
        {
            return ClosedPeriodError(id);
        }

        var hasRelatedRows =
            await _unitOfWork.Repository<LocalFundCount>().Query().AnyAsync(lfc => lfc.PeriodId == id) ||
            await _unitOfWork.Repository<LocalFundCountCorrection>().Query().AnyAsync(c => c.AppliedPeriodId == id);

        if (hasRelatedRows)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Este período ya tiene arqueos o correcciones y no se puede eliminar." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        repository.Remove(period);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = period.PeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "Period deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static Period NewPeriod(int year, int month, DateOnly? dueDate) => new()
    {
        Year = year,
        Month = month,
        DueDate = dueDate ?? new DateOnly(year, month, 1).AddMonths(1).AddDays(DueDayOfNextMonth - 1),
        Status = PeriodStatuses.Open
    };

    private static ResponsePost ClosedPeriodError(long id) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = "Este período está cerrado y ya no se puede modificar." }],
        StatusCode = HttpStatusCode.BadRequest
    };
}
