using System.Globalization;
using System.Text;
using Curl.Api.EngineContracts.WebSocket;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2302.c</c>'s <c>struct ws_data</c> with <c>add_data</c> and <c>flush_data</c>: the bytes of consecutive writes that
/// came with the same frame flags (or all without a frame), up to <see cref="BufferSize"/>, written as one hex line to
/// stdout, followed by <c>RECFLAGS: &lt;flags in hex&gt;</c> there, or by <c>RECFLAGS: NULL</c> on stderr when they came
/// without a frame.
/// </summary>
/// <param name="output">The program's stdout.</param>
/// <param name="error">The program's stderr.</param>
public sealed class Lib2302Frames(TextWriter output, TextWriter error)
{
    /// <summary><c>LIB2302_BUFSIZE</c>, the most bytes held at once.</summary>
    public const int BufferSize = 1024 * 1024;

    private readonly TextWriter output = output ?? throw new ArgumentNullException(nameof(output));
    private readonly TextWriter error = error ?? throw new ArgumentNullException(nameof(error));
    private readonly List<byte> buffer = [];
    private int writes;
    private CurlWebSocketFrame? frame;

    /// <summary><c>add_data</c>: flushes what is held when <paramref name="meta"/> differs from its frame, then holds <paramref name="data"/>.</summary>
    /// <param name="data">The write's bytes.</param>
    /// <param name="meta">The frame <c>curl_ws_meta</c> gave, or <see langword="null"/>.</param>
    /// <returns>The bytes held, or 0 when they would pass <see cref="BufferSize"/>.</returns>
    public long Add(ReadOnlySpan<byte> data, CurlWebSocketFrame? meta)
    {
        if (writes == 0 || meta.HasValue != frame.HasValue || (meta.HasValue && meta.Value.Flags != frame!.Value.Flags))
        {
            Flush();
            frame = meta;
        }

        if (buffer.Count + data.Length > BufferSize)
        {
            return 0;
        }

        buffer.AddRange(data);
        writes++;
        return data.Length;
    }

    /// <summary><c>flush_data</c>: writes what is held, when anything was written since the last flush.</summary>
    public void Flush()
    {
        if (writes == 0)
        {
            return;
        }

        var line = new StringBuilder();
        foreach (byte octet in buffer)
        {
            _ = line.Append(octet.ToString("x2", CultureInfo.InvariantCulture)).Append(' ');
        }

        output.Write(line.Append('\n').ToString());
        if (frame is { } held)
        {
            output.Write(string.Create(CultureInfo.InvariantCulture, $"RECFLAGS: {(int)held.Flags:x}\n"));
        }
        else
        {
            error.Write("RECFLAGS: NULL\n");
        }

        buffer.Clear();
        writes = 0;
    }
}
