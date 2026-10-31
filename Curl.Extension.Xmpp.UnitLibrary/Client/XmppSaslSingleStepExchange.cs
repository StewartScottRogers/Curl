using Curl.Protocol.Abstractions;

namespace Curl.Extension.Xmpp.Client;

/// <summary>An exchange that sends its whole response in <c>&lt;auth/&gt;</c> and takes no challenge, as PLAIN and ANONYMOUS do.</summary>
/// <param name="initialResponse">The response the <c>&lt;auth/&gt;</c> element carries.</param>
internal sealed class XmppSaslSingleStepExchange(string initialResponse) : IXmppSaslExchange
{
    /// <inheritdoc />
    public string InitialResponse { get; } = initialResponse;

    /// <inheritdoc />
    public string Respond(string challenge) =>
        throw new XmppClientException(CurlExitCode.WeirdServerReply, XmppClientMessages.UnexpectedElement("challenge"));

    /// <inheritdoc />
    public void Complete(string additionalData)
    {
    }
}
