namespace FamilyGraph.Application.People;

/// <summary>The profile has no image, or the viewer sees it as a placeholder, which never shows one.</summary>
public sealed class ProfileImageNotFoundException()
    : NotFoundException("profile_image.not_found", "This profile has no image.");
