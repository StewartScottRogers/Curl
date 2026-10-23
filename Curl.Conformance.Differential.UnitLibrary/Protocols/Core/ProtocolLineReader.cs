using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Protocols.Core;

/// <summary>
/// Reads a protocol server's command lines the way proto_servers.py's <c>Line.rl</c> does: one line
/// decoded as Latin-1 with its trailing <c>\r</c> and <c>\n</c> characters stripped.
/// </summary>
public sealed class ProtocolLineReader(Stream stream)
{
    private readonly DifferentialHttpLineReader _lines = new(stream);

    /// <summary>Reads the next line; <see langword="null"/> means the stream has ended.</summary>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = await _lines.ReadLineAsync(cancellationToken);
        return line.Length == 0 ? null : Encoding.Latin1.GetString(line).TrimEnd('\r', '\n');
    }
}
