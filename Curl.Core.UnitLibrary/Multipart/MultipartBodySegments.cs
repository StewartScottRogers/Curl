using System.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// The pieces of a multipart body as it is built: literal text, coalesced into memory
/// segments, and the file streams between them, with the running byte count.
/// </summary>
/// <param name="textEncoding">How literal text becomes bytes.</param>
internal sealed class MultipartBodySegments(Encoding textEncoding) : IDisposable
{
    private readonly List<Stream> segments = [];

    private readonly StringBuilder pendingText = new();

    private long knownLength;

    private bool lengthIsKnown = true;

    /// <summary>
    /// Gets the body's size in bytes, or <see langword="null" /> when a segment's size is
    /// unknown. Complete only after <see cref="ToStream" />.
    /// </summary>
    internal long? Length => lengthIsKnown ? knownLength : null;

    /// <summary>
    /// Gets a value indicating whether a part's data was refused by its encoder, which fails the
    /// body once every file has been opened, as curl fails it only when it reaches that data.
    /// </summary>
    internal bool HoldsRefusedData { get; private set; }

    /// <summary>Records that a part's encoder refused its data.</summary>
    internal void AddRefusedData() => HoldsRefusedData = true;

    /// <summary>Appends <paramref name="text" />, sent in the text encoding.</summary>
    /// <param name="text">The text.</param>
    internal void AddText(string text) => pendingText.Append(text);

    /// <summary>Appends <paramref name="content" />, which these segments now own.</summary>
    /// <param name="content">The stream to send.</param>
    /// <param name="length">Its size in bytes, or <see langword="null" /> when unknown.</param>
    internal void AddStream(Stream content, long? length)
    {
        FlushText();
        segments.Add(content);
        knownLength += length ?? 0;
        lengthIsKnown &= length.HasValue;
    }

    /// <summary>Hands every segment to one stream, which then owns them.</summary>
    /// <param name="allowsSeeking">Whether the body seeks when every segment seeks.</param>
    /// <returns>The body, read segment by segment.</returns>
    internal ConcatenatedReadStream ToStream(bool allowsSeeking = true)
    {
        FlushText();
        ConcatenatedReadStream body = new([.. segments], allowsSeeking);
        segments.Clear();
        return body;
    }

    /// <summary>Disposes every segment not yet handed to <see cref="ToStream" />.</summary>
    public void Dispose()
    {
        foreach (Stream segment in segments)
        {
            segment.Dispose();
        }

        segments.Clear();
    }

    private void FlushText()
    {
        byte[] bytes = textEncoding.GetBytes(pendingText.ToString());
        pendingText.Clear();
        segments.Add(new MemoryStream(bytes, writable: false));
        knownLength += bytes.Length;
    }
}
