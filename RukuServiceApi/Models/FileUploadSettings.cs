namespace RukuServiceApi.Models;

public class FileUploadSettings
{
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB default
    public string[] AllowedExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
    public string[] AllowedMimeTypes { get; set; } = { "image/jpeg", "image/png", "image/gif", "application/pdf" };
    public string UploadPath { get; set; } = "uploads";
    public bool ScanForViruses { get; set; } = false; // Set to true in production
}
