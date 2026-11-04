namespace FamilyGraph.Application.Claims;

public sealed class ClaimNotFoundException()
    : NotFoundException("claim.not_found", "This claim does not exist.");
