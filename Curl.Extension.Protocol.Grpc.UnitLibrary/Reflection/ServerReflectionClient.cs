using Curl.Extension.Protocol.Grpc.Client;
using Curl.Extension.Protocol.Grpc.Wire;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Grpc.Reflection;

/// <summary>
/// Learns a method's request and response types from the server by gRPC server reflection v1 (ADR-1149), for a call made
/// without <c>--grpc-proto</c>: asks for the file containing the service (<c>file_containing_symbol</c>), then for each
/// dependency no file it has yet declares (<c>file_by_filename</c>), and builds the types from the files.
/// </summary>
internal static class ServerReflectionClient
{
    /// <summary>Describes the method the call invokes.</summary>
    /// <param name="session">The call's connection.</param>
    /// <param name="callTarget">The call: its scheme, authority, service and method.</param>
    /// <param name="timeout">The <c>grpc-timeout</c> to send, or <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">Cancels the reflection.</param>
    /// <returns>The method's message types.</returns>
    /// <exception cref="GrpcClientException">
    /// The server does not support reflection or sent an <c>error_response</c> (exit 22), or its files declare no such service
    /// or method (exit 2).
    /// </exception>
    /// <exception cref="GrpcWireException">A reply breaks PROTOCOL-HTTP2.md, or its files do not decode or do not resolve.</exception>
    public static async Task<GrpcProtoMethod> DescribeAsync(GrpcHttp2Session session, GrpcRequestTarget callTarget, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        ServerReflectionStream stream = await ServerReflectionStream.OpenAsync(session, callTarget, timeout, cancellationToken).ConfigureAwait(false);
        var files = new Dictionary<string, ReflectedProtoFile>(StringComparer.Ordinal);
        Add(files, await stream.ExchangeAsync(ServerReflectionRequest.FileContainingSymbol(callTarget.Service), cancellationToken).ConfigureAwait(false));
        while (MissingDependency(files) is { } fileName)
        {
            Add(files, await stream.ExchangeAsync(ServerReflectionRequest.FileByFilename(fileName), cancellationToken).ConfigureAwait(false));
            if (!files.ContainsKey(fileName))
            {
                throw new GrpcWireException($"The server's reflection reply for {fileName} did not hold that file.");
            }
        }

        await stream.CloseAsync(cancellationToken).ConfigureAwait(false);
        return MethodOf([.. files.Values.Select(static file => file.Descriptor)], callTarget);
    }

    private static void Add(Dictionary<string, ReflectedProtoFile> files, ServerReflectionResponse response)
    {
        if (response.Error is { } error)
        {
            throw new GrpcClientException(
                CurlExitCode.HttpReturnedError,
                $"gRPC server reflection failed (error {error.Code}: {error.Message}); give the method's .proto file with --grpc-proto");
        }

        foreach (ReadOnlyMemory<byte> serialized in response.FileDescriptorProtos)
        {
            ReflectedProtoFile file = FileDescriptorProtoDecoder.Decode(serialized);
            files.TryAdd(file.Name, file);
        }
    }

    private static string? MissingDependency(Dictionary<string, ReflectedProtoFile> files) =>
        files.Values.SelectMany(static file => file.Dependencies).FirstOrDefault(name => !files.ContainsKey(name));

    private static GrpcProtoMethod MethodOf(List<ProtoFileDescriptor> files, GrpcRequestTarget target)
    {
        ProtoServiceDescriptor service = files
            .SelectMany(static file => file.Services.Select(service => (Name: QualifiedName(file.Package, service.Name), Service: service)))
            .FirstOrDefault(entry => entry.Name == target.Service).Service
            ?? throw new GrpcClientException(CurlExitCode.FailedInit, $"gRPC server reflection: the server's files declare no service {target.Service}");
        ProtoMethodDescriptor method = service.Methods.FirstOrDefault(method => method.Name == target.Method)
            ?? throw new GrpcClientException(CurlExitCode.FailedInit, $"gRPC server reflection: service {target.Service} has no method {target.Method}");
        var catalogue = new DynamicTypeCatalogue(files);
        try
        {
            return new GrpcProtoMethod(catalogue.GetMessageType(method.InputType.Name), catalogue.GetMessageType(method.OutputType.Name));
        }
        catch (ArgumentException failure)
        {
            throw new GrpcWireException($"The server's reflection files do not resolve: {failure.Message}");
        }
    }

    private static string QualifiedName(string package, string name) => package.Length == 0 ? name : $"{package}.{name}";
}
