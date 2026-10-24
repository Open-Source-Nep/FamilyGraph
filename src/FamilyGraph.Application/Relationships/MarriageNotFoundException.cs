namespace FamilyGraph.Application.Relationships;

public sealed class MarriageNotFoundException()
    : NotFoundException("marriage.not_found", "This marriage does not exist.");
