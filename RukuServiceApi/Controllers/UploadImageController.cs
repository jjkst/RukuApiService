using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RukuServiceApi.Models;
using RukuServiceApi.Services;

namespace RukuServiceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.AdminOrOwner)] // Only admins and owners can upload files
public class UploadImageController(
    IFileUploadService fileUploadService,
    IOptions<FileUploadSettings> fileUploadSettings,
    ILogger<UploadImageController> uploadImageController
) : ControllerBase
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg",  "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png",  "image/png" },
        { ".gif",  "image/gif" },
        { ".webp", "image/webp" },
    };

    public class UploadImageRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? Folder { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage([FromForm] UploadImageRequest request)
    {
        try
        {
            var (success, filePath, errorMessage) = await fileUploadService.UploadFileAsync(
                request.File,
                request.Folder
            );

            if (!success)
            {
                return BadRequest(new { message = errorMessage });
            }

            return Ok(
                new
                {
                    fileName = Path.GetFileName(filePath),
                    size = request.File.Length,
                }
            );
        }
        catch (Exception ex)
        {
            uploadImageController.LogError(
                ex,
                "Error uploading file: {FileName}",
                request.File?.FileName
            );
            return StatusCode(500, new { message = "File upload failed" });
        }
    }

    [HttpGet("files/{fileName}")]
    [AllowAnonymous]
    public IActionResult GetFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.Contains("..") || Path.IsPathRooted(fileName))
            return BadRequest();

        var settings = fileUploadSettings.Value;
        var uploadRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), settings.UploadPath));
        var filePath = Path.GetFullPath(Path.Combine(uploadRoot, "uploads", fileName));

        if (!filePath.StartsWith(uploadRoot + Path.DirectorySeparatorChar))
            return BadRequest();

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ContentTypes.GetValueOrDefault(ext, "application/octet-stream");
        return PhysicalFile(filePath, contentType);
    }
}
