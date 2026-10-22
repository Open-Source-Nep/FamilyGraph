using System.Runtime.CompilerServices;
using System.Text.Json;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Tests.Persistence;

public class AuditEventRecordTests(PostgresFixture postgres)
{
    public static TheoryData<Type> AllEventTypes() =>
        new(typeof(AuditEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(AuditEvent)) && !t.IsAbstract));

    [Theory]
    [MemberData(nameof(AllEventTypes))]
    public void Every_audit_event_type_can_be_stored(Type eventType)
    {
        var auditEvent = (AuditEvent)RuntimeHelpers.GetUninitializedObject(eventType);

        var record = AuditEventRecord.From(auditEvent, DateTimeOffset.UtcNow);

        Assert.Equal(eventType.Name, record.Type);
    }

    [Fact]
    public async Task Link_created_event_is_stored_with_ids_and_states_only()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = await postgres.NewMigratedDatabaseAsync();
        var account = TestData.NewAccount();
        var parent = TestData.NewPerson(account, "Parent");
        var child = TestData.NewPerson(TestData.NewAccount(), "Child");
        var link = TestData.NewLink(parent, child, account);
        var created = Assert.IsType<LinkCreated>(Assert.Single(link.TakeEvents()));
        var occurredAt = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

        await using (var context = new FamilyGraphDbContext(options))
        {
            // No foreign key to the subject: the record is stored without the link or people rows.
            context.Add(AuditEventRecord.From(created, occurredAt));
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var context = new FamilyGraphDbContext(options))
        {
            var record = await context.Set<AuditEventRecord>().SingleAsync(cancellationToken);

            Assert.Equal(occurredAt, record.OccurredAt);
            Assert.Equal(AuditFamily.RelationshipConsent.Value, record.Family);
            Assert.Equal(nameof(LinkCreated), record.Type);
            Assert.Equal(account.Value, record.ActorId);
            Assert.Equal(link.Id.Value, record.SubjectId);

            using var data = JsonDocument.Parse(record.Data);
            var fields = data.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
            Assert.Equal(
                new Dictionary<string, string>
                {
                    ["linkId"] = link.Id.Value.ToString(),
                    ["parentId"] = parent.Id.Value.ToString(),
                    ["childId"] = child.Id.Value.ToString(),
                    ["type"] = ParentChildType.Biological.Value.ToString(),
                    ["parentApproval"] = EndpointApproval.Approved.Value.ToString(),
                    ["childApproval"] = EndpointApproval.Pending.Value.ToString(),
                },
                fields);
        }
    }
}
