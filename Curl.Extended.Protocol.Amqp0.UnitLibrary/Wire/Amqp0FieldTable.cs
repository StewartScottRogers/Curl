namespace Curl.Extended.Protocol.Amqp0.Wire;

/// <summary>
/// A field table (section 4.2.5.5): named, tagged values in the order they are written. A name appears once at most
/// in a table a broker accepts, but the order and every field read are kept as they came.
/// </summary>
internal sealed class Amqp0FieldTable
{
    /// <summary>A table with no fields.</summary>
    public static readonly Amqp0FieldTable Empty = new([]);

    /// <summary>Creates a table of <paramref name="fields"/>.</summary>
    public Amqp0FieldTable(IReadOnlyList<KeyValuePair<string, Amqp0FieldValue>> fields) => Fields = fields;

    /// <summary>The fields, in order.</summary>
    public IReadOnlyList<KeyValuePair<string, Amqp0FieldValue>> Fields { get; }

    /// <summary>The value of the first field named <paramref name="name"/>, or none.</summary>
    public Amqp0FieldValue? Find(string name) =>
        Fields.FirstOrDefault(field => field.Key == name).Value;
}
