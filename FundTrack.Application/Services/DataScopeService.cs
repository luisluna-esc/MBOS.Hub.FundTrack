using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class DataScopeService : IDataScopeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DataScopeService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<long?> GetRestrictedDistrictId()
    {
        // Solo se restringe a quien es únicamente Pastor: si además tiene otro rol, ve todo.
        var roles = _currentUserService.Roles;
        if (roles.Count == 0 || roles.Any(r => r != RoleNames.Pastor))
            return null;

        var appUserId = _currentUserService.AppUserId;
        var pastorId = await _unitOfWork.Repository<AppUser>().Query()
            .Where(u => u.AppUserId == appUserId)
            .Select(u => u.PastorId)
            .FirstOrDefaultAsync();

        if (!pastorId.HasValue)
            return 0;

        var districtId = await _unitOfWork.Repository<DistrictPastor>().Query()
            .Where(dp => dp.PastorId == pastorId.Value && dp.EndDate == null)
            .Select(dp => (long?)dp.DistrictId)
            .FirstOrDefaultAsync();

        return districtId ?? 0;
    }
}
