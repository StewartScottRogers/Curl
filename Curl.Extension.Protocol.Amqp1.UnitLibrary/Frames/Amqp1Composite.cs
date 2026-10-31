using Curl.Extension.Protocol.Amqp1.Types;

namespace Curl.Extension.Protocol.Amqp1.Frames;

/// <summary>
/// A value of one of the composite types of <see cref="Amqp1CompositeType"/>: its fields in order, as the described
/// list that carries it on the wire (Part 1 section 1.4). Every mandatory field is present and not null.
/// </summary>
internal sealed class Amqp1Composite
{
    private Amqp1Composite(Amqp1CompositeType type, IReadOnlyList<object?> fields)
    {
        for (var index = 0; index < type.FieldNames.Count; index++)
        {
            if (type.IsMandatory(index) && (index >= fields.Count || fields[index] is null))
            {
                throw new Amqp1MalformedEncodingException($"{type.Name} has no {type.FieldNames[index]}, which is mandatory.");
            }
        }

        Type = type;
        Fields = fields;
    }

    /// <summary>The composite type.</summary>
    public Amqp1CompositeType Type { get; }

    /// <summary>The fields in order; trailing fields that are null may be absent.</summary>
    public IReadOnlyList<object?> Fields { get; }

    /// <summary>The value of the field named <paramref name="fieldName"/>, null when absent.</summary>
    public object? this[string fieldName]
    {
        get
        {
            var index = Type.IndexOf(fieldName);
            return index < Fields.Count ? Fields[index] : null;
        }
    }

    /// <summary>
    /// A value of <paramref name="type"/> with the named fields set and every other field null; the list ends at the
    /// last field set, as section 1.4 allows.
    /// </summary>
    public static Amqp1Composite Create(Amqp1CompositeType type, params (string FieldName, object? Value)[] values)
    {
        var fields = new object?[type.FieldNames.Count];
        var length = 0;
        foreach (var (fieldName, value) in values)
        {
            var index = type.IndexOf(fieldName);
            fields[index] = value;
            length = Math.Max(length, index + 1);
        }

        return new Amqp1Composite(type, fields[..length]);
    }

    /// <summary>
    /// Reads a composite from the value <see cref="Amqp1Reader"/> read: an <see cref="Amqp1Described"/> whose
    /// descriptor names a type of <see cref="Amqp1CompositeType"/> and whose value is a list.
    /// </summary>
    public static Amqp1Composite Read(object? value)
    {
        var described = value as Amqp1Described
            ?? throw new Amqp1MalformedEncodingException($"A {value?.GetType().Name ?? "null"} is not a described AMQP composite.");
        var type = Amqp1CompositeType.FromDescriptor(described.Descriptor);
        var fields = described.Value as IReadOnlyList<object?>
            ?? throw new Amqp1MalformedEncodingException($"{type.Name} is described, but its value is not a list.");
        return new Amqp1Composite(type, fields);
    }

    /// <summary>The described list to write, with the type's numeric descriptor.</summary>
    public Amqp1Described ToDescribed() => new(Type.Code, Fields);
}
