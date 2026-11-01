using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>The <c>grpc://</c> extension's description, its <c>--grpc-proto</c> option, its hook and its handler factory (ADR-1147).</summary>
public static class GrpcExtension
{
    /// <summary>
    /// Gets the <c>--grpc-proto</c> option: the <c>.proto</c> file declaring the service. It is not <c>--proto</c>, which
    /// is curl's own option restricting protocols.
    /// </summary>
    public static ExtensionOption ProtoOption { get; } = new(
        "grpc-proto",
        ExtensionOptionArgument.Value,
        "The .proto file declaring the service a grpc:// or grpcs:// URL calls; files it imports are read from its directory. Without it, the server describes the method by gRPC server reflection v1");

    /// <summary>Gets what <c>curl --extensions-help</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "grpc",
        "Calls a gRPC method over HTTP/2 (grpc.io PROTOCOL-HTTP2.md): grpc://host:port/package.Service/Method over cleartext HTTP/2, grpcs:// over TLS with ALPN h2; --grpc-proto names the .proto file (without it, the method's types come from the server by gRPC server reflection v1), -d the request message as proto3 JSON, and each response message is written as JSON. A grpc-status other than 0 fails with exit 22 and the grpc-message.",
        [GrpcProtocolHandler.Scheme, GrpcProtocolHandler.SecureScheme],
        [ProtoOption],
        new Uri("https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md"));

    /// <summary>Creates the handler over curl's connector, which also runs TLS for <c>grpcs://</c>.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new GrpcProtocolHandler(services.Connector);
    }

    /// <summary>Gives the hook carrying the <c>--grpc-proto</c> file when <paramref name="values" /> hold it, otherwise <see langword="null" />.</summary>
    /// <param name="values">An option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" />.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(ProtoOption) is { } protoPath ? new GrpcProtoTransferHook(protoPath) : null;
    }
}
