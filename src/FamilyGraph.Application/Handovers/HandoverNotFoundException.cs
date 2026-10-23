namespace FamilyGraph.Application.Handovers;

/// <summary>No handover link has this token, or the profile has no open link.</summary>
public sealed class HandoverNotFoundException()
    : NotFoundException("handover.not_found", "This handover link does not exist.");
