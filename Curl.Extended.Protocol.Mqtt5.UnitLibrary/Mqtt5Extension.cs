using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5;

/// <summary>The <c>mqtt5://</c>, <c>mqtt5s://</c> and <c>mqtt5+ws://</c> extension's description and handler factory (ADR-1086).</summary>
public static class Mqtt5Extension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "mqtt5",
        "Publishes and subscribes with MQTT 5 (curl's mqtt:// stays 3.1.1): with -d it PUBLISHes the data to the URL path's topic, otherwise it SUBSCRIBEs to it (a $share/name/filter shared subscription too) and writes each message's payload until the broker sends DISCONNECT; a failure reason code is named in the error, --user gives User Name and Password, mqtt5s:// adds TLS on port 8883 and mqtt5+ws:// runs over WebSocket to /mqtt on port 80.",
        ["mqtt5", "mqtt5s", "mqtt5+ws"],
        [],
        new Uri("https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html"));

    /// <summary>Creates the handler over curl's connector.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler serving all three schemes.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new Mqtt5SchemeHandler(services.Connector);
    }
}
