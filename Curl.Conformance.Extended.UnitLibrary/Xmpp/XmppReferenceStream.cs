using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Curl.Conformance.Extended.Xmpp;

/// <summary>
/// One XML stream of <see cref="XmppReferenceServer" /> (RFC 6120, 4): it reads curl's stream header and then one
/// first-level element at a time, and writes the server's text. A stream restart is a new instance on the same bytes.
/// </summary>
internal sealed class XmppReferenceStream
{
    private readonly Stream stream;

    private readonly XmlReader reader;

    private readonly CancellationToken cancellationToken;

    internal XmppReferenceStream(Stream stream, CancellationToken cancellationToken)
    {
        this.stream = stream;
        this.cancellationToken = cancellationToken;
        reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false });
    }

    /// <summary>Gets a value indicating whether the last read found curl's closing <c>&lt;/stream:stream&gt;</c>.</summary>
    internal bool ClosedByClient { get; private set; }

    /// <summary>Gives a new stream on the same bytes, for a stream restart (RFC 6120, 5.4.3.3 and 6.4.6).</summary>
    /// <returns>The restarted stream.</returns>
    internal XmppReferenceStream Restart() => new(stream, cancellationToken);

    /// <summary>Reads curl's stream header and answers with the server's header, carrying <paramref name="identifier" />, and <paramref name="features" />.</summary>
    /// <param name="identifier">The stream ID (RFC 6120, 4.7.3).</param>
    /// <param name="features">The children of <c>&lt;stream:features/&gt;</c>.</param>
    /// <returns>A task that completes when the features are sent.</returns>
    internal async Task OpenAsync(string identifier, string features)
    {
        await reader.MoveToContentAsync().ConfigureAwait(false);
        await SendAsync(
            $"<?xml version='1.0'?><stream:stream from='{XmppReferenceServer.Domain}' id='{identifier}' version='1.0' xml:lang='en' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'><stream:features>{features}</stream:features>")
            .ConfigureAwait(false);
    }

    /// <summary>Reads curl's next first-level element.</summary>
    /// <returns>The element; <see langword="null" /> when curl closed the stream or the connection.</returns>
    internal async Task<XElement?> ReadAsync()
    {
        try
        {
            await reader.ReadAsync().ConfigureAwait(false);
            if (reader.NodeType != XmlNodeType.Element)
            {
                ClosedByClient = reader.NodeType == XmlNodeType.EndElement;
                return null;
            }

            using XmlReader subtree = reader.ReadSubtree();
            await subtree.ReadAsync().ConfigureAwait(false);
            return (XElement)await XNode.ReadFromAsync(subtree, cancellationToken).ConfigureAwait(false);
        }
        catch (XmlException)
        {
            // curl ended the connection inside its open stream.
            return null;
        }
    }

    /// <summary>Writes <paramref name="text" /> as UTF-8 in one write.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A task that completes when the text is flushed.</returns>
    internal async Task SendAsync(string text)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
