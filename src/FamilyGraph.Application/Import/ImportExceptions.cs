using FamilyGraph.Business;

namespace FamilyGraph.Application.Import;

/// <summary>One thing wrong with an import file, at <see cref="Path"/> such as <c>people[3]</c>.</summary>
public sealed record ImportProblem(string Path, string Code, string Message);

/// <summary>
/// The file was refused and nothing was saved (D-033, all or nothing). <see cref="Problems"/> holds
/// the first <see cref="ImportService.MaxProblems"/> problems in file order; <see cref="MoreProblems"/>
/// tells whether there were more.
/// </summary>
public sealed class InvalidImportFileException(IReadOnlyList<ImportProblem> problems, bool moreProblems)
    : DomainException("import.invalid_file", "The file cannot be imported. Nothing was saved.")
{
    public IReadOnlyList<ImportProblem> Problems { get; } = problems;

    public bool MoreProblems { get; } = moreProblems;
}

/// <summary>An import only goes into an instance with no profiles (D-033).</summary>
public sealed class InstanceNotEmptyException()
    : ConflictException("import.instance_not_empty", "Data can only be imported into an instance without any profiles.");
