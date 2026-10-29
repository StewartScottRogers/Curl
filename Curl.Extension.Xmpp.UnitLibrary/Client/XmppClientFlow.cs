using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Curl.Extension.Xmpp.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Xmpp.Client;

/// <summary>
/// The XMPP client exchange (RFC 6120; ADR-1068): opens the stream (4.2), negotiates STARTTLS when the server offers it
/// and the connection is not yet secure (5.4), authenticates with SASL PLAIN, or ANONYMOUS without credentials (6.4),
/// restarting the stream after each (4.3.3), binds a server-chosen resource (7.6), sends the request stanza, writes a
/// disco answer's payload to the output, then closes the stream (4.4) and reads until the server closes its own.
/// </summary>
internal sealed class XmppClientFlow(IConnection connection, ITlsProvider tlsProvider, string domain, Stream output, ITransferProgress progress, CancellationToken cancellationToken)
    : IAsyncDisposable
{
    /// <summary>The <c>id</c> of the resource binding <c>&lt;iq/&gt;</c>.</summary>
    internal const string BindIdentifier = "bind";

    /// <summary>The closing tag of the client's stream.</summary>
    internal const string StreamClose = "</stream:stream>";

    private readonly IConnection plaintext = connection;

    private IConnection transport = connection;

    private XmppStreamReader reader = null!;

    /// <summary>The bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>Gives the client's stream header to <paramref name="serverDomain"/> (RFC 6120, 4.7 and 4.8).</summary>
    internal static string StreamHeader(string serverDomain) =>
        $"<?xml version='1.0'?><stream:stream to='{System.Security.SecurityElement.Escape(serverDomain)}' version='1.0' xml:lang='en' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>";

    /// <summary>Runs the exchange, authenticating with <paramref name="credentials"/> and sending <paramref name="request"/>.</summary>
    internal async ValueTask RunAsync(NetworkCredential? credentials, XElement request)
    {
        XElement features = await OpenStreamAsync().ConfigureAwait(false);
        if (!transport.IsSecure && features.Element(XmppNamespaces.Tls + "starttls") is not null)
        {
            features = await StartTlsAsync().ConfigureAwait(false);
        }

        await AuthenticateAsync(features, credentials).ConfigureAwait(false);
        await BindAsync().ConfigureAwait(false);
        await SendAsync(Encode(request)).ConfigureAwait(false);
        if (request.Name.LocalName == "iq")
        {
            await WriteDiscoAnswerAsync().ConfigureAwait(false);
        }

        await SendAsync(StreamClose).ConfigureAwait(false);
        await DrainUntilClosedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!ReferenceEquals(transport, plaintext))
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Gives the name of the first child of <paramref name="container"/>: an error's defined condition (RFC 6120, 4.9.2 and 8.3.2).</summary>
    internal static string ConditionOf(XElement? container) =>
        container?.Elements().FirstOrDefault()?.Name.LocalName ?? "undefined-condition";

    private async ValueTask<XElement> OpenStreamAsync()
    {
        await SendAsync(StreamHeader(domain)).ConfigureAwait(false);
        reader = new XmppStreamReader(transport, cancellationToken);
        return await ExpectAsync(XmppNamespaces.Stream + "features").ConfigureAwait(false);
    }

    private async ValueTask<XElement> StartTlsAsync()
    {
        await SendAsync(Encode(new XElement(XmppNamespaces.Tls + "starttls"))).ConfigureAwait(false);
        await ExpectAsync(XmppNamespaces.Tls + "proceed").ConfigureAwait(false);
        ConnectResult secured = await tlsProvider.AuthenticateAsClientAsync(transport, domain, cancellationToken).ConfigureAwait(false);
        transport = secured.Connection ?? throw new XmppClientException(secured.ExitCode, secured.ErrorMessage!);
        return await OpenStreamAsync().ConfigureAwait(false);
    }

    private async ValueTask<XElement> AuthenticateAsync(XElement features, NetworkCredential? credentials)
    {
        (string mechanism, string response) = credentials is null
            ? ("ANONYMOUS", "=")
            : ("PLAIN", Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{credentials.UserName}\0{credentials.Password}")));
        bool offered = features.Elements(XmppNamespaces.Sasl + "mechanisms").Elements(XmppNamespaces.Sasl + "mechanism").Any(offer => offer.Value == mechanism);
        if (!offered)
        {
            throw new XmppClientException(CurlExitCode.LoginDenied, XmppClientMessages.MechanismNotOffered(mechanism));
        }

        await SendAsync(Encode(new XElement(XmppNamespaces.Sasl + "auth", new XAttribute("mechanism", mechanism), response))).ConfigureAwait(false);
        XElement outcome = await NextAsync().ConfigureAwait(false);
        if (outcome.Name == XmppNamespaces.Sasl + "failure")
        {
            throw new XmppClientException(CurlExitCode.LoginDenied, XmppClientMessages.AuthenticationFailed(ConditionOf(outcome)));
        }

        if (outcome.Name != XmppNamespaces.Sasl + "success")
        {
            throw Unexpected(outcome);
        }

        return await OpenStreamAsync().ConfigureAwait(false);
    }

    private async ValueTask BindAsync()
    {
        XElement bind = new(
            XmppNamespaces.Client + "iq",
            new XAttribute("type", "set"),
            new XAttribute("id", BindIdentifier),
            new XElement(XmppNamespaces.Bind + "bind"));
        await SendAsync(Encode(bind)).ConfigureAwait(false);
        XElement answer = await ExpectAsync(XmppNamespaces.Client + "iq").ConfigureAwait(false);
        ThrowWhenError(answer);
    }

    private async ValueTask WriteDiscoAnswerAsync()
    {
        XElement answer = await NextAsync().ConfigureAwait(false);
        while (answer.Name != XmppNamespaces.Client + "iq" || (string?)answer.Attribute("id") != XmppRequests.DiscoIdentifier)
        {
            answer = await NextAsync().ConfigureAwait(false);
        }

        ThrowWhenError(answer);
        await WriteOutputAsync(Encoding.UTF8.GetBytes(string.Concat(answer.Elements().Select(payload => payload.ToString(SaveOptions.DisableFormatting))) + "\n")).ConfigureAwait(false);
    }

    private async ValueTask DrainUntilClosedAsync()
    {
        for (XElement? element = await ReadElementAsync().ConfigureAwait(false); element is not null; element = await ReadElementAsync().ConfigureAwait(false))
        {
            ThrowWhenStreamError(element);
            ThrowWhenError(element);
        }
    }

    private static void ThrowWhenError(XElement stanza)
    {
        if ((string?)stanza.Attribute("type") == "error")
        {
            throw new XmppClientException(CurlExitCode.WeirdServerReply, XmppClientMessages.StanzaError(ConditionOf(stanza.Element(XmppNamespaces.Client + "error"))));
        }
    }

    private static void ThrowWhenStreamError(XElement element)
    {
        if (element.Name == XmppNamespaces.Stream + "error")
        {
            throw new XmppClientException(CurlExitCode.WeirdServerReply, XmppClientMessages.StreamError(ConditionOf(element)));
        }
    }

    private static XmppClientException Unexpected(XElement element) =>
        new(CurlExitCode.WeirdServerReply, XmppClientMessages.UnexpectedElement(element.Name.LocalName));

    private async ValueTask<XElement> ExpectAsync(XName name)
    {
        XElement element = await NextAsync().ConfigureAwait(false);
        return element.Name == name ? element : throw Unexpected(element);
    }

    private async ValueTask<XElement> NextAsync()
    {
        XElement element = await ReadElementAsync().ConfigureAwait(false)
            ?? throw new XmppClientException(CurlExitCode.RecvError, XmppClientMessages.Disconnected);
        ThrowWhenStreamError(element);
        return element;
    }

    private async ValueTask<XElement?> ReadElementAsync()
    {
        try
        {
            return await reader.ReadAsync().ConfigureAwait(false);
        }
        catch (XmlException)
        {
            throw new XmppClientException(CurlExitCode.WeirdServerReply, XmppClientMessages.Malformed);
        }
        catch (IOException)
        {
            throw new XmppClientException(CurlExitCode.RecvError, XmppClientMessages.Disconnected);
        }
    }

    private static string Encode(XElement element)
    {
        try
        {
            return element.ToString(SaveOptions.DisableFormatting);
        }
        catch (ArgumentException)
        {
            throw new XmppClientException(CurlExitCode.BadFunctionArgument, XmppClientMessages.ForbiddenCharacter);
        }
    }

    private async ValueTask SendAsync(string text)
    {
        try
        {
            await transport.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new XmppClientException(CurlExitCode.SendError, XmppClientMessages.SendFailed);
        }
    }

    private async ValueTask WriteOutputAsync(byte[] text)
    {
        try
        {
            await output.WriteAsync(text, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new XmppClientException(CurlExitCode.WriteError, XmppClientMessages.OutputWriteFailed(text.Length));
        }

        BytesWritten += text.Length;
        progress.ReportDownloaded(BytesWritten, null);
    }
}
