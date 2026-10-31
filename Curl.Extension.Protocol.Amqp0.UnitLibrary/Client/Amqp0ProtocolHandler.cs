using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Amqp0.Client;

/// <summary>
/// Serves <c>amqp://</c> (port 5672) and <c>amqps://</c> (port 5671, TLS from the connector) as an AMQP 0-9-1 client
/// (ADR-1060): it declares the URL's queue in the URL's virtual host, and with a body (<c>-d</c>) publishes it there,
/// otherwise takes one message with basic.get and writes its body to the output. <c>--user</c> is the PLAIN login,
/// guest/guest without it. The exchange itself is <see cref="Amqp0ClientFlow"/>.
/// </summary>
/// <param name="connector">Makes the TCP or TLS connection.</param>
public sealed class Amqp0ProtocolHandler(IConnector connector) : IProtocolHandler
{
    private const string SecureScheme = "amqps";

    private const int DefaultPort = 5672;

    private const int SecureDefaultPort = 5671;

    /// <summary>The schemes served: <c>amqp</c> and <c>amqps</c>.</summary>
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["amqp", SecureScheme];

    /// <summary>Connects to the URL's host and runs the publish or get flow.</summary>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await TransferAsync(context, Amqp0UrlQueue.Decode(context.Url)).ConfigureAwait(false);
        }
        catch (Amqp0ClientException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    internal static ConnectTarget TargetOf(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool useTls = string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        int port = url.Port >= 0 ? url.Port : useTls ? SecureDefaultPort : DefaultPort;
        return new ConnectTarget(url.IdnHost, port, useTls)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, Amqp0UrlQueue queue)
    {
        ConnectResult connect = await connector.ConnectAsync(TargetOf(context), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            var flow = new Amqp0ClientFlow(connection, context.Output, context.Progress, context.CancellationToken);
            try
            {
                await flow.RunAsync(queue, context.Credentials, context.PostData).ConfigureAwait(false);
                return TransferResult.Success(flow.BytesWritten);
            }
            catch (Amqp0ClientException failure)
            {
                return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
            }
        }
    }
}
