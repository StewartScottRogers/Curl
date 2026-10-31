using System.Xml;
using System.Xml.Linq;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Xmpp.Wire;

/// <summary>
/// Reads one XMPP stream from the server (RFC 6120, 4.2 "Opening a Stream" and 4.4 "Closing a Stream"): past the
/// <c>&lt;stream:stream&gt;</c> header, it gives each first-level element (a stream feature, a stanza, a stream error)
/// whole, and <see langword="null"/> once the server closes the stream or the connection. A stream restart (4.3.3)
/// starts a new reader. DTDs are refused (RFC 6120, 11.1 "XML Restrictions").
/// </summary>
internal sealed class XmppStreamReader
{
    private readonly ConnectionReadStream source;

    private readonly XmlReader reader;

    private readonly CancellationToken cancellationToken;

    /// <summary>Starts reading the stream <paramref name="connection"/> carries.</summary>
    internal XmppStreamReader(IConnection connection, CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
        source = new ConnectionReadStream(connection, cancellationToken);
        reader = XmlReader.Create(source, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
    }

    /// <summary>Gives the next first-level element, or <see langword="null"/> when the stream or connection has ended.</summary>
    /// <exception cref="XmlException">The server sent XML that is not well formed.</exception>
    internal async ValueTask<XElement?> ReadAsync()
    {
        try
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    return null;
                }

                if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1)
                {
                    using XmlReader subtree = reader.ReadSubtree();
                    await subtree.ReadAsync().ConfigureAwait(false);
                    return (XElement)await XNode.ReadFromAsync(subtree, cancellationToken).ConfigureAwait(false);
                }
            }

            return null;
        }
        catch (XmlException) when (source.ReachedEnd)
        {
            return null;
        }
    }
}
