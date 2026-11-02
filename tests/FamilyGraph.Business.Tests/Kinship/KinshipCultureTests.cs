using FamilyGraph.Business.Graph;
using FamilyGraph.Business.Kinship;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Kinship;

public class KinshipCultureTests
{
    private static readonly StepRelation Parent = StepRelation.Parent;
    private static readonly StepRelation Child = StepRelation.Child;
    private static readonly StepRelation Spouse = StepRelation.Spouse;

    private static KinshipLabel Label(string ne, string en) => new(new Dictionary<string, string> { ["ne"] = ne, ["en"] = en });

    private static KinshipRule Rule(string en, params KinshipStep[] steps) => new(steps, Label(en, en));

    private static KinshipCulture Culture(params KinshipRule[] rules) => new(
        "ne", ["ne", "en"], new Dictionary<string, string> { ["ne"] = "नेपाली", ["en"] = "Nepali" }, Label("नातेदार", "Relative"), rules);

    private static KinshipCulture Uncles() => Culture(
        Rule("Thulobuwa/Kaka", new(Parent, Gender.Male), new(Parent), new(Child, Gender.Male)),
        Rule("Thulobuwa", new(Parent, Gender.Male), new(Parent), new(Child, Gender.Male, RelativeAge.Elder)),
        Rule("Kaka", new(Parent, Gender.Male), new(Parent), new(Child, Gender.Male, RelativeAge.Younger)),
        Rule("Kanchhabuwa", new(Parent, Gender.Male), new(Parent), new(Child, Gender.Male, RelativeAge.Younger, ChildPosition.Last(sameGender: true))),
        Rule("Mama", new(Parent, Gender.Female), new(Parent), new(Child, Gender.Male)));

    private static string? English(KinshipCulture culture, KinshipFacts facts) =>
        culture.Label(facts) is { } label ? culture.Text(label, "en") : null;

    [Fact]
    [Trait("AC", "AC-08.29")]
    public void Genders_along_the_path_decide_the_label()
    {
        var family = new Family();
        var path = family.UncleThrough(Gender.Female, Gender.Male);

        Assert.Equal("Mama", English(Uncles(), family.Facts(path)));
    }

    [Fact]
    [Trait("AC", "AC-08.30")]
    public void Birth_order_under_the_shared_parents_decides_elder_or_younger()
    {
        var family = new Family { FatherOrder = 1, UncleOrder = 2 };
        var younger = family.UncleThrough(Gender.Male, Gender.Male);
        Assert.Equal("Kaka", English(Uncles(), family.Facts(younger, ranked: false)));

        family = new Family { FatherOrder = 2, UncleOrder = 1 };
        var elder = family.UncleThrough(Gender.Male, Gender.Male);
        Assert.Equal("Thulobuwa", English(Uncles(), family.Facts(elder)));
    }

    [Theory]
    [Trait("AC", "AC-08.30")]
    [InlineData(null, 2)]
    [InlineData(1, null)]
    [InlineData(1, 1)]
    public void Unknown_birth_order_falls_back_to_the_rule_without_age(int? father, int? uncle)
    {
        var family = new Family { FatherOrder = father, UncleOrder = uncle };
        var path = family.UncleThrough(Gender.Male, Gender.Male);

        Assert.Equal("Thulobuwa/Kaka", English(Uncles(), family.Facts(path)));
    }

    [Fact]
    [Trait("AC", "AC-08.30")]
    public void Shared_parents_who_disagree_leave_the_age_unknown()
    {
        var family = new Family { FatherOrder = 1, UncleOrder = 2, GrandmotherOrders = (2, 1) };
        var path = family.UncleThrough(Gender.Male, Gender.Male);

        Assert.Equal("Thulobuwa/Kaka", English(Uncles(), family.Facts(path)));
    }

