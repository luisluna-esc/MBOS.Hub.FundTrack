using System.Net;
using FundTrack.Application.Dtos.Funds;
using FundTrack.Application.Interfaces.Funds;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Funds;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class DifferenceFollowUpService : IDifferenceFollowUpService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<DifferenceFollowUpRequestDto> _validator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDataScopeService _dataScopeService;

    public DifferenceFollowUpService(
        IUnitOfWork unitOfWork,
        IValidator<DifferenceFollowUpRequestDto> validator,
        ICurrentUserService currentUserService,
        IDataScopeService dataScopeService)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
        _currentUserService = currentUserService;
        _dataScopeService = dataScopeService;
    }

    public async Task<ResponseGetObject> GetFollowUps(long localFundCountId)
    {
        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        var countExists = await _unitOfWork.Repository<LocalFundCount>().Query()
            .AnyAsync(c => c.LocalFundCountId == localFundCountId &&
                           (!restrictedDistrictId.HasValue || c.DistrictId == restrictedDistrictId.Value));

        if (!countExists)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Local fund count not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var followUps = await _unitOfWork.Repository<DifferenceFollowUp>().Query()
            .Where(f => f.LocalFundCountId == localFundCountId)
            .OrderBy(f => f.FollowedUpAt)
            .ToListAsync();

        var userIds = followUps.Where(f => f.AppUserId.HasValue).Select(f => f.AppUserId!.Value).Distinct().ToList();
        var fullNameById = await _unitOfWork.Repository<AppUser>().Query()
            .Where(u => userIds.Contains(u.AppUserId))
            .ToDictionaryAsync(u => u.AppUserId, u => u.FullName);

        return new ResponseGetObject
        {
            Data = followUps.Select(f => new DifferenceFollowUpDto
            {
                DifferenceFollowUpId = f.DifferenceFollowUpId,
                LocalFundCountId = f.LocalFundCountId,
                PreviousStatus = f.PreviousStatus,
                NewStatus = f.NewStatus,
                Justification = f.Justification,
                AppUserId = f.AppUserId,
                AppUserFullName = f.AppUserId.HasValue ? fullNameById.GetValueOrDefault(f.AppUserId.Value) : null,
                FollowedUpAt = f.FollowedUpAt
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> AddFollowUp(long localFundCountId, DifferenceFollowUpRequestDto followUpRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(followUpRequestDto);
        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var countRepository = _unitOfWork.Repository<LocalFundCount>();
        var count = await countRepository.GetByIdAsync(localFundCountId);

        if (count is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Local fund count not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        // El seguimiento cambia solo el estado de la diferencia, no los montos, por eso se
        // permite también en arqueos de períodos ya cerrados.
        var expectedNext = DifferenceStatuses.NextOf(count.DifferenceStatus);
        if (expectedNext != followUpRequestDto.NewStatus)
        {
            var description = expectedNext is null
                ? "Este arqueo no tiene una diferencia pendiente de seguimiento."
                : $"La diferencia está en '{count.DifferenceStatus}'; el siguiente paso solo puede ser '{expectedNext}'.";

            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = description }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var followUp = new DifferenceFollowUp
        {
            LocalFundCountId = count.LocalFundCountId,
            PreviousStatus = count.DifferenceStatus,
            NewStatus = followUpRequestDto.NewStatus,
            Justification = followUpRequestDto.Justification,
            AppUserId = _currentUserService.AppUserId,
            FollowedUpAt = DateTime.UtcNow
        };

        count.DifferenceStatus = followUpRequestDto.NewStatus;
        countRepository.Update(count);

        await _unitOfWork.Repository<DifferenceFollowUp>().AddAsync(followUp);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = followUp.DifferenceFollowUpId,
            Messages = [new Message { Type = MessageType.Success, Description = "Difference follow-up registered successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }
}
