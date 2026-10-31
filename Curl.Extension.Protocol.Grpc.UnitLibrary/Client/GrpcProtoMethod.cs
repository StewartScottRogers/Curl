using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Grpc.Client;

/// <summary>The request and response message types of the method a call invokes, as a <c>.proto</c> file declares them (ADR-1147).</summary>
/// <param name="Input">The request message type.</param>
/// <param name="Output">The response message type.</param>
internal sealed record GrpcProtoMethod(DynamicMessageType Input, DynamicMessageType Output)
{
    /// <summary>Reads the <c>.proto</c> file, and every file it imports from its own directory, and finds the method.</summary>
    /// <param name="protoPath">The <c>--grpc-proto</c> file.</param>
    /// <param name="target">The service and method the URL names.</param>
    /// <returns>The method's message types.</returns>
    /// <exception cref="GrpcClientException">
    /// A file is missing or does not parse (exit 26), or no file declares the service or the service has no such method (exit 2).
    /// </exception>
    public static GrpcProtoMethod Load(string protoPath, GrpcMethodPath target)
    {
        string fullPath = Path.GetFullPath(protoPath);
        var reader = new ProtoFileDirectoryReader(Path.GetDirectoryName(fullPath)!);
        if (!ProtoFileSetReader.TryRead(Path.GetFileName(fullPath), reader, out ProtoFileSet? files, out ProtoFileError? error))
        {
            throw new GrpcClientException(CurlExitCode.ReadError, $"--grpc-proto: {error.Path}:{error.Line}:{error.Column}: {error.Message}");
        }

        ProtoMethodDescriptor method = FindMethod(files, target, protoPath);
        var catalogue = new DynamicTypeCatalogue(files.Files.Select(static file => file.Descriptor));
        return new GrpcProtoMethod(
            catalogue.GetMessageType(files.FindType(method.InputType)!.FullName),
            catalogue.GetMessageType(files.FindType(method.OutputType)!.FullName));
    }

    private static ProtoMethodDescriptor FindMethod(ProtoFileSet files, GrpcMethodPath target, string protoPath)
    {
        ProtoServiceDescriptor service = files.Files
            .SelectMany(static file => file.Descriptor.Services.Select(service => (Name: QualifiedName(file.Descriptor.Package, service.Name), Service: service)))
            .FirstOrDefault(entry => entry.Name == target.Service).Service
            ?? throw new GrpcClientException(CurlExitCode.FailedInit, $"--grpc-proto: {protoPath} declares no service {target.Service}");
        return service.Methods.FirstOrDefault(method => method.Name == target.Method)
            ?? throw new GrpcClientException(CurlExitCode.FailedInit, $"--grpc-proto: service {target.Service} has no method {target.Method}");
    }

    private static string QualifiedName(string package, string name) => package.Length == 0 ? name : $"{package}.{name}";
}
