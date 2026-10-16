using System.Text;

namespace Curl.Mcp;

/// <summary>
/// Collects the header lines a transfer's header callback is given into the last response's headers
/// (ADR-0593): a status line starts a new response, so the headers of a redirect, a <c>100</c> or a
/// proxy's <c>CONNECT</c> answer are dropped; a line starting with white space continues the header
/// before it; the blank line and lines with no name are skipped. Order and repeats are kept.
/// </summary>
public sealed class McpResponseHeaderCollector
{
    private readonly List<McpTransferHeader> headers = [];

    /// <summary>The last response's headers so far, in the order received.</summary>
    public IReadOnlyList<McpTransferHeader> Headers => headers;

    /// <summary>Takes one header line, as <c>CURLOPT_HEADERFUNCTION</c> gives it, end of line included.</summary>
    /// <param name="line">The header line's bytes, read as UTF-8.</param>
    /// <returns><paramref name="line" />'s length: every byte taken.</returns>
    public long Collect(ReadOnlySpan<byte> line)
    {
        string text = Encoding.UTF8.GetString(line).TrimEnd('\r', '\n');
        if (text.StartsWith("HTTP/", StringComparison.Ordinal))
        {
            headers.Clear();
        }
        else if (text.StartsWith(' ') || text.StartsWith('\t'))
        {
            ContinueLastHeader(text);
        }
        else
        {
            AddHeader(text);
        }

        return line.Length;
    }

    private void ContinueLastHeader(string text)
    {
        if (headers.Count > 0)
        {
            McpTransferHeader last = headers[^1];
            headers[^1] = last with { Value = $"{last.Value} {text.Trim()}".Trim() };
        }
    }

    private void AddHeader(string text)
    {
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            headers.Add(new McpTransferHeader(text[..colon], text[(colon + 1)..].Trim()));
        }
    }
}
