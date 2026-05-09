using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RukuServiceApi.Models;
using RukuServiceApi.Services;

namespace RukuServiceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.AdminOrOwner)] // Only admins and owners can upload files
public class UploadImageController(
    IFileUploadService fileUploadService,
    ILogger<UploadImageController> uploadImageController
) : ControllerBase
{
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

            // Return relative path for security
            var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), filePath);

            return Ok(
                new
                {
                    filePath = relativePath,
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
}
