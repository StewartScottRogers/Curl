using Curl.Extended.Protocol.Grpc.Reflection;
using Curl.Extended.Protocol.Grpc.Wire;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

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

    private const string RecvFailure = "Recv failure: Connection was reset";

    private const string SendFailure = "Send failure: Connection was reset";

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
        catch (Http2StreamResetException reset)
        {
            return StatusFailure(StatusOfReset(reset.ErrorCode), reset.Message);
        }
        catch (Http2GoAwayException goAway)
        {
            return StatusFailure(GrpcStatusCode.Unavailable, goAway.Message);
        }
        catch (Exception failure) when (failure is Http2ProtocolException or HpackDecodingException or EndOfStreamException)
        {
            return TransferResult.Failure(CurlExitCode.Http2, failure.Message);
        }
    }

    /// <summary>
    /// Gives the gRPC status PROTOCOL-HTTP2.md's Errors table maps an RST_STREAM error code to (ADR-1202): REFUSED_STREAM is
    /// UNAVAILABLE, CANCEL is CANCELLED, ENHANCE_YOUR_CALM is RESOURCE_EXHAUSTED, INADEQUATE_SECURITY is PERMISSION_DENIED,
    /// and every other code is INTERNAL.
    /// </summary>
    /// <param name="errorCode">The error code the server reset the call's stream with.</param>
    /// <returns>The status the call fails with.</returns>
    internal static GrpcStatusCode StatusOfReset(Http2ErrorCode errorCode) =>
        ResetStatuses.TryGetValue(errorCode, out GrpcStatusCode code) ? code : GrpcStatusCode.Internal;

    private static readonly Dictionary<Http2ErrorCode, GrpcStatusCode> ResetStatuses = new()
    {
        [Http2ErrorCode.RefusedStream] = GrpcStatusCode.Unavailable,
        [Http2ErrorCode.Cancel] = GrpcStatusCode.Cancelled,
        [Http2ErrorCode.EnhanceYourCalm] = GrpcStatusCode.ResourceExhausted,
        [Http2ErrorCode.InadequateSecurity] = GrpcStatusCode.PermissionDenied,
    };

    private static TransferResult StatusFailure(GrpcStatusCode code, string message) =>
        TransferResult.Failure(CurlExitCode.HttpReturnedError, $"grpc-status {(int)code} ({GrpcStatus.NameOf(code)}): {message}");

    private static bool IsSecure(CurlUrl url) => string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gives the request message's JSON: <c>-d</c>'s body, else <c>-T</c>'s upload read whole, else none.
    /// </summary>
    private static async ValueTask<ReadOnlyMemory<byte>?> RequestBodyAsync(ITransferContext context)
    {
        if (context.PostData is not null || context.Upload is not { } upload)
        {
            return context.PostData;
        }

        using var body = new MemoryStream();
        await upload.CopyToAsync(body, context.CancellationToken).ConfigureAwait(false);
        return body.ToArray();
    }

    private async Task<TransferResult> CallAsync(ITransferContext context)
    {
        GrpcMethodPath target = GrpcMethodPath.Parse(context.Url.AbsolutePath);
        ReadOnlyMemory<byte>? body = await RequestBodyAsync(context).ConfigureAwait(false);
        (GrpcProtoMethod? method, byte[]? request) = FromProtoFile(context, target, body);
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
                : await CallOverAsync(context, connection, new GrpcRequestTarget(secure ? "https" : "http", AuthorityOf(context.Url), target.Service, target.Method), method, request, body).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the method's types from the <c>--grpc-proto</c> file and encodes <c>-d</c> before connecting; both are
    /// <see langword="null" /> without the option, and server reflection gives them once connected.
    /// </summary>
    private static (GrpcProtoMethod? Method, byte[]? Request) FromProtoFile(ITransferContext context, GrpcMethodPath target, ReadOnlyMemory<byte>? body)
    {
        if (context is not GrpcProtoContext proto)
        {
            return (null, null);
        }

        GrpcProtoMethod method = GrpcProtoMethod.Load(proto.ProtoPath, target);
        return (method, GrpcMessageJson.Encode(method.Input, body));
    }

    /// <summary>
    /// Makes the call on the open connection and writes its reply; without <c>--grpc-proto</c>, first learns the method's
    /// types by server reflection on the same connection (ADR-1149).
    /// </summary>
    private static async Task<TransferResult> CallOverAsync(ITransferContext context, IConnection connection, GrpcRequestTarget target, GrpcProtoMethod? method, byte[]? request, ReadOnlyMemory<byte>? body)
    {
        var stream = new ConnectionStream(connection);
        try
        {
            return await CallOverStreamAsync(context, stream, target, method, request, body).ConfigureAwait(false);
        }
        catch (IOException failure) when (failure is not (EndOfStreamException or OutputWriteFailedException))
        {
            return TransferResult.Failure(
                stream.WriteFailed ? CurlExitCode.SendError : CurlExitCode.RecvError,
                stream.WriteFailed ? SendFailure : RecvFailure);
        }
    }

    private static async Task<TransferResult> CallOverStreamAsync(ITransferContext context, ConnectionStream stream, GrpcRequestTarget target, GrpcProtoMethod? method, byte[]? request, ReadOnlyMemory<byte>? body)
    {
        GrpcHttp2Session session = await GrpcHttp2Session.OpenAsync(stream, context.CancellationToken).ConfigureAwait(false);
        context.Progress.ReportTransferStarted();
        GrpcRequestHeaderValues headerValues = HeaderValuesOf(context);
        method ??= await ServerReflectionClient.DescribeAsync(session, target, headerValues, context.CancellationToken).ConfigureAwait(false);
        request ??= GrpcMessageJson.Encode(method.Input, body);
        var headers = GrpcRequestHeaders.Build(target, headerValues);
        GrpcCallResult result = await GrpcHttp2Call.RunAsync(session, headers, request, context.CancellationToken).ConfigureAwait(false);
        return await WriteAsync(context, method.Output, result).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives what the Request-Headers carry from the command line: <c>-m</c> as <c>grpc-timeout</c>, <c>-A</c> (or curl's
    /// own) as <c>user-agent</c> and each <c>-H</c> as Custom-Metadata (ADR-1227).
    /// </summary>
    private static GrpcRequestHeaderValues HeaderValuesOf(ITransferContext context) =>
        new(context.MaxTime, GrpcRequestHeaderValues.UserAgentFor(context.Http?.UserAgent), context.Http?.Headers ?? []);

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

        TransferReport report = result.Report with { DownloadSize = written };
        GrpcStatus status = result.Status;
        return status.Code == GrpcStatusCode.Ok
            ? TransferResult.Success(written) with { Report = report }
            : TransferResult.Failure(CurlExitCode.HttpReturnedError, $"grpc-status {(int)status.Code} ({status.CodeName}): {status.Message}", written) with { Report = report };
    }

    // A grpcs:// target carries PoolScheme https, the one scheme whose TLS handshake offers the
    // target's ApplicationProtocols through ALPN, so the ClientHello offers h2 (ADR-1197, BL-4562).
    private static ConnectTarget TargetOf(ITransferContext context, bool secure) =>
        new(context.Url.IdnHost, context.Url.Port >= 0 ? context.Url.Port : DefaultPortOf(secure), secure)
        {
            ApplicationProtocols = ["h2"],
            PoolScheme = secure ? "https" : null,
            IsNeverPooled = secure,
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };

    private static int DefaultPortOf(bool secure) => secure ? 443 : 80;

    private static string AuthorityOf(CurlUrl url) => url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}";
}