    [Fact]
    [Trait("AC", "AC-08.31")]
    public void Position_among_children_of_the_same_gender_picks_the_most_specific_rule()
    {
        var family = new Family { FatherOrder = 1, UncleOrder = 3, AuntOrder = 4 };
        var path = family.UncleThrough(Gender.Male, Gender.Male);

        // The aunt is younger, but the uncle is the youngest son.
        Assert.Equal("Kanchhabuwa", English(Uncles(), family.Facts(path)));
    }

    [Fact]
    [Trait("AC", "AC-08.31")]
    public void Position_is_unknown_when_a_sibling_counted_has_no_birth_order_or_a_hidden_gender()
    {
        // A daughter without a birth order does not change the order of the sons.
        var unorderedDaughter = new Family { FatherOrder = 1, UncleOrder = 3, AuntOrder = null };
        Assert.Equal("Kanchhabuwa", English(Uncles(), unorderedDaughter.Facts(unorderedDaughter.UncleThrough(Gender.Male, Gender.Male))));

        var unorderedSon = new Family { FatherOrder = null, UncleOrder = 3, AuntOrder = 4 };
        Assert.Equal("Thulobuwa/Kaka", English(Uncles(), unorderedSon.Facts(unorderedSon.UncleThrough(Gender.Male, Gender.Male))));

        var hidden = new Family { FatherOrder = 1, UncleOrder = 3, AuntOrder = 4, AuntGender = null };
        Assert.Equal("Kaka", English(Uncles(), hidden.Facts(hidden.UncleThrough(Gender.Male, Gender.Male))));

        var notLoaded = new Family { FatherOrder = 1, UncleOrder = 3, AuntOrder = 4 };
        Assert.Equal("Kaka", English(Uncles(), notLoaded.Facts(notLoaded.UncleThrough(Gender.Male, Gender.Male), ranked: false)));
    }

    [Fact]
    [Trait("AC", "AC-08.31")]
    public void Position_among_all_children_counts_every_child()
    {
        var culture = Culture(
            Rule("Child", new KinshipStep(Child)),
            Rule("Youngest", new KinshipStep(Child, position: ChildPosition.Last())),
            Rule("Eldest", new KinshipStep(Child, position: ChildPosition.Nth(1))));
        var family = new Family { FatherOrder = 1, UncleOrder = 3, AuntOrder = 4 };
        var father = family.Grandfather;

        Assert.Equal("Youngest", English(culture, family.FactsFrom(father, family.Aunt)));
        Assert.Equal("Eldest", English(culture, family.FactsFrom(father, family.Father)));
        Assert.Equal("Child", English(culture, family.FactsFrom(father, family.Uncle)));
    }

    [Fact]
    [Trait("AC", "AC-08.29")]
    public void A_hidden_gender_matches_only_rules_without_it()
    {
        var family = new Family();
        var path = family.UncleThrough(null, Gender.Male);

        Assert.Equal("Relative", English(Uncles(), family.Facts(path)));
    }

    [Fact]
    [Trait("AC", "AC-08.29")]
    public void The_start_persons_gender_can_decide_the_label()
    {
        var culture = Culture(
            new KinshipRule([new(Spouse)], Label("x", "Spouse")),
            new KinshipRule([new(Spouse)], Label("x", "Husband"), startGender: Gender.Female));

        Assert.Equal("Husband", English(culture, new KinshipFacts(Gender.Female, [new(Spouse, null)])));
        Assert.Equal("Spouse", English(culture, new KinshipFacts(null, [new(Spouse, null)])));
    }

    [Fact]
    [Trait("AC", "AC-08.28")]
    public void No_matching_rule_gives_the_fallback_and_no_steps_give_no_label()
    {
        var culture = Uncles();

        Assert.Equal("Relative", English(culture, new KinshipFacts(null, [new(Spouse, Gender.Male)])));
        Assert.Null(culture.Label(new KinshipFacts(null, [])));
    }

    [Fact]
    [Trait("AC", "AC-08.33")]
    public void A_label_is_written_in_the_language_or_else_the_cultures_first()
    {
        var culture = Culture();

        Assert.Equal("Relative", culture.Text(culture.Fallback, "en"));
        Assert.Equal("नातेदार", culture.Text(culture.Fallback, "fr"));
    }

