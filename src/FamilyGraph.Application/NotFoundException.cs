namespace FamilyGraph.Application;

/// <summary>
/// Something a use case needs does not exist (D-007). <see cref="Code"/> is a stable identifier
/// like a domain error code; the API answers 404 (D-010).
/// </summary>
public abstract class NotFoundException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
