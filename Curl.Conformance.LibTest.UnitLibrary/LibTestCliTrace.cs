using System.Text;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Info;

namespace Curl.Conformance.LibTest;

/// <summary>
/// curl 8.22.0's <c>tests/libtest/testtrace.c</c> <c>cli_debug_cb</c>, the client tests'
/// <c>CURLOPT_DEBUGFUNCTION</c>: the trace look libcurl's own verbose output has, each line started
/// with <c>* </c>, <c>&lt; </c>, <c>&gt; </c>, <c>{ </c> or <c>} </c> after a
/// <c>[transfer-connection] </c> tag, and a run of data as one <c>[N bytes data]</c> line. Upstream's
/// <c>static</c> line state lives in the instance, so one trace serves one program. Bytes are written
/// one character each (Latin-1).
/// </summary>
/// <param name="output">Where the trace goes, upstream's <c>stderr</c>.</param>
public sealed class LibTestCliTrace(TextWriter output)
{
    private static readonly string[] LineStarts = ["* ", "< ", "> ", "{ ", "} ", "{ ", "} "];

    private readonly TextWriter output = output ?? throw new ArgumentNullException(nameof(output));
    private bool midLine;
    private bool tracedData;

    /// <summary>The debug callback for <paramref name="easy"/>, whose ids tag each line.</summary>
    /// <param name="easy">The handle the callback is set on.</param>
    /// <returns>A callback that calls <see cref="Trace"/> for <paramref name="easy"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public DebugCallback CallbackFor(CurlEasy easy)
    {
        ArgumentNullException.ThrowIfNull(easy);
        return (type, data) => Trace(easy, type, data);
    }

    /// <summary><c>cli_debug_cb</c>: traces one piece of debug data from <paramref name="easy"/>; always answers 0.</summary>
    /// <param name="easy">The handle whose <c>CURLINFO_XFER_ID</c> and <c>CURLINFO_CONN_ID</c> tag the lines.</param>
    /// <param name="type">What kind of data it is.</param>
    /// <param name="data">The data.</param>
    /// <returns>0, as upstream's callback returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public int Trace(CurlEasy easy, CurlInfoType type, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(easy);
        string ids = IdsTag(easy);
        switch (type)
        {
            case CurlInfoType.HeaderOut:
                TraceHeaderOut(ids, type, data);
                break;
            case CurlInfoType.Text:
            case CurlInfoType.HeaderIn:
                StartLineUnlessMidLine(ids, type);
                output.Write(Latin1(data));
                EndWith(data);
                break;
            case CurlInfoType.DataOut:
            case CurlInfoType.DataIn:
            case CurlInfoType.SslDataIn:
            case CurlInfoType.SslDataOut:
                TraceData(ids, type, data.Length);
                break;
            default:
                midLine = false;
                tracedData = true;
                break;
        }

        return 0;
    }

    private static string IdsTag(CurlEasy easy)
    {
        if (!TryReadId(easy, CurlInfo.XferId, out long transferId))
        {
            return string.Empty;
        }

        return TryReadId(easy, CurlInfo.ConnId, out long connectionId)
            ? $"[{transferId}-{connectionId}] "
            : $"[{transferId}-x] ";
    }

    // Libcurl answers CURLINFO_XFER_ID and CURLINFO_CONN_ID on every live handle, -1 when there is
    // none yet, so upstream's failed-getinfo branch never arises and only the sign is tested.
    private static bool TryReadId(CurlEasy easy, CurlInfo info, out long id)
    {
        easy.GetInfo(info, out object? value);
        id = (long)value!;
        return id >= 0;
    }

    // Each LF inside the data (not its last byte) ends a line; the next line gets its start unless
    // the previous call left one open. The rest, through the last byte, follows as one write.
    private void TraceHeaderOut(string ids, CurlInfoType type, ReadOnlySpan<byte> data)
    {
        if (data.Length > 0)
        {
            int start = 0;
            for (int index = 0; index < data.Length - 1; index++)
            {
                if (data[index] == (byte)'\n')
                {
                    StartLineUnlessMidLine(ids, type);
                    output.Write(Latin1(data[start..(index + 1)]));
                    start = index + 1;
                    midLine = false;
                }
            }

            StartLineUnlessMidLine(ids, type);
            output.Write(Latin1(data[start..]));
        }

        EndWith(data);
    }

    private void TraceData(string ids, CurlInfoType type, int size)
    {
        if (tracedData)
        {
            return;
        }

        StartLineUnlessMidLine(ids, type);
        output.Write($"[{size} bytes data]\n");
        midLine = false;
        tracedData = true;
    }

    private void StartLineUnlessMidLine(string ids, CurlInfoType type)
    {
        if (!midLine)
        {
            output.Write(ids + LineStarts[(int)type]);
        }
    }

    private void EndWith(ReadOnlySpan<byte> data)
    {
        midLine = data.Length > 0 && data[^1] != (byte)'\n';
        tracedData = false;
    }

    private static string Latin1(ReadOnlySpan<byte> data) => Encoding.Latin1.GetString(data);
}
