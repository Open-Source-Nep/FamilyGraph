namespace FamilyGraph.Business.People;

/// <summary>The image formats a profile image may have, recognized by their file signature (D-028).</summary>
public sealed class ImageFormat : BaseEnum<ImageFormat>
{
    public static readonly ImageFormat Jpeg = new(1, nameof(Jpeg), "image/jpeg");

    public static readonly ImageFormat Png = new(2, nameof(Png), "image/png");

    public static readonly ImageFormat WebP = new(3, nameof(WebP), "image/webp");

    private ImageFormat(int value, string name, string mediaType)
        : base(value, name)
    {
        MediaType = mediaType;
    }

    public string MediaType { get; }

    /// <summary>The format whose signature <paramref name="content"/> starts with, or <see langword="null"/>.</summary>
    public static ImageFormat? Detect(ReadOnlySpan<byte> content) =>
        content.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) ? Jpeg
        : content.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ? Png
        : content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8) ? WebP
        : null;
}
