using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Curl.Extension.Protocol.Xmpp.Client;

/// <summary>SASL SCRAM-SHA-1 (RFC 5802), preferred over PLAIN.</summary>
internal sealed class XmppScramSha1 : IXmppSaslMechanism
{
    /// <inheritdoc />
    public string Name => "SCRAM-SHA-1";

    /// <inheritdoc />
    public int Preference => 1;

    /// <inheritdoc />
    public bool NeedsCredentials => true;

    /// <inheritdoc />
    public IXmppSaslExchange Start(NetworkCredential? credentials, Func<string> createClientNonce) =>
        new XmppScramExchange(HashAlgorithmName.SHA1, credentials!.UserName, credentials.Password, createClientNonce());

    [ModuleInitializer]
    internal static void Register() => XmppSaslMechanisms.Register(new XmppScramSha1());
}
