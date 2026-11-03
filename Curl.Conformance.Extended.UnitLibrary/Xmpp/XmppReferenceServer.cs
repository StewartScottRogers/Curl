using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5.Transports;

namespace Curl.Conformance.Extended.Xmpp;

/// <summary>
/// An XMPP server written from RFC 6120 for one client connection: stream features with STARTTLS as the
/// <see cref="XmppReferenceScript" /> offers it (5), SASL PLAIN, ANONYMOUS, SCRAM-SHA-1 and SCRAM-SHA-256 (6), a stream
/// restart after each, resource binding with a server-assigned resource (7), then the script's answer to curl's stanza.
/// It records every first-level element curl sent, so a case reads what curl sent behind TLS too.
/// </summary>
/// <param name="script">What the server offers and answers.</param>
public sealed class XmppReferenceServer(XmppReferenceScript script) : IReferenceServer
{
    /// <summary>The server's domain, which every case's URL names.</summary>
    public const string Domain = "localhost";

    private const string TlsNamespace = "urn:ietf:params:xml:ns:xmpp-tls";

    private const string SaslNamespace = "urn:ietf:params:xml:ns:xmpp-sasl";

    private readonly List<XElement> received = [];

    /// <summary>Gets every first-level element curl sent, in order, each without the stream's default namespace written out.</summary>
    public IReadOnlyList<string> Received => [.. received.Select(element => element.ToString(SaveOptions.DisableFormatting).Replace(" xmlns=\"jabber:client\"", string.Empty, StringComparison.Ordinal))];

    /// <summary>Gets the SASL initial response curl sent with <c>&lt;auth/&gt;</c>, decoded.</summary>
    public string InitialResponse { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether curl's SCRAM client proof matched the password.</summary>
    public bool ProofVerified { get; private set; }

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Stream wire = new DelegatingDuplexStream(connection.ReadAsync, connection.WriteAsync);
        XmppReferenceStream stream = new(wire, cancellationToken);
        await stream.OpenAsync("s1", StartTlsFeature() + MechanismsFeature()).ConfigureAwait(false);
        if (script.StartTls != XmppStartTls.None)
        {
            await NextAsync(stream).ConfigureAwait(false);
            await stream.SendAsync($"<proceed xmlns='{TlsNamespace}'/>").ConfigureAwait(false);
            SslStream tls = new(wire);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = script.Certificate }, cancellationToken).ConfigureAwait(false);
            stream = new XmppReferenceStream(tls, cancellationToken);
            await stream.OpenAsync("s2", MechanismsFeature()).ConfigureAwait(false);
        }

        if (await AuthenticateAsync(stream).ConfigureAwait(false))
        {
            await ServeBoundStreamAsync(stream).ConfigureAwait(false);
        }
    }

    private string StartTlsFeature() => script.StartTls switch
    {
        XmppStartTls.Required => $"<starttls xmlns='{TlsNamespace}'><required/></starttls>",
        XmppStartTls.Optional => $"<starttls xmlns='{TlsNamespace}'/>",
        _ => string.Empty,
    };

    private string MechanismsFeature() =>
        $"<mechanisms xmlns='{SaslNamespace}'>{string.Concat(script.Mechanisms.Select(name => $"<mechanism>{name}</mechanism>"))}</mechanisms>";

    private async Task<XElement> NextAsync(XmppReferenceStream stream)
    {
        XElement element = (await stream.ReadAsync().ConfigureAwait(false))!;
        received.Add(element);
        return element;
    }

    // Answers <auth/>: <failure/> with the script's condition, or <success/> after the mechanism's challenges.
    private async Task<bool> AuthenticateAsync(XmppReferenceStream stream)
    {
        XElement auth = await NextAsync(stream).ConfigureAwait(false);
        InitialResponse = Decode(auth.Value);
        if (script.FailureCondition is { } condition)
        {
            await stream.SendAsync($"<failure xmlns='{SaslNamespace}'><{condition}/></failure>").ConfigureAwait(false);
            return false;
        }

        string mechanism = (string)auth.Attribute("mechanism")!;
        string additionalData = mechanism.StartsWith("SCRAM-", StringComparison.Ordinal) ? await ScramAsync(stream, mechanism).ConfigureAwait(false) : "=";
        await stream.SendAsync($"<success xmlns='{SaslNamespace}'>{additionalData}</success>").ConfigureAwait(false);

        // A client that finds the server signature forged ends the connection rather than restart the stream.
        return !script.ForgeServerSignature;
    }

    // One challenge carrying the server-first message; the server signature goes with <success/> (RFC 6120, 6.3.10).
    private async Task<string> ScramAsync(XmppReferenceStream stream, string mechanism)
    {
        XmppReferenceScram scram = new(mechanism == "SCRAM-SHA-1" ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256, script.Password, InitialResponse);
        await stream.SendAsync($"<challenge xmlns='{SaslNamespace}'>{Encode(scram.ServerFirst)}</challenge>").ConfigureAwait(false);
        XElement response = await NextAsync(stream).ConfigureAwait(false);
        byte[] signature = scram.ServerSignature(Decode(response.Value), out bool proofVerified);
        ProofVerified = proofVerified;
        signature[0] ^= script.ForgeServerSignature ? (byte)1 : (byte)0;
        return Encode("v=" + Convert.ToBase64String(signature));
    }

    private async Task ServeBoundStreamAsync(XmppReferenceStream authenticated)
    {
        XmppReferenceStream stream = authenticated.Restart();
        await stream.OpenAsync("s3", "<bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/>").ConfigureAwait(false);
        XElement bind = await NextAsync(stream).ConfigureAwait(false);
        await stream.SendAsync(
            $"<iq type='result' id='{(string?)bind.Attribute("id")}'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><jid>curl@{Domain}/{script.Resource}</jid></bind></iq>")
            .ConfigureAwait(false);
        XElement request = await NextAsync(stream).ConfigureAwait(false);
        foreach (string chunk in script.Answer(request))
        {
            await stream.SendAsync(chunk).ConfigureAwait(false);
        }

        // curl closes its stream after the answer; close ours in turn (RFC 6120, 4.4). It may instead end the connection.
        await stream.ReadAsync().ConfigureAwait(false);
        if (stream.ClosedByClient)
        {
            await stream.SendAsync("</stream:stream>").ConfigureAwait(false);
        }
    }

    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static string Decode(string text) => text == "=" ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(text));
}
