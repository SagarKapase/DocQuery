using DocQuery.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocQuery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController : BaseController
{
    private readonly DocumentService _documents;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(DocumentService documents, ILogger<DocumentsController> logger)
    {
        _documents = documents;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)] // 20 MB
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return BadRequest("Only PDF files are allowed.");

        try
        {
            // Copy to memory because PdfPig needs a seekable stream
            using var memory = new MemoryStream();
            await file.CopyToAsync(memory);
            memory.Position = 0;

            var doc = await _documents.IngestAsync(CurrentUserId, file.FileName, memory);
            return Ok(doc);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for {File}", file.FileName);
            return StatusCode(500, $"Failed to process the PDF: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        return Ok(await _documents.ListAsync(CurrentUserId));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _documents.DeleteAsync(CurrentUserId, id);
        return deleted ? NoContent() : NotFound();
    }
}