    [Fact]
    public void Every_label_needs_each_language_of_the_culture()
    {
        Assert.Throws<ArgumentException>(() => Culture(
            new KinshipRule([new(Parent)], new KinshipLabel(new Dictionary<string, string> { ["en"] = "Parent" }))));
    }

    [Fact]
    public void Elder_or_younger_needs_a_child_step_right_after_a_parent_step()
    {
        Assert.Throws<ArgumentException>(() => new KinshipRule([new(Child, age: RelativeAge.Elder)], Label("x", "x")));
        Assert.Throws<ArgumentException>(() => new KinshipStep(Parent, age: RelativeAge.Elder));
    }

    /// <summary>
    /// You, your father and mother, your father's parents, and his brother (the uncle) and sister.
    /// Your mother's brother is reached through the grandparents too, to keep one family.
    /// </summary>
    private sealed class Family
    {
        public PersonId You { get; } = PersonId.New();
        public PersonId Father { get; } = PersonId.New();
        public PersonId Grandfather { get; } = PersonId.New();
        public PersonId Grandmother { get; } = PersonId.New();
        public PersonId Uncle { get; } = PersonId.New();
        public PersonId Aunt { get; } = PersonId.New();

        public int? FatherOrder { get; init; }
        public int? UncleOrder { get; init; }
        public int? AuntOrder { get; init; } = 5;
        public Gender? AuntGender { get; init; } = Gender.Female;

        /// <summary>The grandmother's own orders for the father and the uncle; unset by default.</summary>
        public (int Father, int Uncle)? GrandmotherOrders { get; init; }

        private Dictionary<PersonId, Gender?> genders = [];

        private List<GraphLink> Links() =>
        [
            new(LinkId.New(), Father, You, ParentChildType.Biological),
            new(LinkId.New(), Grandfather, Father, ParentChildType.Biological, FatherOrder),
            new(LinkId.New(), Grandfather, Uncle, ParentChildType.Biological, UncleOrder),
            new(LinkId.New(), Grandfather, Aunt, ParentChildType.Biological, AuntOrder),
            new(LinkId.New(), Grandmother, Father, ParentChildType.Biological, GrandmotherOrders?.Father),
            new(LinkId.New(), Grandmother, Uncle, ParentChildType.Biological, GrandmotherOrders?.Uncle),
        ];

        public RelationshipPath UncleThrough(Gender? parentGender, Gender? uncleGender)
        {
            genders = new() { [Father] = parentGender, [Uncle] = uncleGender, [Aunt] = AuntGender, [Grandfather] = Gender.Male };
            return new RelationshipPath(You,
            [
                new PathStep(new GraphLink(LinkId.New(), Father, You, ParentChildType.Biological), StepRelation.Parent, Father),
                new PathStep(new GraphLink(LinkId.New(), Grandfather, Father, ParentChildType.Biological), StepRelation.Parent, Grandfather),
                new PathStep(new GraphLink(LinkId.New(), Grandfather, Uncle, ParentChildType.Biological), StepRelation.Child, Uncle),
            ]);
        }

        public KinshipFacts Facts(RelationshipPath path, bool ranked = true) =>
            KinshipFacts.Of(path, id => genders.GetValueOrDefault(id), Links(), ranked ? new HashSet<PersonId> { Grandfather } : []);

        public KinshipFacts FactsFrom(PersonId parent, PersonId child)
        {
            genders = new() { [Father] = Gender.Male, [Uncle] = Gender.Male, [Aunt] = AuntGender };
            var path = new RelationshipPath(parent,
                [new PathStep(new GraphLink(LinkId.New(), parent, child, ParentChildType.Biological), StepRelation.Child, child)]);
            return KinshipFacts.Of(path, id => genders.GetValueOrDefault(id), Links(), new HashSet<PersonId> { parent });
        }
    }
}
