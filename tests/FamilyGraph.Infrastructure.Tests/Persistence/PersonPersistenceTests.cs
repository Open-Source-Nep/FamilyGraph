using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Tests.Persistence;

public class PersonPersistenceTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("Asha", null, null)]
    [InlineData("Asha", "Devi", "Rai")]
    public async Task Person_round_trips_name_and_maintainer(string first, string? middle, string? last)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = await postgres.NewMigratedDatabaseAsync();
        var maintainer = TestData.NewAccount();
        var person = Person.Create(new PersonName(first, middle, last), maintainer);

        await using (var context = new FamilyGraphDbContext(options))
        {
            context.Add(person);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var context = new FamilyGraphDbContext(options))
        {
            var loaded = await context.Set<Person>().SingleAsync(p => p.Id == person.Id, cancellationToken);

            Assert.Equal(person.Name, loaded.Name);
            Assert.True(loaded.IsMaintainedBy(maintainer));
            Assert.Empty(loaded.PendingEvents);
        }
    }
}
