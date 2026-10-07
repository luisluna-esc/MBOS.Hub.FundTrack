using FundTrack.Application.Dtos.Organization;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;

namespace FundTrack.Application.Interfaces.Organization;

public interface IPastorService
{
    Task<ResponseGetObject> GetAllPastors(PaginationQueryFilter paginationQueryFilter, PastorQueryFilter pastorQueryFilter);
    Task<ResponsePost> InsertPastor(PastorRequestDto pastorRequestDto);
    Task<ResponsePost> UpdatePastor(long id, PastorRequestDto pastorRequestDto);
    Task<ResponsePost> DeletePastor(long id);
}
