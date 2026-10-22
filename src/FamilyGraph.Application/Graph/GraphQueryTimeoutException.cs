namespace FamilyGraph.Application.Graph;

/// <summary>A graph query ran past its timeout (D-009). The API answers 503 <c>graph.timeout</c>.</summary>
public sealed class GraphQueryTimeoutException(Exception innerException)
    : Exception("The graph query took too long. Try again later.", innerException)
{
    public string Code => "graph.timeout";
}
