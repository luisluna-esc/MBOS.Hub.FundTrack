using FundTrack.Application.Dtos.Organization;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;

namespace FundTrack.Application.Interfaces.Organization;

public interface IRegionService
{
    Task<ResponseGetObject> GetAllRegions(PaginationQueryFilter paginationQueryFilter, RegionQueryFilter regionQueryFilter);
    Task<ResponsePost> InsertRegion(RegionDto regionDto);
    Task<ResponsePost> UpdateRegion(long id, RegionDto regionDto);
    Task<ResponsePost> DeleteRegion(long id);
}
