using System.Net;
using System.Runtime.CompilerServices;

namespace Curl.Extension.Protocol.Xmpp.Client;

/// <summary>SASL PLAIN (RFC 4616): <c>--user</c>'s name and password in the clear, the least preferred mechanism with credentials.</summary>
internal sealed class XmppSaslPlain : IXmppSaslMechanism
{
    /// <inheritdoc />
    public string Name => "PLAIN";

    /// <inheritdoc />
    public int Preference => 0;

    /// <inheritdoc />
    public bool NeedsCredentials => true;

    /// <inheritdoc />
    public IXmppSaslExchange Start(NetworkCredential? credentials, Func<string> createClientNonce) =>
        new XmppSaslSingleStepExchange($"\0{credentials!.UserName}\0{credentials.Password}");

    [ModuleInitializer]
    internal static void Register() => XmppSaslMechanisms.Register(new XmppSaslPlain());
}
