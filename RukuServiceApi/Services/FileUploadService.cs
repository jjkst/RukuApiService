using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using RukuServiceApi.Models;

namespace RukuServiceApi.Services;

public interface IFileUploadService
{
    Task<(bool Success, string FilePath, string ErrorMessage)> UploadFileAsync(
        IFormFile file,
        string? folder = null
    );
    bool ValidateFile(IFormFile file, out string errorMessage);
    string GenerateSecureFileName(string originalFileName);
    bool IsPathSafe(string path);
}

public class FileUploadService : IFileUploadService
{
    private readonly FileUploadSettings _settings;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(
        IOptions<FileUploadSettings> settings,
        ILogger<FileUploadService> logger
    )
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string FilePath, string ErrorMessage)> UploadFileAsync(
        IFormFile file,
        string? folder = null
    )
    {
        try
        {
            if (!ValidateFile(file, out string validationError))
            {
                return (false, string.Empty, validationError);
            }

            // Create secure upload directory
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), _settings.UploadPath);
            if (!string.IsNullOrEmpty(folder))
            {
                uploadDir = Path.Combine(uploadDir, folder);
            }

            // Ensure directory exists and is safe
            if (!IsPathSafe(uploadDir))
            {
                _logger.LogWarning("Unsafe upload path attempted: {Path}", uploadDir);
                return (false, string.Empty, "Invalid upload path");
            }

            Directory.CreateDirectory(uploadDir);

            // Generate secure filename
            var secureFileName = GenerateSecureFileName(file.FileName);
            var filePath = Path.Combine(uploadDir, secureFileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File uploaded successfully: {FileName}", secureFileName);
            return (true, filePath, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file?.FileName ?? "<unknown>");
            return (false, string.Empty, "File upload failed");
        }
    }

    public bool ValidateFile(IFormFile file, out string errorMessage)
    {
        errorMessage = string.Empty;

        // Check if file exists
        if (file == null || file.Length == 0)
        {
            errorMessage = "No file provided";
            return false;
        }

        // Check file size
        if (file.Length > _settings.MaxFileSizeBytes)
        {
            errorMessage =
                $"File size exceeds maximum allowed size of {_settings.MaxFileSizeBytes / (1024 * 1024)}MB";
            return false;
        }

        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_settings.AllowedExtensions.Contains(extension))
        {
            errorMessage =
                $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _settings.AllowedExtensions)}";
            return false;
        }

        // Check MIME type to prevent extension spoofing
        if (!_settings.AllowedMimeTypes.Contains(file.ContentType))
        {
            errorMessage =
                $"File MIME type '{file.ContentType}' is not allowed. Allowed types: {string.Join(", ", _settings.AllowedMimeTypes)}";
            return false;
        }

        // Check filename for suspicious patterns
        if (ContainsSuspiciousPatterns(file.FileName))
        {
            errorMessage = "Filename contains suspicious patterns";
            return false;
        }

        return true;
    }

    public string GenerateSecureFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Generate a random string for additional security
        using (var rng = RandomNumberGenerator.Create())
        {
            var bytes = new byte[8];
            rng.GetBytes(bytes);
            var randomString = Convert
                .ToBase64String(bytes)
                .Replace("/", "")
                .Replace("+", "")
                .Replace("=", "");
            return $"{timestamp}_{randomString}{extension}";
        }
    }

    public bool IsPathSafe(string path)
    {
        // Prevent path traversal attacks
        var fullPath = Path.GetFullPath(path);
        var basePath = Path.GetFullPath(_settings.UploadPath);

        // Ensure base path ends with separator to prevent prefix attacks (e.g. "uploads_evil" matching "uploads")
        if (!basePath.EndsWith(Path.DirectorySeparatorChar))
        {
            basePath += Path.DirectorySeparatorChar;
        }

        // Use case-insensitive comparison on Windows
        return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
    }

    private bool ContainsSuspiciousPatterns(string fileName)
    {
        var suspiciousPatterns = new[]
        {
            "..",
            "\\",
            "/",
            "<",
            ">",
            ":",
            "\"",
            "|",
            "?",
            "*",
            "script",
            "javascript",
            "vbscript",
            "onload",
            "onerror",
        };

        var lowerFileName = fileName.ToLowerInvariant();
        return suspiciousPatterns.Any(pattern => lowerFileName.Contains(pattern));
    }
}
