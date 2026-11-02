namespace FamilyGraph.Business.Kinship;

/// <summary>
/// A rule's condition on a child's place among their parent's children (D-048): the
/// <see cref="Number"/>th by birth order (1 is the eldest), or the <see cref="Youngest"/>, counted
/// among all the parent's children or, with <see cref="SameGender"/>, among those of the child's
/// gender (Jetho, Maila, Kanchha).
/// </summary>
public sealed record ChildPosition
{
    public ChildPosition(int? number, bool youngest, bool sameGender)
    {
        if ((number is null) == !youngest)
        {
            throw new ArgumentException("A child position is either a number or the youngest.");
        }
        if (number is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Positions start at 1.");
        }

        Number = number;
        Youngest = youngest;
        SameGender = sameGender;
    }

    public int? Number { get; }

    public bool Youngest { get; }

    public bool SameGender { get; }

    public static ChildPosition Nth(int number, bool sameGender = false) => new(number, false, sameGender);

    public static ChildPosition Last(bool sameGender = false) => new(null, true, sameGender);

    internal bool Matches(KinshipStepFacts facts) =>
        (SameGender ? facts.SameGenderRank : facts.Rank) is { } rank
        && (Youngest ? rank.Youngest : rank.Number == Number);
}
