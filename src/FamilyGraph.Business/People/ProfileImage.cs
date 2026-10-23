namespace FamilyGraph.Business.People;

/// <summary>
/// The image of a person profile, stored apart from <see cref="Person"/> so loading a profile
/// never loads the image (D-028). The Client resizes and re-encodes it before upload; the server
/// only accepts a known format within <see cref="MaxBytes"/>. Only <see cref="Person.SetImage"/>
/// creates one, so only the maintainer can.
/// </summary>
public sealed class ProfileImage
{
    /// <summary>1 MiB. The Client sends 512 px JPEGs, which are far smaller.</summary>
    public const int MaxBytes = 1024 * 1024;

    private ProfileImage(ProfileImageId id, PersonId personId, ImageFormat format, byte[] content)
    {
        Id = id;
        PersonId = personId;
        Format = format;
        Content = content;
    }

    /// <summary>For persistence, which sets the properties after construction.</summary>
    private ProfileImage()
    {
        Format = null!;
        Content = null!;
    }

    public ProfileImageId Id { get; }

    public PersonId PersonId { get; }

    public ImageFormat Format { get; }

    public byte[] Content { get; }

    internal static ProfileImage Create(PersonId personId, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length > MaxBytes)
        {
            throw new ProfileImageTooLargeException();
        }

        var format = ImageFormat.Detect(content) ?? throw new UnsupportedImageFormatException();
        return new ProfileImage(ProfileImageId.New(), personId, format, content);
    }
}

public sealed class ProfileImageTooLargeException()
    : DomainException("profile_image.too_large", $"A profile image can be at most {ProfileImage.MaxBytes} bytes.");

public sealed class UnsupportedImageFormatException()
    : DomainException("profile_image.unsupported_format", "A profile image must be a JPEG, PNG or WebP image.");
