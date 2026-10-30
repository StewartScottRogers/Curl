using Curl.Extension.Mqtt5.Client;
using Curl.Extension.Mqtt5.WebSocket;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Mqtt5;

/// <summary>
/// Serves all three MQTT 5 schemes as one handler, so the extension registers with one line (ADR-0999): each transfer
/// goes to a <see cref="Mqtt5WebSocketProtocolHandler"/> when its URL's scheme is <c>mqtt5+ws</c> and to a
/// <see cref="Mqtt5ProtocolHandler"/> for <c>mqtt5</c> and <c>mqtt5s</c>, made over <paramref name="connector"/> for
/// that transfer (ADR-1086).
/// </summary>
/// <param name="connector">Opens the connection to the broker.</param>
public sealed class Mqtt5SchemeHandler(IConnector connector) : IProtocolHandler
{
    private const string WebSocketScheme = "mqtt5+ws";

    /// <summary>The schemes served: <c>mqtt5</c>, <c>mqtt5s</c> and <c>mqtt5+ws</c>.</summary>
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["mqtt5", "mqtt5s", WebSocketScheme];

    /// <summary>Hands the transfer to the handler for its URL's scheme.</summary>
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return string.Equals(context.Url.Scheme, WebSocketScheme, StringComparison.OrdinalIgnoreCase)
            ? new Mqtt5WebSocketProtocolHandler(connector).ExecuteAsync(context)
            : new Mqtt5ProtocolHandler(connector).ExecuteAsync(context);
    }
}
