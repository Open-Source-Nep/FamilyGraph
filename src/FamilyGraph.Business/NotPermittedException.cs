namespace FamilyGraph.Business;

/// <summary>
/// A rule violation where the acting account lacks the authority for the operation, such as a
/// change by someone other than the profile's maintainer (D-007). The API answers 403 (D-010).
/// </summary>
public abstract class NotPermittedException(string code, string message) : DomainException(code, message);
