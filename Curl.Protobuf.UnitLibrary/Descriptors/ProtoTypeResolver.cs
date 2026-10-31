namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Gives every message and enum its fully qualified name and resolves each named type reference
/// as protoc does: the innermost enclosing scope outward, among the types of the file itself,
/// the files it imports, and the files those import <c>public</c>.
/// </summary>
internal sealed class ProtoTypeResolver
{
    private readonly Dictionary<string, ProtoNamedType> types = [];

    private readonly Dictionary<ProtoTypeReference, ProtoNamedType> resolved = new(ReferenceEqualityComparer.Instance);

    /// <summary>Resolves every named type reference of the files, given each after the files it imports.</summary>
    public static ProtoFileSet Resolve(IReadOnlyList<ProtoParsedFile> files, IReadOnlyDictionary<string, ProtoParsedFile> filesByPath)
    {
        var resolver = new ProtoTypeResolver();
        foreach (var file in files)
        {
            var descriptor = file.File.Descriptor;
            resolver.CollectTypes(file.File.Path, PackageScope(descriptor), descriptor.Messages, descriptor.Enums);
        }

        foreach (var file in files)
        {
            resolver.ResolveFile(file, VisibleFiles(file, filesByPath));
        }

        return new ProtoFileSet(files.Select(file => file.File).ToList(), resolver.types, resolver.resolved);
    }

    private static string PackageScope(ProtoFileDescriptor file) => file.Package.Length == 0 ? string.Empty : "." + file.Package;

    private void CollectTypes(string path, string scope, IReadOnlyList<ProtoMessageDescriptor> messages, IReadOnlyList<ProtoEnumDescriptor> enums)
    {
        foreach (var message in messages)
        {
            var fullName = $"{scope}.{message.Name}";
            types.TryAdd(fullName, new ProtoNamedType(fullName, path, message, null));
            CollectTypes(path, fullName, message.Messages, message.Enums);
        }

        foreach (var enumDescriptor in enums)
        {
            var fullName = $"{scope}.{enumDescriptor.Name}";
            types.TryAdd(fullName, new ProtoNamedType(fullName, path, null, enumDescriptor));
        }
    }

    /// <summary>The paths of the file, the files it imports, and the files those import <c>public</c>, at any depth.</summary>
    private static HashSet<string> VisibleFiles(ProtoParsedFile file, IReadOnlyDictionary<string, ProtoParsedFile> filesByPath)
    {
        var visible = new HashSet<string> { file.File.Path };
        foreach (var import in file.File.Descriptor.Imports)
        {
            AddWithPublicImports(import.Path, filesByPath, visible);
        }

        return visible;
    }

    private static void AddWithPublicImports(string path, IReadOnlyDictionary<string, ProtoParsedFile> filesByPath, HashSet<string> visible)
    {
        if (!visible.Add(path))
        {
            return;
        }

        foreach (var import in filesByPath[path].File.Descriptor.Imports.Where(import => import.Kind == ProtoImportKind.Public))
        {
            AddWithPublicImports(import.Path, filesByPath, visible);
        }
    }

    private void ResolveFile(ProtoParsedFile file, HashSet<string> visible)
    {
        var scope = PackageScope(file.File.Descriptor);
        ResolveMessages(file, visible, scope, file.File.Descriptor.Messages);
        foreach (var method in file.File.Descriptor.Services.SelectMany(service => service.Methods))
        {
            ResolveMessageType(file, visible, scope, method.InputType);
            ResolveMessageType(file, visible, scope, method.OutputType);
        }
    }

    private void ResolveMessages(ProtoParsedFile file, HashSet<string> visible, string scope, IReadOnlyList<ProtoMessageDescriptor> messages)
    {
        foreach (var message in messages)
        {
            var fullName = $"{scope}.{message.Name}";
            foreach (var field in message.Fields)
            {
                ResolveIfNamed(file, visible, fullName, field.Type);
                ResolveIfNamed(file, visible, fullName, field.MapValue);
            }

            ResolveMessages(file, visible, fullName, message.Messages);
        }
    }

    private void ResolveIfNamed(ProtoParsedFile file, HashSet<string> visible, string scope, ProtoTypeReference? reference)
    {
        if (reference is { Type: ProtoFieldType.Named })
        {
            Resolve(file, visible, scope, reference);
        }
    }

    private void ResolveMessageType(ProtoParsedFile file, HashSet<string> visible, string scope, ProtoTypeReference reference)
    {
        if (Resolve(file, visible, scope, reference).Message is null)
        {
            throw At(file, reference, ProtoFileErrorKind.NotAMessage, $"Type \"{reference.Name}\" is an enum, not a message.");
        }
    }

    private ProtoNamedType Resolve(ProtoParsedFile file, HashSet<string> visible, string scope, ProtoTypeReference reference)
    {
        foreach (var candidate in Candidates(reference.Name, scope))
        {
            if (types.TryGetValue(candidate, out var type) && visible.Contains(type.Path))
            {
                resolved.Add(reference, type);
                return type;
            }
        }

        throw At(file, reference, ProtoFileErrorKind.UnresolvedType, $"Type \"{reference.Name}\" is not defined.");
    }

    /// <summary>The fully qualified names a reference may mean, innermost scope first.</summary>
    private static IEnumerable<string> Candidates(string name, string scope)
    {
        if (name.StartsWith('.'))
        {
            yield return name;
            yield break;
        }

        while (true)
        {
            yield return $"{scope}.{name}";
            if (scope.Length == 0)
            {
                yield break;
            }

            scope = scope[..scope.LastIndexOf('.')];
        }
    }

    private static ProtoFileException At(ProtoParsedFile file, ProtoTypeReference reference, ProtoFileErrorKind kind, string message)
    {
        var token = file.ReferenceTokens[reference];
        return new ProtoFileException(new ProtoFileError(file.File.Path, kind, token.Line, token.Column, message));
    }
}
