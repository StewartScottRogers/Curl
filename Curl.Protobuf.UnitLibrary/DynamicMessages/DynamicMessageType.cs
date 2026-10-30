using Curl.Protobuf.Descriptors;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>A message type from a <see cref="DynamicTypeCatalogue" />: a descriptor whose fields' types are resolved.</summary>
public sealed class DynamicMessageType
{
    private readonly Dictionary<int, DynamicField> fieldsByNumber = [];
    private readonly Dictionary<string, DynamicField> fieldsByName = new(StringComparer.Ordinal);
    private DynamicField[] fields = [];

    internal DynamicMessageType(string fullName, ProtoMessageDescriptor descriptor)
    {
        FullName = fullName;
        Descriptor = descriptor;
    }

    /// <summary>The message's full name, its package and enclosing messages before it: <c>pkg.Outer.Inner</c>.</summary>
    public string FullName { get; }

    /// <summary>The message as the <c>.proto</c> file declares it.</summary>
    public ProtoMessageDescriptor Descriptor { get; }

    /// <summary>The message's fields in field number order, the order they are written in.</summary>
    public IReadOnlyList<DynamicField> Fields => fields;

    /// <summary>The field of this number, or null when the message has none.</summary>
    public DynamicField? FindField(int number) => fieldsByNumber.GetValueOrDefault(number);

    /// <summary>The field of this name, or null when the message has none.</summary>
    public DynamicField? FindField(string name) => fieldsByName.GetValueOrDefault(name);

    internal void SetFields(IEnumerable<DynamicField> resolved)
    {
        fields = [.. resolved.OrderBy(field => field.Number)];
        foreach (var field in fields)
        {
            fieldsByNumber[field.Number] = field;
            fieldsByName[field.Name] = field;
        }
    }
}
