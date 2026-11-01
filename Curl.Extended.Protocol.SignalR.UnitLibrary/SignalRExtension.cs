using Curl.Extension.Abstractions.Description;
using Curl.Extended.Protocol.SignalR.Client;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extended.Protocol.SignalR;

/// <summary>The <c>signalr://</c> and <c>signalrs://</c> extension's description and handler factory (ADR-1084).</summary>
public static class SignalRExtension
{
    /// <summary>Gets what <c>curl --extensions-help</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "signalr",
        "Connects to ASP.NET Core SignalR hubs: negotiates, opens the WebSocket transport with the JSON hub protocol, sends the -d message {\"type\":1|4,\"target\":\"Method\",\"arguments\":[...]} as an invocation (1) or stream invocation (4) and writes its result or each stream item as one JSON line; without -d writes each call the hub makes as a JSON array [\"target\", args...] per line until it closes.",
        ["signalr", "signalrs"],
        [],
        new Uri("https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/HubProtocol.md"));

    /// <summary>Creates the handler over curl's connector, with system randomness for WebSocket keys and masks.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new SignalRProtocolHandler(services.Connector, new SystemWebSocketRandomSource());
    }
}
