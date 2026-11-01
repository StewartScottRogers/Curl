using System.Net;

namespace Curl.Extended.Protocol.Xmpp.Client;

/// <summary>A SASL mechanism the client can log in with (RFC 6120, 6), added to <see cref="XmppSaslMechanisms"/> by its own file.</summary>
internal interface IXmppSaslMechanism
{
    /// <summary>Gets the mechanism's name as the server's <c>&lt;mechanism/&gt;</c> offers it.</summary>
    string Name { get; }

    /// <summary>Gets the mechanism's rank among those that apply: the offered mechanism with the highest is chosen.</summary>
    int Preference { get; }

    /// <summary>Gets a value indicating whether the mechanism logs in with <c>--user</c>'s credentials, rather than without any.</summary>
    bool NeedsCredentials { get; }

    /// <summary>Starts an exchange that logs in with <paramref name="credentials"/>.</summary>
    /// <param name="credentials">The credentials, <see langword="null"/> when the mechanism needs none.</param>
    /// <param name="createClientNonce">Gives a fresh client nonce, for mechanisms that send one.</param>
    /// <returns>The exchange.</returns>
    IXmppSaslExchange Start(NetworkCredential? credentials, Func<string> createClientNonce);
}
