namespace Libcurl.Callbacks.ReadWrite;

/// <summary>
/// libcurl's default write and read functions over the <see cref="Stream"/> a <c>CURLOPT_WRITEDATA</c>,
/// <c>CURLOPT_HEADERDATA</c> or <c>CURLOPT_READDATA</c> names, the managed <c>FILE *</c>: <see cref="Write"/> is
/// <c>fwrite</c> and <see cref="Read"/> is <c>fread</c> (ADR-0569).
/// </summary>
/// <param name="stream">The stream the data goes to or comes from.</param>
internal sealed class StreamDataCallbacks(Stream stream)
{
    /// <summary>Gets the stream the data goes to or comes from.</summary>
    internal Stream Stream { get; } = stream;

    /// <summary>Writes <paramref name="data"/> to the stream, as <c>fwrite</c> does.</summary>
    /// <param name="data">The bytes received.</param>
    /// <returns>The bytes written: all of them, or 0 when the stream fails, which fails the transfer with <c>CURLE_WRITE_ERROR</c>.</returns>
    internal long Write(ReadOnlySpan<byte> data)
    {
        try
        {
            Stream.Write(data);
            return data.Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Fills <paramref name="buffer"/> from the stream, as <c>fread</c> does.</summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <returns>The bytes read, 0 at the end of the stream.</returns>
    internal long Read(Span<byte> buffer) => Stream.Read(buffer);
}
