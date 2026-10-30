using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Amqp1.Client;

/// <summary>The <c>amqp1://</c> and <c>amqp1s://</c> extension's description and handler factory (ADR-1082).</summary>
public static class Amqp1Extension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "amqp1",
        "Sends or receives one message over AMQP 1.0: the URL path is the URL-encoded address; with -d or -T it sends the body as one message to that address, otherwise it receives one message from it and writes its body; --user authenticates with SASL PLAIN, otherwise ANONYMOUS; amqp1s:// adds TLS.",
        ["amqp1", "amqp1s"],
        [],
        new Uri("https://docs.oasis-open.org/amqp/core/v1.0/os/amqp-core-overview-v1.0-os.html"));

    /// <summary>Creates the handler over curl's connector.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new Amqp1ProtocolHandler(services.Connector);
    }
}
