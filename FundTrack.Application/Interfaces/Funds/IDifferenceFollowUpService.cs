using FundTrack.Application.Dtos.Funds;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Funds;

public interface IDifferenceFollowUpService
{
    Task<ResponseGetObject> GetFollowUps(long localFundCountId);
    Task<ResponsePost> AddFollowUp(long localFundCountId, DifferenceFollowUpRequestDto followUpRequestDto);
}
