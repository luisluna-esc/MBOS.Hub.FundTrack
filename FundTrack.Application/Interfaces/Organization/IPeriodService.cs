using FundTrack.Application.Dtos.Organization;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;

namespace FundTrack.Application.Interfaces.Organization;

public interface IPeriodService
{
    Task<ResponseGetObject> GetAllPeriods(PaginationQueryFilter paginationQueryFilter, PeriodQueryFilter periodQueryFilter);
    Task<ResponsePost> InsertPeriod(PeriodRequestDto periodRequestDto);
    Task<ResponsePost> GenerateYear(int year);
    Task<ResponsePost> UpdateDueDate(long id, DateOnly dueDate);
    Task<ResponsePost> ClosePeriod(long id);
    Task<ResponsePost> DeletePeriod(long id);
}
