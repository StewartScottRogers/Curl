using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>
/// Serves <c>nats://[user[:password]@]host[:port]/subject[?request][&amp;count=N]</c> and its TLS form
/// <c>natss://</c> (ADR-1065): without <c>-d</c> it subscribes to the subject and writes each message's
/// payload and a newline; with <c>-d</c> or <c>-T</c> it publishes the data or the uploaded file; with <c>?request</c> it publishes the
/// data with a reply subject and writes the one reply.
/// </summary>
/// <param name="connector">Opens the connection, in plain text for <c>natss</c> when a TLS provider is given, else with TLS from the first byte.</param>
/// <param name="newInbox">Gives a fresh reply subject for a request.</param>
/// <param name="tlsProvider">Starts TLS over the plain connection for <c>natss</c>; without one the connector opens TLS from the first byte.</param>
/// <param name="timeProvider">Times how long a server may stay silent before it is taken for a <c>handshake_first</c> one; the system clock when absent.</param>
public sealed class NatsProtocolHandler(IConnector connector, Func<string> newInbox, ITlsProvider? tlsProvider = null, TimeProvider? timeProvider = null) : IProtocolHandler
{
    /// <summary>How long a <c>natss</c> server may stay silent after the connection opens before the client starts TLS, taking it for a <c>handshake_first</c> server.</summary>
    public static readonly TimeSpan SilenceMeansHandshakeFirst = TimeSpan.FromSeconds(2);

    /// <summary>The port a URL without one connects to, the NATS server's default.</summary>
    public const int DefaultPort = 4222;

    private const string SecureScheme = "natss";

    private const string ConnectionReset = "Recv failure: Connection was reset";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["nats", SecureScheme];

    /// <summary>Gives a fresh reply subject: <c>_INBOX.</c> and 32 random hexadecimal digits.</summary>
    /// <returns>The subject.</returns>
    public static string NewInbox() => "_INBOX." + Guid.NewGuid().ToString("N");

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await TransferAsync(context).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return TransferResult.Failure(CurlExitCode.RecvError, ConnectionReset);
        }
        catch (NatsException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context)
    {
        NatsRequest request = NatsRequest.Of(context.Url, await RequestBodyAsync(context).ConfigureAwait(false));
        if (request.Kind != NatsRequestKind.Subscribe)
        {
            NatsSubject.RefuseWildcardToPublish(request.Subject);
        }

        bool isSecure = string.Equals(context.Url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        ConnectTarget target = TargetFor(context, isSecure);
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        var session = new NatsSession(connection, context, newInbox);
        NatsTlsUpgrade? upgrade = isSecure ? UpgradeFor(context) : null;
        TransferResult result = await ExchangeAsync(session, request, isSecure, upgrade).ConfigureAwait(false);
        await session.Connection.DisposeAsync().ConfigureAwait(false);
        return result;
    }

    private ConnectTarget TargetFor(ITransferContext context, bool isSecure)
    {
        CurlUrl url = context.Url;
        return new ConnectTarget(url.IdnHost, url.Port >= 0 ? url.Port : DefaultPort, isSecure && tlsProvider is null)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
    }

    private NatsTlsUpgrade? UpgradeFor(ITransferContext context) => tlsProvider is null
        ? null
        : new NatsTlsUpgrade(tlsProvider, context.Url.IdnHost, context.Events, SilenceMeansHandshakeFirst, timeProvider ?? TimeProvider.System);

    /// <summary>
    /// Gives the payload to publish: <c>-d</c>'s data when there is any, otherwise the whole
    /// <c>-T</c> upload, otherwise none (a subscription).
    /// </summary>
    private static async ValueTask<ReadOnlyMemory<byte>?> RequestBodyAsync(ITransferContext context)
    {
        if (context.PostData is not null || context.Upload is not { } upload)
        {
            return context.PostData;
        }

        using var body = new MemoryStream();
        try
        {
            await upload.CopyToAsync(body, context.CancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure)
        {
            throw new NatsException(CurlExitCode.ReadError, "Failed to read the upload: " + failure.Message);
        }

        return body.ToArray();
    }

    private static async ValueTask<TransferResult> ExchangeAsync(NatsSession session, NatsRequest request, bool isSecure, NatsTlsUpgrade? upgrade)
    {
        try
        {
            return TransferResult.Success(await session.RunAsync(request, isSecure, upgrade).ConfigureAwait(false));
        }
        catch (NatsException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }
}
