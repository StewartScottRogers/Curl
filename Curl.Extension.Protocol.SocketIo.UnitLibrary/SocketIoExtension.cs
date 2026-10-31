using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.Protocol.SocketIo;

/// <summary>The <c>socketio://</c> and <c>socketios://</c> extension's description and handler factory (ADR-1035).</summary>
public static class SocketIoExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "socketio",
        "Connects to Socket.IO v5 servers over WebSocket: joins the URL path's namespace, emits the -d JSON array [\"event\", args...] as an event and writes each event received as one JSON array per line until the server disconnects.",
        ["socketio", "socketios"],
        [],
        new Uri("https://socket.io/docs/v4/socket-io-protocol/"));

    /// <summary>Creates the handler over curl's connector, with system randomness for keys and masks.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new SocketIoProtocolHandler(services.Connector, new SystemWebSocketRandomSource());
    }
}
