using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Curl.Extension.Xmpp.Client;

/// <summary>SASL SCRAM-SHA-256 (RFC 7677), preferred over SCRAM-SHA-1 and PLAIN.</summary>
internal sealed class XmppScramSha256 : IXmppSaslMechanism
{
    /// <inheritdoc />
    public string Name => "SCRAM-SHA-256";

    /// <inheritdoc />
    public int Preference => 2;

    /// <inheritdoc />
    public bool NeedsCredentials => true;

    /// <inheritdoc />
    public IXmppSaslExchange Start(NetworkCredential? credentials, Func<string> createClientNonce) =>
        new XmppScramExchange(HashAlgorithmName.SHA256, credentials!.UserName, credentials.Password, createClientNonce());

    [ModuleInitializer]
    internal static void Register() => XmppSaslMechanisms.Register(new XmppScramSha256());
}
