using System.Globalization;
using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Protocols.Core;

/// <summary>
/// Answers one request on the protocol servers' control port, as proto_servers.py's <c>Ctl</c> did:
/// <c>/reset</c> clears the wire log and answers <c>ok</c>; any other path answers the wire log joined by
/// <c>\n</c> and clears it.
/// </summary>
public sealed class ProtocolControlResponder(ProtocolWireLog log)
{
    /// <summary>Reads one request from <paramref name="connection"/> and answers it; a request line without a target gets no answer.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var reader = new DifferentialHttpLineReader(connection);
        var parts = Encoding.UTF8.GetString(await reader.ReadLineAsync(cancellationToken))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        byte[] line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken);
        }
        while (line.Length != 0 && !line.AsSpan().SequenceEqual("\r\n"u8));

        if (parts.Length < 2)
        {
            return;
        }

        byte[] body;
        if (parts[1] == "/reset")
        {
            log.Reset();
            body = "ok"u8.ToArray();
        }
        else
        {
            body = log.Take();
        }

        var head = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n"));
        await DifferentialHttpResponseWriter.WriteRawAsync(connection, (byte[])[.. head, .. body], cancellationToken);
    }
}
