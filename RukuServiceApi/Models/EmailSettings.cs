namespace RukuServiceApi.Models;

public class EmailSettings
{
    public required string ResendApiKey { get; set; }
    public required string RecipientEmail { get; set; }
}
