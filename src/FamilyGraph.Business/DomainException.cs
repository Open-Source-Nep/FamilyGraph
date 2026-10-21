namespace FamilyGraph.Business;

/// <summary>
/// Base type for domain rule violations (D-007). <see cref="Code"/> is a stable identifier
/// that callers can map to responses or translated messages; <see cref="Exception.Message"/>
/// explains the violation in English.
/// </summary>
public abstract class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
