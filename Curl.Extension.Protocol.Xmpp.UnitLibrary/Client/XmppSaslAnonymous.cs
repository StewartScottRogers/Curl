using System.Net;
using System.Runtime.CompilerServices;

namespace Curl.Extension.Protocol.Xmpp.Client;

/// <summary>SASL ANONYMOUS (RFC 4505) with an empty trace: the mechanism without <c>--user</c>.</summary>
internal sealed class XmppSaslAnonymous : IXmppSaslMechanism
{
    /// <inheritdoc />
    public string Name => "ANONYMOUS";

    /// <inheritdoc />
    public int Preference => 0;

    /// <inheritdoc />
    public bool NeedsCredentials => false;

    /// <inheritdoc />
    public IXmppSaslExchange Start(NetworkCredential? credentials, Func<string> createClientNonce) => new XmppSaslSingleStepExchange(string.Empty);

    [ModuleInitializer]
    internal static void Register() => XmppSaslMechanisms.Register(new XmppSaslAnonymous());
}
