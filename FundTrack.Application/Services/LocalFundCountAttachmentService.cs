using System.Net;
using FundTrack.Application.Dtos.Funds;
using FundTrack.Application.Interfaces.Funds;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Funds;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class LocalFundCountAttachmentService : ILocalFundCountAttachmentService
{
    private static readonly Dictionary<string, string> ExtensionByMimeType = new()
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;
    private readonly AttachmentOptions _attachmentOptions;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDataScopeService _dataScopeService;

    public LocalFundCountAttachmentService(
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage,
        AttachmentOptions attachmentOptions,
        ICurrentUserService currentUserService,
        IDataScopeService dataScopeService)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _attachmentOptions = attachmentOptions;
        _currentUserService = currentUserService;
        _dataScopeService = dataScopeService;
    }

    public async Task<ResponseGetObject> GetAttachments(long localFundCountId)
    {
        if (!await IsVisibleCount(localFundCountId))
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Local fund count not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var attachments = await _unitOfWork.Repository<LocalFundCountAttachment>().Query()
            .Where(a => a.LocalFundCountId == localFundCountId)
            .OrderBy(a => a.RegisteredAt)
            .ToListAsync();

        var userIds = attachments.Where(a => a.AppUserId.HasValue).Select(a => a.AppUserId!.Value).Distinct().ToList();
        var fullNameById = await _unitOfWork.Repository<AppUser>().Query()
            .Where(u => userIds.Contains(u.AppUserId))
            .ToDictionaryAsync(u => u.AppUserId, u => u.FullName);

        return new ResponseGetObject
        {
            Data = attachments.Select(a => new LocalFundCountAttachmentDto
            {
                LocalFundCountAttachmentId = a.LocalFundCountAttachmentId,
                LocalFundCountId = a.LocalFundCountId,
                FileName = a.FileName,
                MimeType = a.MimeType,
                SizeBytes = a.SizeBytes,
                AppUserId = a.AppUserId,
                AppUserFullName = a.AppUserId.HasValue ? fullNameById.GetValueOrDefault(a.AppUserId.Value) : null,
                RegisteredAt = a.RegisteredAt
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> Upload(long localFundCountId, string fileName, string mimeType, long sizeBytes, Stream content)
    {
        var errors = new List<Message>();

        if (!_attachmentOptions.AllowedContentTypes.Contains(mimeType) || !ExtensionByMimeType.ContainsKey(mimeType))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "Solo se aceptan archivos PDF, JPG o PNG." });
        }

        if (sizeBytes <= 0 || sizeBytes > _attachmentOptions.MaxSizeBytes)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = $"El archivo debe pesar como máximo {_attachmentOptions.MaxSizeBytes / (1024 * 1024)} MB." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        if (!await _unitOfWork.Repository<LocalFundCount>().Query().AnyAsync(c => c.LocalFundCountId == localFundCountId))
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Local fund count not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        // Un respaldo no cambia montos, así que se puede adjuntar aunque el período ya esté cerrado.
        var relativePath = await _fileStorage.Save(localFundCountId.ToString(), ExtensionByMimeType[mimeType], content);

        var attachment = new LocalFundCountAttachment
        {
            LocalFundCountId = localFundCountId,
            FileName = Path.GetFileName(fileName),
            FilePath = relativePath,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            AppUserId = _currentUserService.AppUserId,
            RegisteredAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<LocalFundCountAttachment>().AddAsync(attachment);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch
        {
            _fileStorage.Delete(relativePath);
            throw;
        }

        return new ResponsePost
        {
            Id = attachment.LocalFundCountAttachmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Attachment uploaded successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<AttachmentFile?> Download(long localFundCountId, long attachmentId)
    {
        if (!await IsVisibleCount(localFundCountId))
            return null;

        var attachment = await _unitOfWork.Repository<LocalFundCountAttachment>().Query()
            .FirstOrDefaultAsync(a => a.LocalFundCountAttachmentId == attachmentId && a.LocalFundCountId == localFundCountId);

        if (attachment is null)
            return null;

        var content = _fileStorage.Open(attachment.FilePath);
        return content is null ? null : new AttachmentFile(content, attachment.MimeType, attachment.FileName);
    }

    public async Task<ResponsePost> Delete(long localFundCountId, long attachmentId)
    {
        var repository = _unitOfWork.Repository<LocalFundCountAttachment>();
        var attachment = await repository.Query()
            .FirstOrDefaultAsync(a => a.LocalFundCountAttachmentId == attachmentId && a.LocalFundCountId == localFundCountId);

        if (attachment is null)
        {
            return new ResponsePost
            {
                Id = attachmentId,
                Messages = [new Message { Type = MessageType.Error, Description = "Attachment not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        // Borrar un respaldo de un mes cerrado dejaría al arqueo sin sustento.
        var periodIsClosed = await (
            from c in _unitOfWork.Repository<LocalFundCount>().Query()
            join p in _unitOfWork.Repository<Period>().Query() on c.PeriodId equals p.PeriodId
            where c.LocalFundCountId == localFundCountId && p.Status == PeriodStatuses.Closed
            select p.PeriodId
        ).AnyAsync();

        if (periodIsClosed)
        {
            return new ResponsePost
            {
                Id = attachmentId,
                Messages = [new Message { Type = MessageType.Error, Description = "El período está cerrado y sus respaldos ya no se pueden eliminar." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        repository.Remove(attachment);
        await _unitOfWork.SaveChangesAsync();
        _fileStorage.Delete(attachment.FilePath);

        return new ResponsePost
        {
            Id = attachmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Attachment deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<bool> IsVisibleCount(long localFundCountId)
    {
        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        return await _unitOfWork.Repository<LocalFundCount>().Query()
            .AnyAsync(c => c.LocalFundCountId == localFundCountId &&
                           (!restrictedDistrictId.HasValue || c.DistrictId == restrictedDistrictId.Value));
    }
}
