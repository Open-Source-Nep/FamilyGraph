using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FamilyGraph.Business;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Export;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Infrastructure.Persistence;

/// <summary>
/// One row of the append-only audit table (D-015). Family, type, subject and actor are columns;
/// the event's other fields are kept as JSON in <see cref="Data"/>. There is no foreign key to
/// the subject, so the record outlives a deleted row.
/// </summary>
internal sealed class AuditEventRecord
{
    private static readonly JsonSerializerOptions DataOptions = CreateDataOptions();

    private AuditEventRecord(Guid id, DateTimeOffset occurredAt, int family, string type, Guid actorId, Guid subjectId, string data)
    {
        Id = id;
        OccurredAt = occurredAt;
        Family = family;
        Type = type;
        ActorId = actorId;
        SubjectId = subjectId;
        Data = data;
    }

    public Guid Id { get; }

    public DateTimeOffset OccurredAt { get; }

    /// <summary>The <see cref="AuditFamily"/> value.</summary>
    public int Family { get; }

    /// <summary>The event's record type name, e.g. <c>LinkCreated</c>.</summary>
    public string Type { get; }

    public Guid ActorId { get; }

    /// <summary>
    /// The link, marriage or person the event is about; a handover's events are about its person, and an
    /// export is about the exporting account.
    /// </summary>
    public Guid SubjectId { get; }

    /// <summary>The event's fields except actor and family, as a JSON object.</summary>
    public string Data { get; }

    public static AuditEventRecord From(AuditEvent auditEvent, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return new AuditEventRecord(
            Guid.CreateVersion7(occurredAt),
            occurredAt,
            auditEvent.Family.Value,
            auditEvent.GetType().Name,
            auditEvent.ActorId.Value,
            SubjectOf(auditEvent),
            JsonSerializer.Serialize(auditEvent, auditEvent.GetType(), DataOptions));
    }

    private static Guid SubjectOf(AuditEvent auditEvent) => auditEvent switch
    {
        PersonEvent e => e.PersonId.Value,
        LinkEvent e => e.LinkId.Value,
        LinkBirthOrderChanged e => e.LinkId.Value,
        MarriageEvent e => e.MarriageId.Value,
        MarriageFormerChanged e => e.MarriageId.Value,
        HandoverEvent e => e.PersonId.Value,
        DataExported e => e.ActorId.Value,
        _ => throw new ArgumentException($"No audit subject is defined for {auditEvent.GetType().Name}.", nameof(auditEvent)),
    };

    private static JsonSerializerOptions CreateDataOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { ExcludeColumnFields },
            },
        };
        options.Converters.Add(new GuidValueConverter<PersonId>(id => id.Value));
        options.Converters.Add(new GuidValueConverter<LinkId>(id => id.Value));
        options.Converters.Add(new GuidValueConverter<AccountId>(id => id.Value));
        options.Converters.Add(new GuidValueConverter<HandoverId>(id => id.Value));
        options.Converters.Add(new BaseEnumConverterFactory());
        return options;
    }

    private static void ExcludeColumnFields(JsonTypeInfo typeInfo)
    {
        if (!typeInfo.Type.IsAssignableTo(typeof(AuditEvent)))
        {
            return;
        }

        foreach (var property in typeInfo.Properties.ToList())
        {
            if (property.Name is "actorId" or "family")
            {
                typeInfo.Properties.Remove(property);
            }
        }
    }

    private sealed class GuidValueConverter<T>(Func<T, Guid> toGuid) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("Audit data is written here; reading it back comes with the audit queries (#28).");

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(toGuid(value));
    }

    /// <summary>Writes a <see cref="BaseEnum{TEnum}"/> as its integer value, like the columns.</summary>
    private sealed class BaseEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => FindBaseEnum(typeToConvert) is not null;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(BaseEnumConverter<>).MakeGenericType(typeToConvert))!;

        private static Type? FindBaseEnum(Type? type)
        {
            for (; type is not null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BaseEnum<>))
                {
                    return type;
                }
            }

            return null;
        }
    }

    private sealed class BaseEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : BaseEnum<TEnum>
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            BaseEnum<TEnum>.FromValue(reader.GetInt32());

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Value);
    }
}
