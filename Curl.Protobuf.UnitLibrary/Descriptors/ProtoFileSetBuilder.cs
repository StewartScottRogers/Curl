namespace Curl.Protobuf.Descriptors;

/// <summary>Reads a file and its imports depth first, each import once, failing on a missing import or a cycle.</summary>
internal sealed class ProtoFileSetBuilder(IProtoFileReader reader)
{
    private readonly Dictionary<string, ProtoParsedFile> read = [];

    private readonly HashSet<string> reading = [];

    private readonly List<ProtoParsedFile> ordered = [];

    /// <summary>Reads the file at the path given, its imports, and resolves every named type.</summary>
    public ProtoFileSet Build(string path)
    {
        if (!reader.TryReadFile(path, out var text))
        {
            throw new ProtoFileException(new ProtoFileError(path, ProtoFileErrorKind.FileNotFound, 0, 0, $"File \"{path}\" was not found."));
        }

        ReadFile(path, text);
        return ProtoTypeResolver.Resolve(ordered, read);
    }

    private void ReadFile(string path, string text)
    {
        var parsed = Parse(path, text);
        reading.Add(path);
        foreach (var import in parsed.File.Descriptor.Imports)
        {
            ReadImport(path, import);
        }

        reading.Remove(path);
        read.Add(path, parsed);
        ordered.Add(parsed);
    }

    private void ReadImport(string importer, ProtoImportDescriptor import)
    {
        if (reading.Contains(import.Path))
        {
            throw Error(importer, import, ProtoFileErrorKind.ImportCycle, $"Import \"{import.Path}\" leads back to a file that imports it.");
        }

        if (read.ContainsKey(import.Path))
        {
            return;
        }

        if (!reader.TryReadFile(import.Path, out var text))
        {
            throw Error(importer, import, ProtoFileErrorKind.MissingImport, $"Import \"{import.Path}\" was not found.");
        }

        ReadFile(import.Path, text);
    }

    private static ProtoParsedFile Parse(string path, string text)
    {
        try
        {
            var cursor = new ProtoTokenCursor(text, ProtoTokenizer.Tokenize(text));
            return new ProtoParsedFile(new ProtoFile(path, ProtoFileParser.ParseFile(cursor)), cursor.ReferenceTokens);
        }
        catch (ProtoSyntaxException exception)
        {
            var error = exception.Error;
            throw new ProtoFileException(new ProtoFileError(path, ProtoFileErrorKind.Syntax, error.Line, error.Column, error.Message));
        }
    }

    private static ProtoFileException Error(string importer, ProtoImportDescriptor import, ProtoFileErrorKind kind, string message) =>
        new(new ProtoFileError(importer, kind, import.Line, import.Column, message));
}
