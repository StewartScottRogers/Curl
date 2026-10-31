using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Stomp.Client;

/// <summary>The <c>stomp://</c> and <c>stomps://</c> extension's description and handler factory (ADR-1059).</summary>
public static class StompExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "stomp",
        "Sends and subscribes with STOMP 1.2: with -d it SENDs the data to the URL path's destination, otherwise it SUBSCRIBEs to it and writes each MESSAGE body until the server closes; --user gives login and passcode, stomps:// adds TLS.",
        ["stomp", "stomps"],
        [],
        new Uri("https://stomp.github.io/stomp-specification-1.2.html"));

    /// <summary>Creates the handler over curl's connector.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new StompProtocolHandler(services.Connector);
    }
}
