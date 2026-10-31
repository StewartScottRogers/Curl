using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Nats;

/// <summary>The <c>nats://</c> and <c>natss://</c> extension's description and handler factory (ADR-1065).</summary>
public static class NatsExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "nats",
        "Talks to NATS servers (port 4222; natss:// is TLS from the first byte): without -d subscribes to the URL path's subject and writes each message's payload and a newline until the server closes or ?count=N messages arrived; with -d publishes the data; with ?request publishes it with a reply subject and writes the one reply. -u gives a user and password, or a token alone.",
        ["nats", "natss"],
        [],
        new Uri("https://docs.nats.io/reference/reference-protocols/nats-protocol"));

    /// <summary>Creates the handler over curl's connector, with random reply subjects.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NatsProtocolHandler(services.Connector, NatsProtocolHandler.NewInbox);
    }
}
