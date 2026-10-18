using System.Text;
using Curl.Api.Callbacks;

namespace Curl.Conformance.LibTest;

/// <summary>
/// What libcurl itself writes to <c>stderr</c> for a handle with <c>CURLOPT_VERBOSE</c> and no
/// <c>CURLOPT_DEBUGFUNCTION</c>: text as <c>* </c> lines, received headers as <c>&lt; </c> lines and sent headers as
/// <c>&gt; </c> lines, one prefix per line, and no data. Bytes are written one character each (Latin-1).
/// </summary>
/// <param name="output">Where the lines go, upstream's <c>stderr</c>.</param>
public sealed class LibTestDefaultVerboseTrace(TextWriter output)
{
    private readonly TextWriter output = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>Writes one piece of debug data the way libcurl's default verbose output does; always answers 0.</summary>
    /// <param name="type">What kind of data it is; data and TLS bytes are not shown.</param>
    /// <param name="data">The data.</param>
    /// <returns>0, as a debug callback answers.</returns>
    public int Trace(CurlInfoType type, ReadOnlySpan<byte> data)
    {
        string? prefix = type switch
        {
            CurlInfoType.Text => "* ",
            CurlInfoType.HeaderIn => "< ",
            CurlInfoType.HeaderOut => "> ",
            _ => null,
        };
        if (prefix is not null)
        {
            WriteLines(prefix, Encoding.Latin1.GetString(data));
        }

        return 0;
    }

    private void WriteLines(string prefix, string text)
    {
        int start = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\n', start);
            end = end < 0 ? text.Length : end + 1;
            output.Write(prefix);
            output.Write(text.AsSpan(start, end - start));
            start = end;
        }

        output.Flush();
    }
}
