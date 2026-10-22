namespace FamilyGraph.Application.Relationships;

public sealed class LinkNotFoundException()
    : NotFoundException("link.not_found", "This link does not exist.");
