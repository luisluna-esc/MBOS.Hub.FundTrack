using FundTrack.Application.Dtos.Funds;
using FundTrack.Application.Interfaces.Funds;
using FundTrack.core.Constants;
using FundTrack.core.QueryFilters.Funds;
using FundTrack.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

[ApiController]
[Route("api/local-fund-counts")]
[Authorize]
public class LocalFundCountsController : ControllerBase
{
    private readonly ILocalFundCountService _localFundCountService;
    private readonly IDifferenceFollowUpService _differenceFollowUpService;
    private readonly ILocalFundCountCorrectionService _correctionService;
    private readonly ILocalFundCountAttachmentService _attachmentService;

    public LocalFundCountsController(
        ILocalFundCountService localFundCountService,
        IDifferenceFollowUpService differenceFollowUpService,
        ILocalFundCountCorrectionService correctionService,
        ILocalFundCountAttachmentService attachmentService)
    {
        _localFundCountService = localFundCountService;
        _differenceFollowUpService = differenceFollowUpService;
        _correctionService = correctionService;
        _attachmentService = attachmentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] LocalFundCountQueryFilter localFundCountQueryFilter)
    {
        var result = await _localFundCountService.GetAllLocalFundCounts(paginationQueryFilter, localFundCountQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _localFundCountService.GetLocalFundCount(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/documents")]
    public async Task<IActionResult> GetDocuments(long id)
    {
        var result = await _localFundCountService.GetDocuments(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("by-period/{periodId}")]
    public async Task<IActionResult> GetByPeriod(long periodId, [FromQuery] long? regionId, [FromQuery] long? districtId)
    {
        var result = await _localFundCountService.GetByPeriod(periodId, regionId, districtId);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] LocalFundCountRequestDto localFundCountRequestDto)
    {
        var result = await _localFundCountService.InsertLocalFundCount(localFundCountRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] LocalFundCountRequestDto localFundCountRequestDto)
    {
        var result = await _localFundCountService.UpdateLocalFundCount(id, localFundCountRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _localFundCountService.DeleteLocalFundCount(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/follow-ups")]
    public async Task<IActionResult> GetFollowUps(long id)
    {
        var result = await _differenceFollowUpService.GetFollowUps(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("{id}/follow-ups")]
    public async Task<IActionResult> AddFollowUp(long id, [FromBody] DifferenceFollowUpRequestDto followUpRequestDto)
    {
        var result = await _differenceFollowUpService.AddFollowUp(id, followUpRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/corrections")]
    public async Task<IActionResult> GetCorrections(long id)
    {
        var result = await _correctionService.GetCorrections(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("{id}/corrections")]
    public async Task<IActionResult> AddCorrection(long id, [FromBody] LocalFundCountCorrectionRequestDto correctionRequestDto)
    {
        var result = await _correctionService.AddCorrection(id, correctionRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/attachments")]
    public async Task<IActionResult> GetAttachments(long id)
    {
        var result = await _attachmentService.GetAttachments(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("{id}/attachments")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(long id, IFormFile file)
    {
        await using var content = file.OpenReadStream();
        var result = await _attachmentService.Upload(id, file.FileName, file.ContentType, file.Length, content);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/attachments/{attachmentId}")]
    public async Task<IActionResult> DownloadAttachment(long id, long attachmentId)
    {
        var attachmentFile = await _attachmentService.Download(id, attachmentId);
        return attachmentFile is null
            ? NotFound()
            : File(attachmentFile.Content, attachmentFile.MimeType, attachmentFile.FileName);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("{id}/attachments/{attachmentId}")]
    public async Task<IActionResult> DeleteAttachment(long id, long attachmentId)
    {
        var result = await _attachmentService.Delete(id, attachmentId);
        return StatusCode((int)result.StatusCode, result);
    }
}
