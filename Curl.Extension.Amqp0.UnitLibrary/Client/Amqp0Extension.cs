using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Amqp0.Client;

/// <summary>The <c>amqp://</c> and <c>amqps://</c> extension's description and handler factory (ADR-1091).</summary>
public static class Amqp0Extension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "amqp0",
        "Publishes or gets one message over AMQP 0-9-1 (RabbitMQ): the URL path is /<virtual host>/<queue>, the queue is declared, -d publishes the data to it through the default exchange, without -d basic.get writes one message's body; --user or the URL's user information log in with PLAIN, amqps:// adds TLS.",
        ["amqp", "amqps"],
        [],
        new Uri("https://www.rabbitmq.com/resources/specs/amqp0-9-1.pdf"));

    /// <summary>Creates the handler over curl's connector.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new Amqp0ProtocolHandler(services.Connector);
    }
}
