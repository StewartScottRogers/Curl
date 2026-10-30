using Curl.Extension.Mqtt5.Client;
using Curl.Extension.Mqtt5.Packets;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.Mqtt5.WebSocket;

/// <summary>
/// Serves <c>mqtt5+ws://</c> (port 80): the WebSocket opening handshake to <c>/mqtt</c> with the <c>mqtt</c>
/// subprotocol (<see cref="Mqtt5WebSocketUpgrade"/>), then <see cref="Mqtt5ClientFlow"/>'s publish or subscribe to the
/// URL path's topic in binary frames (<see cref="Mqtt5WebSocketConnection"/>), as OASIS MQTT Version 5.0 section 6
/// describes and ADR-1054 decides.
/// </summary>
public sealed class Mqtt5WebSocketProtocolHandler : IProtocolHandler
{
    private const int DefaultPort = 80;

    private readonly IConnector connector;

    private readonly IWebSocketRandomSource randomSource;

    private readonly Func<string> clientIdentifierSuffixSource;

    /// <summary>Creates the handler over <paramref name="connector"/>, with random keys, masks and client identifier.</summary>
    public Mqtt5WebSocketProtocolHandler(IConnector connector)
        : this(connector, new SystemWebSocketRandomSource(), Mqtt5ProtocolHandler.NewClientIdentifierSuffix)
    {
    }

    /// <summary>
    /// Creates the handler over <paramref name="connector"/>, taking the key and masks from <paramref name="randomSource"/>
    /// and the client identifier's suffix from <paramref name="clientIdentifierSuffixSource"/>.
    /// </summary>
    public Mqtt5WebSocketProtocolHandler(IConnector connector, IWebSocketRandomSource randomSource, Func<string> clientIdentifierSuffixSource)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        this.clientIdentifierSuffixSource = clientIdentifierSuffixSource ?? throw new ArgumentNullException(nameof(clientIdentifierSuffixSource));
    }

    /// <summary>The scheme served: <c>mqtt5+ws</c>.</summary>
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["mqtt5+ws"];

    /// <summary>Connects to the URL's host, upgrades to WebSocket and runs the publish or subscribe flow.</summary>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await TransferAsync(context, Mqtt5UrlTopic.Decode(context.Url)).ConfigureAwait(false);
        }
        catch (Mqtt5ClientException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    internal static ConnectTarget TargetOf(ITransferContext context) =>
        new(context.Url.IdnHost, context.Url.Port >= 0 ? context.Url.Port : DefaultPort, false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, string topic)
    {
        ConnectResult connect = await connector.ConnectAsync(TargetOf(context), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        await using (connection.ConfigureAwait(false))
        {
            Mqtt5WebSocketConnection upgraded = await UpgradeAsync(context, connection).ConfigureAwait(false);
            context.Progress.ReportTransferStarted();
            var flow = new Mqtt5ClientFlow(upgraded, context.Output, context.Progress, context.TimeProvider, context.CancellationToken);
            try
            {
                Mqtt5Connect connectPacket = Mqtt5ProtocolHandler.ConnectFor(Mqtt5ProtocolHandler.ClientIdentifierPrefix + clientIdentifierSuffixSource(), context.Credentials);
                await flow.RunAsync(connectPacket, topic, context.PostData).ConfigureAwait(false);
                return TransferResult.Success(flow.BytesWritten);
            }
            catch (Mqtt5ClientException failure)
            {
                return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
            }
        }
    }

    private async ValueTask<Mqtt5WebSocketConnection> UpgradeAsync(ITransferContext context, IConnection connection)
    {
        string key = Mqtt5WebSocketUpgrade.NewKey(randomSource);
        await Mqtt5WebSocketUpgrade.SendRequestAsync(connection, Mqtt5WebSocketUpgrade.Request(context.Url, key), context.CancellationToken).ConfigureAwait(false);
        ReadOnlyMemory<byte> received = await Mqtt5WebSocketUpgrade.ReadReplyAsync(connection, key, context.CancellationToken).ConfigureAwait(false);
        return new Mqtt5WebSocketConnection(connection, randomSource, received);
    }
}
