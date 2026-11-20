namespace RaceDay.Api.Helpers;

/// <summary>
/// Checks an uploaded image before it is sent to Azure Blob Storage: size, extension and the
/// file's real signature (so a text file renamed to .png is rejected).
/// </summary>
public static class ImageValidator
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp"
    };

    public record ImageCheck(bool IsValid, string? Error, string Extension, string ContentType);

    public static async Task<ImageCheck> ValidateAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return Fail("Please choose an image file to upload.");

        if (file.Length > MaxBytes)
            return Fail("The image must be 5 MB or smaller.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ContentTypes.TryGetValue(extension, out var contentType))
            return Fail("Only JPG, PNG, GIF or WEBP images are allowed.");

        var header = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            var read = await stream.ReadAsync(header, 0, header.Length);
            if (read < header.Length || !MatchesSignature(header, extension))
                return Fail("The file is not a valid image of the type its extension claims.");
        }

        return new ImageCheck(true, null, extension == ".jpeg" ? ".jpg" : extension, contentType);
    }

    private static bool MatchesSignature(byte[] h, string extension) => extension switch
    {
        ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                  && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A,
        ".gif" => h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38
                  && (h[4] == 0x37 || h[4] == 0x39) && h[5] == 0x61,
        ".webp" => h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                   && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50,
        _ => false
    };

    private static ImageCheck Fail(string error) => new(false, error, string.Empty, string.Empty);
}
