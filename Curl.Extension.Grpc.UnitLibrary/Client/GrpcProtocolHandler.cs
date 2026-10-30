using Curl.Extension.Grpc.Reflection;
using Curl.Extension.Grpc.Wire;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Grpc.Client;

/// <summary>
/// Serves <c>grpc://host[:port]/package.Service/Method</c> and <c>grpcs://</c> (ADR-1147): encodes the <c>-d</c> JSON as the
/// <c>--grpc-proto</c> file's request message (or, without one, the type server reflection gives, ADR-1149), calls the method over HTTP/2 (prior knowledge for <c>grpc://</c>, TLS with ALPN
/// <c>h2</c> for <c>grpcs://</c>) and writes each response message as proto3 JSON. A <c>grpc-status</c> other than 0 ends the
/// transfer with exit 22 and the status and its <c>grpc-message</c>.
/// </summary>
/// <param name="connector">Connects TCP, and TLS for <c>grpcs://</c>.</param>
internal sealed class GrpcProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>The scheme of a call over TLS.</summary>
    public const string SecureScheme = "grpcs";

    /// <summary>The scheme of a call over cleartext HTTP/2.</summary>
    public const string Scheme = "grpc";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = [Scheme, SecureScheme];

    /// <summary>Tells whether a URL is one this handler serves.</summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true" /> for <c>grpc://</c> and <c>grpcs://</c>.</returns>
    public static bool Serves(CurlUrl url) =>
        string.Equals(url.Scheme, Scheme, StringComparison.OrdinalIgnoreCase) || IsSecure(url);

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await CallAsync(context).ConfigureAwait(false);
        }
        catch (GrpcClientException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
        catch (GrpcWireException failure)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, failure.Message);
        }
        catch (Exception failure) when (failure is Http2ProtocolException or Http2StreamResetException or Http2GoAwayException or HpackDecodingException or EndOfStreamException)
        {
            return TransferResult.Failure(CurlExitCode.Http2, failure.Message);
        }
    }

    private static bool IsSecure(CurlUrl url) => string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);

    private async Task<TransferResult> CallAsync(ITransferContext context)
    {
        GrpcMethodPath target = GrpcMethodPath.Parse(context.Url.AbsolutePath);
        (GrpcProtoMethod? method, byte[]? request) = FromProtoFile(context, target);
        bool secure = IsSecure(context.Url);
        ConnectResult connected = await connector.ConnectAsync(TargetOf(context, secure), context.CancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new TransferResult(connected.ExitCode, 0, connected.ErrorMessage) { IsConnectionRefused = connected.IsConnectionRefused };
        }

        await using (connection.ConfigureAwait(false))
        {
            return secure && connected.ApplicationProtocol != "h2"
                ? TransferResult.Failure(CurlExitCode.Http2, $"The server at {context.Url.Host} did not negotiate h2 with ALPN, so it cannot take a gRPC call")
                : await CallOverAsync(context, connection, new GrpcRequestTarget(secure ? "https" : "http", AuthorityOf(context.Url), target.Service, target.Method), method, request).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the method's types from the <c>--grpc-proto</c> file and encodes <c>-d</c> before connecting; both are
    /// <see langword="null" /> without the option, and server reflection gives them once connected.
    /// </summary>
    private static (GrpcProtoMethod? Method, byte[]? Request) FromProtoFile(ITransferContext context, GrpcMethodPath target)
    {
        if (context is not GrpcProtoContext proto)
        {
            return (null, null);
        }

        GrpcProtoMethod method = GrpcProtoMethod.Load(proto.ProtoPath, target);
        return (method, GrpcMessageJson.Encode(method.Input, context.PostData));
    }

    /// <summary>
    /// Makes the call on the open connection and writes its reply; without <c>--grpc-proto</c>, first learns the method's
    /// types by server reflection on the same connection (ADR-1149).
    /// </summary>
    private static async Task<TransferResult> CallOverAsync(ITransferContext context, IConnection connection, GrpcRequestTarget target, GrpcProtoMethod? method, byte[]? request)
    {
        GrpcHttp2Session session = await GrpcHttp2Session.OpenAsync(new ConnectionStream(connection), context.CancellationToken).ConfigureAwait(false);
        method ??= await ServerReflectionClient.DescribeAsync(session, target, context.MaxTime, context.CancellationToken).ConfigureAwait(false);
        request ??= GrpcMessageJson.Encode(method.Input, context.PostData);
        context.Progress.ReportTransferStarted();
        var headers = GrpcRequestHeaders.Build(target, context.MaxTime, null);
        GrpcCallResult result = await GrpcHttp2Call.RunAsync(session, headers, request, context.CancellationToken).ConfigureAwait(false);
        return await WriteAsync(context, method.Output, result).ConfigureAwait(false);
    }

    /// <summary>Writes each response message as JSON, then ends the transfer by the call's status.</summary>
    private static async Task<TransferResult> WriteAsync(ITransferContext context, Protobuf.DynamicMessages.DynamicMessageType type, GrpcCallResult result)
    {
        long written = 0;
        foreach (GrpcMessage message in result.Messages)
        {
            if (message.IsCompressed)
            {
                throw new GrpcClientException(CurlExitCode.BadContentEncoding, "The server sent a compressed gRPC message, which Curl never asks for");
            }

            byte[] json = GrpcMessageJson.Decode(type, message.Payload);
            await context.Output.WriteAsync(json, context.CancellationToken).ConfigureAwait(false);
            written += json.Length;
        }

        GrpcStatus status = result.Status;
        return status.Code == GrpcStatusCode.Ok
            ? TransferResult.Success(written)
            : TransferResult.Failure(CurlExitCode.HttpReturnedError, $"grpc-status {(int)status.Code} ({status.CodeName}): {status.Message}", written);
    }

    private static ConnectTarget TargetOf(ITransferContext context, bool secure) =>
        new(context.Url.IdnHost, context.Url.Port >= 0 ? context.Url.Port : DefaultPortOf(secure), secure)
        {
            ApplicationProtocols = ["h2"],
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };

    private static int DefaultPortOf(bool secure) => secure ? 443 : 80;

    private static string AuthorityOf(CurlUrl url) => url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}";
}
