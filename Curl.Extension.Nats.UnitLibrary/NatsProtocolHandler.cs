using Curl.Protocol.Abstractions;

namespace Curl.Extension.Nats;

/// <summary>
/// Serves <c>nats://[user[:password]@]host[:port]/subject[?request][&amp;count=N]</c> and its TLS form
/// <c>natss://</c> (ADR-1065): without <c>-d</c> it subscribes to the subject and writes each message's
/// payload and a newline; with <c>-d</c> it publishes the data; with <c>?request</c> it publishes the
/// data with a reply subject and writes the one reply.
/// </summary>
/// <param name="connector">Opens the connection, with TLS from the first byte for <c>natss</c>.</param>
/// <param name="newInbox">Gives a fresh reply subject for a request.</param>
public sealed class NatsProtocolHandler(IConnector connector, Func<string> newInbox) : IProtocolHandler
{
    /// <summary>The port a URL without one connects to, the NATS server's default.</summary>
    public const int DefaultPort = 4222;

    private const string SecureScheme = "natss";

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
        catch (NatsException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context)
    {
        NatsRequest request = NatsRequest.Of(context.Url, context.PostData);
        CurlUrl url = context.Url;
        bool isSecure = string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        var target = new ConnectTarget(url.IdnHost, url.Port >= 0 ? url.Port : DefaultPort, isSecure)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        TransferResult result = await ExchangeAsync(new NatsSession(connection, context, newInbox), request, isSecure).ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
        return result;
    }

    private static async ValueTask<TransferResult> ExchangeAsync(NatsSession session, NatsRequest request, bool isSecure)
    {
        try
        {
            return TransferResult.Success(await session.RunAsync(request, isSecure).ConfigureAwait(false));
        }
        catch (NatsException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }
}
