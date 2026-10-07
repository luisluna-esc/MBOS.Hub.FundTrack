using FundTrack.Application.Dtos.Funds;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Funds;

public interface ILocalFundCountCorrectionService
{
    Task<ResponseGetObject> GetCorrections(long localFundCountId);
    Task<ResponsePost> AddCorrection(long localFundCountId, LocalFundCountCorrectionRequestDto correctionRequestDto);
}
