using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Text;

/// <summary>
/// Answers an RTSP connection the way proto2_servers.py's <c>Rtsp</c> class does (port 18430): for each request
/// (lines up to a blank line) it logs the request as Latin-1 text and answers <c>200 OK</c> echoing the first
/// <c>CSeq</c> header, with an SDP body for DESCRIBE.
/// </summary>
public sealed class RtspResponder(ProtocolWireLog log)
{
    private static readonly byte[] CrLf = "\r\n"u8.ToArray();

    /// <summary>Serves requests until the client hangs up.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var reader = new DifferentialHttpLineReader(connection);
        var writer = new ProtocolLineWriter(connection);
        while (await ReadRequestAsync(reader, cancellationToken) is { Length: > 0 } request)
        {
            var text = Encoding.Latin1.GetString(request);
            log.Add(text);
            var cseq = text.Split("\r\n").FirstOrDefault(header => header.StartsWith("cseq", StringComparison.OrdinalIgnoreCase));
            var body = text.Split(' ')[0] == "DESCRIBE" ? "v=0\r\ns=mock\r\n"u8.ToArray() : [];
            var head = "RTSP/1.0 200 OK\r\n"
                + (cseq is null ? string.Empty : cseq + "\r\n")
                + "Public: OPTIONS, DESCRIBE\r\n"
                + (body.Length > 0 ? $"Content-Length: {body.Length}\r\nContent-Type: application/sdp\r\n" : string.Empty)
                + "\r\n";
            await writer.SendAsync((byte[])[.. Encoding.Latin1.GetBytes(head), .. body], cancellationToken);
        }
    }

    private static async Task<byte[]> ReadRequestAsync(DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        var request = new List<byte>();
        byte[] line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken);
            request.AddRange(line);
        }
        while (line.Length != 0 && !line.AsSpan().SequenceEqual(CrLf));

        return [.. request];
    }
}
