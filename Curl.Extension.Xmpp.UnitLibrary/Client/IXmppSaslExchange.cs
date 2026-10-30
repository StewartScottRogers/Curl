namespace Curl.Extension.Xmpp.Client;

/// <summary>One SASL authentication's client side (RFC 6120, 6.4), in decoded text; the flow carries it base64 encoded.</summary>
internal interface IXmppSaslExchange
{
    /// <summary>Gets the initial response the <c>&lt;auth/&gt;</c> element carries.</summary>
    string InitialResponse { get; }

    /// <summary>Gives the <c>&lt;response/&gt;</c> to the server's <c>&lt;challenge/&gt;</c> <paramref name="challenge"/>.</summary>
    /// <param name="challenge">The challenge, decoded.</param>
    /// <returns>The response, not yet encoded.</returns>
    /// <exception cref="XmppClientException">The challenge is not one the mechanism accepts.</exception>
    string Respond(string challenge);

    /// <summary>Checks the additional data of the server's <c>&lt;success/&gt;</c> (RFC 6120, 6.3.10).</summary>
    /// <param name="additionalData">The additional data, decoded; empty when there is none.</param>
    /// <exception cref="XmppClientException">The data does not prove the server knows the credentials.</exception>
    void Complete(string additionalData);
}
