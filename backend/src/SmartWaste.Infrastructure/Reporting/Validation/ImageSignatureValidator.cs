using SmartWaste.Application.Common.Exceptions;

namespace SmartWaste.Infrastructure.Reporting.Validation;

/// <summary>
/// Validates image signatures / magic bytes to defeat MIME spoofing and enforce the allowed image types.
/// Supported formats: JPEG (image/jpeg), PNG (image/png), and WebP (image/webp).
/// </summary>
public static class ImageSignatureValidator
{
    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] RiffHeader = { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
    private static readonly byte[] WebpHeader = { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"

    public const string JpegMime = "image/jpeg";
    public const string PngMime = "image/png";
    public const string WebpMime = "image/webp";

    public readonly record struct ValidatedImage(string CanonicalMime, string Extension);

    /// <summary>
    /// Reads initial bytes from the stream, determines the image format via magic bytes,
    /// verifies agreement with optional client-supplied Content-Type and filename, and returns the canonical MIME and extension.
    /// </summary>
    /// <param name="headerBytes">At least the first 12-16 bytes of the file.</param>
    /// <param name="clientContentType">Optional client-supplied Content-Type header.</param>
    /// <param name="clientFileName">Optional client-supplied file name.</param>
    /// <returns>ValidatedImage containing CanonicalMime and Extension.</returns>
    /// <exception cref="FileValidationException">File does not match allowed image formats or header conflicts.</exception>
    public static ValidatedImage Validate(
        ReadOnlySpan<byte> headerBytes,
        string? clientContentType = null,
        string? clientFileName = null)
    {
        var detected = DetectFromMagicBytes(headerBytes);
        if (detected is null)
        {
            throw new FileValidationException("Unsupported file type. Only JPEG, PNG, and WebP images are allowed.");
        }

        var (canonicalMime, extension) = detected.Value;

        // Verify client Content-Type agreement if provided
        if (!string.IsNullOrWhiteSpace(clientContentType))
        {
            var normalizedClientMime = clientContentType.Trim().ToLowerInvariant();
            if (normalizedClientMime != canonicalMime)
            {
                // Also handle common synonyms like image/pjpeg or image/x-png if they match the detected format
                var isJpegSynonym = canonicalMime == JpegMime && (normalizedClientMime == "image/jpg" || normalizedClientMime == "image/pjpeg");
                var isPngSynonym = canonicalMime == PngMime && normalizedClientMime == "image/x-png";

                if (!isJpegSynonym && !isPngSynonym)
                {
                    throw new FileValidationException(
                        $"File content signature ({canonicalMime}) does not match the provided Content-Type ({clientContentType}).");
                }
            }
        }

        // Verify client file extension agreement if provided
        if (!string.IsNullOrWhiteSpace(clientFileName))
        {
            var ext = Path.GetExtension(clientFileName).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext))
            {
                var isExtensionValid = canonicalMime switch
                {
                    JpegMime => ext is ".jpg" or ".jpeg" or ".pjpeg",
                    PngMime => ext is ".png",
                    WebpMime => ext is ".webp",
                    _ => false
                };

                if (!isExtensionValid)
                {
                    throw new FileValidationException(
                        $"File extension '{ext}' does not match detected image format ({canonicalMime}).");
                }
            }
        }

        return new ValidatedImage(canonicalMime, extension);
    }

    private static (string Mime, string Extension)? DetectFromMagicBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 3)
        {
            return null;
        }

        // JPEG check (first 3 bytes: FF D8 FF)
        if (bytes.Length >= JpegMagic.Length && bytes[..JpegMagic.Length].SequenceEqual(JpegMagic))
        {
            return (JpegMime, "jpg");
        }

        // PNG check (first 8 bytes: 89 50 4E 47 0D 0A 1A 0A)
        if (bytes.Length >= PngMagic.Length && bytes[..PngMagic.Length].SequenceEqual(PngMagic))
        {
            return (PngMime, "png");
        }

        // WebP check (bytes 0..3: "RIFF", bytes 8..11: "WEBP")
        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual(RiffHeader)
            && bytes.Slice(8, 4).SequenceEqual(WebpHeader))
        {
            return (WebpMime, "webp");
        }

        return null;
    }
}
