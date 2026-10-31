using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp1.Client;

/// <summary>
/// Serves <c>amqp1://host[:5672]/address</c> and <c>amqp1s://host[:5671]/address</c> (TLS from the connector) with
/// AMQP 1.0 (OASIS AMQP Version 1.0): with a request body (<c>-d</c>, <c>-T</c>) it sends one message to the URL's
/// URL-decoded address, otherwise it receives one message from it and writes its body to the output. <c>--user</c>
/// authenticates with SASL PLAIN, otherwise ANONYMOUS. The exchange itself is <see cref="Amqp1ClientFlow"/>.
/// </summary>
public sealed class Amqp1ProtocolHandler : IProtocolHandler
{
    private const string SecureScheme = "amqp1s";

    private const int DefaultPort = 5672;

    private const int SecureDefaultPort = 5671;

    private readonly IConnector connector;

    /// <summary>
    /// Creates the handler over <paramref name="connector"/>. Unchecked, like every extension handler, because the
    /// catalogue builds each handler before any transfer needs its connector.
    /// </summary>
    public Amqp1ProtocolHandler(IConnector connector)
    {
        this.connector = connector;
    }

    /// <summary>The schemes served: <c>amqp1</c> and <c>amqp1s</c>.</summary>
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["amqp1", SecureScheme];

    /// <summary>Connects to the URL's host and runs the send or receive flow.</summary>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string path = context.Url.AbsolutePath;
        if (path.Length <= 1)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, Amqp1ClientMessages.NoAddress);
        }

        return await TransferAsync(context, Uri.UnescapeDataString(path[1..])).ConfigureAwait(false);
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

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, string address)
    {
        ConnectResult connect = await connector.ConnectAsync(TargetOf(context), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            var flow = new Amqp1ClientFlow(connection, context.Output, context.Progress, context.CancellationToken);
            try
            {
                await flow.RunAsync(context.Credentials, context.Url.IdnHost, $"curl-{Guid.NewGuid():N}", address, await RequestBodyAsync(context).ConfigureAwait(false)).ConfigureAwait(false);
                return TransferResult.Success(flow.BytesWritten);
            }
            catch (Amqp1ClientException failure)
            {
                return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
            }
        }
    }
}
