using FundTrack.Application.Dtos.Organization;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;

namespace FundTrack.Application.Interfaces.Organization;

public interface IDistrictService
{
    Task<ResponseGetObject> GetAllDistricts(PaginationQueryFilter paginationQueryFilter, DistrictQueryFilter districtQueryFilter);
    Task<ResponsePost> InsertDistrict(DistrictRequestDto districtRequestDto);
    Task<ResponsePost> UpdateDistrict(long id, DistrictRequestDto districtRequestDto);
    Task<ResponsePost> DeleteDistrict(long id);
    Task<ResponseGetObject> GetPastorHistory(long districtId);
    Task<ResponsePost> AssignPastor(long districtId, DistrictPastorAssignRequestDto assignRequestDto);
}
