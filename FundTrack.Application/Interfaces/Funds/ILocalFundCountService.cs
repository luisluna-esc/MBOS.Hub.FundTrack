using FundTrack.Application.Dtos.Funds;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Funds;
using FundTrack.core.QueryFilters.Pagination;

namespace FundTrack.Application.Interfaces.Funds;

public interface ILocalFundCountService
{
    Task<ResponseGetObject> GetAllLocalFundCounts(PaginationQueryFilter paginationQueryFilter, LocalFundCountQueryFilter localFundCountQueryFilter);
    Task<ResponseGetObject> GetLocalFundCount(long id);
    Task<ResponseGetObject> GetByPeriod(long periodId, long? regionId, long? districtId);
    Task<ResponsePost> InsertLocalFundCount(LocalFundCountRequestDto localFundCountRequestDto);
    Task<ResponsePost> UpdateLocalFundCount(long id, LocalFundCountRequestDto localFundCountRequestDto);
    Task<ResponsePost> DeleteLocalFundCount(long id);
}
