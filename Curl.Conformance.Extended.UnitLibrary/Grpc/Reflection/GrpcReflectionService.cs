namespace Curl.Conformance.Extended.Grpc.Reflection;

/// <summary>
/// The <c>grpc.reflection.v1.ServerReflection</c> service written from grpc/grpc's <c>doc/server-reflection.md</c> and
/// <c>reflection.proto</c>. Once the client's first <c>ServerReflectionRequest</c> arrives it answers the whole stream: the
/// file the request names (or an <c>error_response</c> NOT_FOUND), then, unless <c>sendsDependencies</c>, one
/// <c>file_by_filename</c> answer for each file that file imports, directly or not, in the breadth-first order a client
/// that asks for each missing dependency in turn asks for them; then <c>grpc-status 0</c>. The reference server sends one
/// reply per call, so the later answers go out ahead of the requests they answer; a case reads the requests curl actually
/// sent from the recorded call.
/// </summary>
/// <param name="files">The files the service describes.</param>
/// <param name="sendsDependencies">Whether the first answer carries the file and every file it imports, as reflection.proto allows, so the client needs no <c>file_by_filename</c>.</param>
public sealed class GrpcReflectionService(IReadOnlyList<GrpcReflectedFile> files, bool sendsDependencies = false) : IGrpcReferenceService
{
    /// <summary>The reflection method's <c>:path</c>.</summary>
    public const string Path = "/grpc.reflection.v1.ServerReflection/ServerReflectionInfo";

    /// <summary>The <c>error_code</c> of a request for a symbol or file the service does not have: NOT_FOUND.</summary>
    public const int NotFoundStatus = 5;

    private const int OriginalRequestField = 2;

    private const int FileDescriptorResponseField = 4;

    private const int ErrorResponseField = 7;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">A file imports a file the service was not given.</exception>
    public GrpcReferenceReply? Answer(GrpcReferenceCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.Path != Path || call.Messages.Count == 0)
        {
            return null;
        }

        byte[] request = call.Messages[0].Payload;
        GrpcReflectionRequest parsed = GrpcReflectionRequest.Parse(request);
        if (Find(parsed) is not { } file)
        {
            return GrpcReferenceReply.Ok(ErrorResponse(request, $"{parsed.Value} not found"));
        }

        List<GrpcReflectedFile> dependencies = TransitiveDependencies(file);
        return sendsDependencies
            ? GrpcReferenceReply.Ok(FileResponse(request, [file, .. dependencies]))
            : GrpcReferenceReply.Ok([FileResponse(request, [file]), .. dependencies.Select(static dependency => FileResponse(GrpcReflectionRequest.FileByFilename(dependency.Name), [dependency]))]);
    }

    private GrpcReflectedFile? Find(GrpcReflectionRequest request) => request.FieldNumber switch
    {
        GrpcReflectionRequest.FileContainingSymbolField => files.FirstOrDefault(file => file.Symbols.Contains(request.Value)),
        GrpcReflectionRequest.FileByFilenameField => FileNamed(request.Value),
        _ => null,
    };

    private GrpcReflectedFile? FileNamed(string name) => files.FirstOrDefault(file => file.Name == name);

    private List<GrpcReflectedFile> TransitiveDependencies(GrpcReflectedFile root)
    {
        List<GrpcReflectedFile> found = [];
        var named = new HashSet<string>(StringComparer.Ordinal) { root.Name };
        var pending = new Queue<GrpcReflectedFile>([root]);
        while (pending.TryDequeue(out GrpcReflectedFile? file))
        {
            foreach (string name in file.Dependencies.Where(named.Add))
            {
                GrpcReflectedFile dependency = FileNamed(name)
                    ?? throw new InvalidOperationException($"{file.Name} imports {name}, which the case gave the reflection service no file for.");
                found.Add(dependency);
                pending.Enqueue(dependency);
            }
        }

        return found;
    }

    private static byte[] FileResponse(byte[] request, IReadOnlyList<GrpcReflectedFile> answer)
    {
        var descriptors = new ProtobufMessageWriter();
        foreach (GrpcReflectedFile file in answer)
        {
            descriptors.Bytes(1, file.Descriptor);
        }

        return new ProtobufMessageWriter().Bytes(OriginalRequestField, request).Message(FileDescriptorResponseField, descriptors).ToArray();
    }

    private static byte[] ErrorResponse(byte[] request, string message) =>
        new ProtobufMessageWriter()
            .Bytes(OriginalRequestField, request)
            .Message(ErrorResponseField, new ProtobufMessageWriter().Varint(1, NotFoundStatus).String(2, message))
            .ToArray();
}
