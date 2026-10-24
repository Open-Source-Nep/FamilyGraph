using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Infrastructure.Tests.Persistence;

internal static class TestData
{
    public static AccountId NewAccount() => new(Guid.NewGuid());

    public static Person NewPerson(AccountId maintainer, string firstName = "Asha") =>
        Person.Create(new PersonName(firstName), maintainer);

    public static ParentChildLink NewLink(Person parent, Person child, AccountId creator) =>
        ParentChildLink.Create(
            parent,
            child,
            ParentChildType.Biological,
            creator,
            new HashSet<PersonId>(),
            new AncestorSnapshot(parent.Id, new HashSet<PersonId>(), isComplete: true), married: false);
}
