using Curl.Protobuf.Descriptors;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>
/// The messages and enums of a set of <c>.proto</c> files by full name, from which
/// <see cref="DynamicMessageType" />s are made with every field's named type resolved.
/// </summary>
public sealed class DynamicTypeCatalogue
{
    private readonly Dictionary<string, ProtoMessageDescriptor> messages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProtoEnumDescriptor> enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DynamicMessageType> types = new(StringComparer.Ordinal);
    private readonly List<string> createdTypeNames = [];

    /// <summary>Catalogues the messages and enums of every file, nested ones under their message's name.</summary>
    public DynamicTypeCatalogue(IEnumerable<ProtoFileDescriptor> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        foreach (var file in files)
        {
            Catalogue(file.Package, file.Messages, file.Enums);
        }
    }

    /// <summary>
    /// The message type of this full name (a leading '.' allowed), every message type it
    /// reaches resolved with it. Throws <see cref="ArgumentException" /> when the name, or a
    /// type one of its fields names, is not in the catalogue. Safe to call from several threads.
    /// </summary>
    public DynamicMessageType GetMessageType(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        lock (types)
        {
            createdTypeNames.Clear();
            try
            {
                return GetOrCreateMessageType(fullName);
            }
            catch (ArgumentException)
            {
                // A type whose fields did not all resolve is not kept half made.
                foreach (var name in createdTypeNames)
                {
                    types.Remove(name);
                }

                throw;
            }
        }
    }

    private DynamicMessageType GetOrCreateMessageType(string fullName)
    {
        var name = fullName.TrimStart('.');
        if (types.TryGetValue(name, out var known))
        {
            return known;
        }

        var descriptor = messages.GetValueOrDefault(name)
            ?? throw new ArgumentException($"No message is named {fullName}.", nameof(fullName));
        var type = new DynamicMessageType(name, descriptor);
        types[name] = type;
        createdTypeNames.Add(name);
        type.SetFields([.. descriptor.Fields.Select(field => ResolveField(name, field))]);
        return type;
    }

    private static string Qualify(string scope, string name) => scope.Length == 0 ? name : $"{scope}.{name}";

    private static string ParentScope(string scope) => scope[..Math.Max(scope.LastIndexOf('.'), 0)];

    private void Catalogue(string scope, IReadOnlyList<ProtoMessageDescriptor> nestedMessages, IReadOnlyList<ProtoEnumDescriptor> nestedEnums)
    {
        foreach (var nestedEnum in nestedEnums)
        {
            enums[Qualify(scope, nestedEnum.Name)] = nestedEnum;
        }

        foreach (var message in nestedMessages)
        {
            var fullName = Qualify(scope, message.Name);
            messages[fullName] = message;
            Catalogue(fullName, message.Messages, message.Enums);
        }
    }

    private DynamicField ResolveField(string scope, ProtoFieldDescriptor field)
    {
        if (field.Type.Type == ProtoFieldType.Map)
        {
            return new DynamicField(field, DynamicValueKind.Message, CreateMapEntryType(scope, field), null);
        }

        if (field.Type.Type != ProtoFieldType.Named)
        {
            // DynamicValueKind lists the scalar types in ProtoFieldType's order.
            return new DynamicField(field, (DynamicValueKind)field.Type.Type, null, null);
        }

        var fullName = ResolveName(scope, field.Type.Name);
        return enums.TryGetValue(fullName, out var enumType)
            ? new DynamicField(field, DynamicValueKind.Enum, null, enumType)
            : new DynamicField(field, DynamicValueKind.Message, GetOrCreateMessageType(fullName), null);
    }

    private DynamicMessageType CreateMapEntryType(string scope, ProtoFieldDescriptor field)
    {
        var entryName = $"{scope}.{field.Name}Entry";
        var entry = new ProtoMessageDescriptor(
            entryName,
            [
                new ProtoFieldDescriptor("key", 1, ProtoFieldLabel.Optional, field.MapKey!, null, null, null, []),
                new ProtoFieldDescriptor("value", 2, ProtoFieldLabel.Optional, field.MapValue!, null, null, null, []),
            ],
            [],
            [],
            [],
            [],
            [],
            []);
        var type = new DynamicMessageType(entryName, entry);
        type.SetFields([.. entry.Fields.Select(entryField => ResolveField(scope, entryField))]);
        return type;
    }

    /// <summary>
    /// Resolves a type name as written, from the innermost scope out: <c>Inner</c> in
    /// <c>pkg.Outer</c> is tried as <c>pkg.Outer.Inner</c>, then <c>pkg.Inner</c>, then <c>Inner</c>.
    /// </summary>
    private string ResolveName(string scope, string name)
    {
        if (name.StartsWith('.'))
        {
            return name[1..];
        }

        for (var candidateScope = scope; ; candidateScope = ParentScope(candidateScope))
        {
            var candidate = Qualify(candidateScope, name);
            if (messages.ContainsKey(candidate) || enums.ContainsKey(candidate))
            {
                return candidate;
            }

            if (candidateScope.Length == 0)
            {
                throw new ArgumentException($"The type {name} named in {scope} is not defined.", nameof(name));
            }
        }
    }
}
