namespace FamilyGraph.Business;

/// <summary>
/// A rule violation caused by the current state rather than by the request itself, such as a
/// link that already exists (D-007). The API answers 409 (D-010).
/// </summary>
public abstract class ConflictException(string code, string message) : DomainException(code, message);
