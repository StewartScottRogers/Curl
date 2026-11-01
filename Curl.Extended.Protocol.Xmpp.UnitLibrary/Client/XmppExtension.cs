using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Xmpp.Client;

/// <summary>The <c>xmpp://</c> and <c>xmpps://</c> extension's description and handler factory (ADR-1068).</summary>
public static class XmppExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "xmpp",
        "Sends XMPP messages and service discovery queries: xmpp://domain/jid logs in to domain with --user (SASL SCRAM-SHA-256, SCRAM-SHA-1 or PLAIN, ANONYMOUS without --user, STARTTLS when offered), then with -d or ?message;body=.. sends a message to jid, otherwise (or with ?disco;request=info|items;node=..) writes the disco answer; xmpps:// uses direct TLS.",
        ["xmpp", "xmpps"],
        [],
        new Uri("https://www.rfc-editor.org/rfc/rfc6120"));

    /// <summary>Creates the handler over curl's connector and TLS provider.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new XmppProtocolHandler(services.Connector, services.TlsProvider);
    }
}
