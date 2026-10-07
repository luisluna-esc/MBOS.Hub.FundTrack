using FundTrack.Application.Dtos.Funds;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Funds;

public interface ILocalFundCountAttachmentService
{
    Task<ResponseGetObject> GetAttachments(long localFundCountId);
    Task<ResponsePost> Upload(long localFundCountId, string fileName, string mimeType, long sizeBytes, Stream content);
    /// <summary>Null si el adjunto no existe o no pertenece a ese arqueo.</summary>
    Task<AttachmentFile?> Download(long localFundCountId, long attachmentId);
    Task<ResponsePost> Delete(long localFundCountId, long attachmentId);
}
