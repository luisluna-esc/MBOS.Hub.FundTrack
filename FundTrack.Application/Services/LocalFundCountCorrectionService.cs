using System.Net;
using FundTrack.Application.Dtos.Funds;
using FundTrack.Application.Interfaces.Funds;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Funds;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class LocalFundCountCorrectionService : ILocalFundCountCorrectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<LocalFundCountCorrectionRequestDto> _validator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDataScopeService _dataScopeService;

    public LocalFundCountCorrectionService(
        IUnitOfWork unitOfWork,
        IValidator<LocalFundCountCorrectionRequestDto> validator,
        ICurrentUserService currentUserService,
        IDataScopeService dataScopeService)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
        _currentUserService = currentUserService;
        _dataScopeService = dataScopeService;
    }

    public async Task<ResponseGetObject> GetCorrections(long localFundCountId)
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

        var corrections = await _unitOfWork.Repository<LocalFundCountCorrection>().Query()
            .Where(c => c.LocalFundCountId == localFundCountId)
            .OrderBy(c => c.RegisteredAt)
            .ToListAsync();

        var periodIds = corrections.Select(c => c.AppliedPeriodId).Distinct().ToList();
        var periodById = await _unitOfWork.Repository<Period>().Query()
            .Where(p => periodIds.Contains(p.PeriodId))
            .ToDictionaryAsync(p => p.PeriodId);

        var userIds = corrections.Where(c => c.AppUserId.HasValue).Select(c => c.AppUserId!.Value).Distinct().ToList();
        var fullNameById = await _unitOfWork.Repository<AppUser>().Query()
            .Where(u => userIds.Contains(u.AppUserId))
            .ToDictionaryAsync(u => u.AppUserId, u => u.FullName);

        return new ResponseGetObject
        {
            Data = corrections.Select(c =>
            {
                periodById.TryGetValue(c.AppliedPeriodId, out var period);
                return new LocalFundCountCorrectionDto
                {
                    LocalFundCountCorrectionId = c.LocalFundCountCorrectionId,
                    LocalFundCountId = c.LocalFundCountId,
                    AppliedPeriodId = c.AppliedPeriodId,
                    AppliedPeriodYear = period?.Year ?? 0,
                    AppliedPeriodMonth = period?.Month ?? 0,
                    FieldName = c.FieldName,
                    PreviousAmount = c.PreviousAmount,
                    CorrectedAmount = c.CorrectedAmount,
                    Reason = c.Reason,
                    AppUserId = c.AppUserId,
                    AppUserFullName = c.AppUserId.HasValue ? fullNameById.GetValueOrDefault(c.AppUserId.Value) : null,
                    RegisteredAt = c.RegisteredAt
                };
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> AddCorrection(long localFundCountId, LocalFundCountCorrectionRequestDto correctionRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(correctionRequestDto);
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

        var count = await _unitOfWork.Repository<LocalFundCount>().GetByIdAsync(localFundCountId);
        if (count is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Local fund count not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var periodRepository = _unitOfWork.Repository<Period>();
        var countPeriod = await periodRepository.GetByIdAsync(count.PeriodId);

        // Mientras el período está abierto se edita el arqueo directamente; la corrección
        // existe solo para los meses que ya se cerraron y no se pueden tocar.
        if (countPeriod!.Status != PeriodStatuses.Closed)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "El período de este arqueo sigue abierto: edita el arqueo directamente." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var appliedPeriod = await periodRepository.Query()
            .Where(p => p.Status == PeriodStatuses.Open)
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .FirstOrDefaultAsync();

        if (appliedPeriod is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "No hay un período abierto donde aplicar la corrección. Crea el período del mes actual." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        // Si el monto ya se corrigió antes, el "anterior" es la última corrección y no el original.
        var lastCorrectedAmount = await _unitOfWork.Repository<LocalFundCountCorrection>().Query()
            .Where(c => c.LocalFundCountId == localFundCountId && c.FieldName == correctionRequestDto.FieldName)
            .OrderByDescending(c => c.RegisteredAt)
            .Select(c => (decimal?)c.CorrectedAmount)
            .FirstOrDefaultAsync();

        var previousAmount = lastCorrectedAmount ?? correctionRequestDto.FieldName switch
        {
            CorrectionFields.AcmsBalance => count.AcmsBalance,
            CorrectionFields.CashAmount => count.CashAmount,
            _ => count.BankAmount
        };

        var correction = new LocalFundCountCorrection
        {
            LocalFundCountId = count.LocalFundCountId,
            AppliedPeriodId = appliedPeriod.PeriodId,
            FieldName = correctionRequestDto.FieldName,
            PreviousAmount = previousAmount,
            CorrectedAmount = correctionRequestDto.CorrectedAmount,
            Reason = correctionRequestDto.Reason,
            AppUserId = _currentUserService.AppUserId,
            RegisteredAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<LocalFundCountCorrection>().AddAsync(correction);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = correction.LocalFundCountCorrectionId,
            Messages = [new Message { Type = MessageType.Success, Description = $"Correction registered and applied to period {appliedPeriod.Month}/{appliedPeriod.Year}." }],
            StatusCode = HttpStatusCode.Created
        };
    }
}
