using FundTrack.Application.Dtos.Organization;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;

namespace FundTrack.Application.Interfaces.Organization;

public interface IChurchService
{
    Task<ResponseGetObject> GetAllChurches(PaginationQueryFilter paginationQueryFilter, ChurchQueryFilter churchQueryFilter);
    Task<ResponsePost> InsertChurch(ChurchRequestDto churchRequestDto);
    Task<ResponsePost> UpdateChurch(long id, ChurchRequestDto churchRequestDto);
    Task<ResponsePost> CloseChurch(long id, ChurchCloseRequestDto churchCloseRequestDto);
    Task<ResponseGetObject> GetHistory(long churchId);
}
