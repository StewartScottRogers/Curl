using System.Runtime.InteropServices;
using Curl.Protobuf.Wire;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>
/// A message of a <see cref="DynamicMessageType" />: its field values by field, and the
/// fields it did not recognise when decoded, kept to be written back.
/// </summary>
public sealed class DynamicMessage
{
    private readonly Dictionary<int, object> values = [];

    /// <summary>An empty message of this type: every field unset.</summary>
    public DynamicMessage(DynamicMessageType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type = type;
    }

    /// <summary>The message's type.</summary>
    public DynamicMessageType Type { get; }

    /// <summary>The fields read that the type does not have, or have another wire type than the type's field; written back after the known fields.</summary>
    public IList<ProtobufField> UnknownFields { get; } = [];

    /// <summary>Whether a singular field is set; for a repeated or map field, whether it holds anything.</summary>
    public bool HasValue(string fieldName) => values.TryGetValue(FieldNamed(fieldName).Number, out var value) && value switch
    {
        List<object> list => list.Count > 0,
        Dictionary<object, object> map => map.Count > 0,
        _ => true,
    };

    /// <summary>A singular field's value, or its default (<see cref="DynamicField.CreateDefaultValue" />) when unset.</summary>
    public object GetValue(string fieldName)
    {
        var field = SingularFieldNamed(fieldName);
        return values.GetValueOrDefault(field.Number) ?? field.CreateDefaultValue();
    }

    /// <summary>
    /// Sets a singular field, clearing any other field of its oneof. Throws
    /// <see cref="ArgumentException" /> when the value is not of the .NET type the field holds.
    /// </summary>
    public void SetValue(string fieldName, object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var field = SingularFieldNamed(fieldName);
        if (!field.Accepts(value))
        {
            throw new ArgumentException($"Field {field.Name} of {Type.FullName} holds {field.Kind}, not {value.GetType().Name}.", nameof(value));
        }

        SetSingular(field, value);
    }

    /// <summary>Unsets a field; a repeated or map field is emptied.</summary>
    public void ClearValue(string fieldName) => values.Remove(FieldNamed(fieldName).Number);

    /// <summary>
    /// A repeated (not map) field's values, to read or change in place. Each value must be of
    /// the .NET type the field holds (<see cref="DynamicValueKind" />), or encoding throws.
    /// </summary>
    public IList<object> GetRepeated(string fieldName)
    {
        var field = FieldNamed(fieldName);
        return field.IsRepeated && !field.IsMap
            ? Repeated(field)
            : throw new InvalidOperationException($"Field {field.Name} of {Type.FullName} is not a repeated field.");
    }

    /// <summary>
    /// A map field's entries, to read or change in place. Keys and values must be of the .NET
    /// types the entry's <c>key</c> and <c>value</c> fields hold, or encoding throws.
    /// </summary>
    public IDictionary<object, object> GetMap(string fieldName)
    {
        var field = FieldNamed(fieldName);
        return field.IsMap
            ? Map(field)
            : throw new InvalidOperationException($"Field {field.Name} of {Type.FullName} is not a map field.");
    }

    /// <summary>The name of the field of a oneof that is set, or null when none is.</summary>
    public string? GetOneofCase(string oneofName) =>
        Type.Fields.FirstOrDefault(field => field.Descriptor.OneofName == oneofName && values.ContainsKey(field.Number))?.Name;

    internal object? FindValue(int fieldNumber) => values.GetValueOrDefault(fieldNumber);

    internal void SetSingular(DynamicField field, object value)
    {
        if (field.Descriptor.OneofName is { } oneofName)
        {
            foreach (var member in Type.Fields.Where(member => member.Descriptor.OneofName == oneofName))
            {
                values.Remove(member.Number);
            }
        }

        values[field.Number] = value;
    }

    internal List<object> Repeated(DynamicField field) => (List<object>)GetOrAdd(field, static () => new List<object>());

    internal Dictionary<object, object> Map(DynamicField field) => (Dictionary<object, object>)GetOrAdd(field, static () => new Dictionary<object, object>());

    private object GetOrAdd(DynamicField field, Func<object> create)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(values, field.Number, out _);
        value ??= create();
        return value;
    }

    private DynamicField FieldNamed(string fieldName)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        return Type.FindField(fieldName)
            ?? throw new ArgumentException($"{Type.FullName} has no field named {fieldName}.", nameof(fieldName));
    }

    private DynamicField SingularFieldNamed(string fieldName)
    {
        var field = FieldNamed(fieldName);
        return field.IsRepeated
            ? throw new InvalidOperationException($"Field {field.Name} of {Type.FullName} is repeated; use {nameof(GetRepeated)} or {nameof(GetMap)}.")
            : field;
    }
}
