using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Text;

/// <summary>
/// Answers a Gopher connection the way proto2_servers.py's <c>Gopher</c> class does (port 18410): reads one line,
/// logs its Python <c>repr</c>, and sends the one-item menu.
/// </summary>
public sealed class GopherResponder(ProtocolWireLog log)
{
    /// <summary>Serves one connection: one selector line in, one menu out.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var line = await new DifferentialHttpLineReader(connection).ReadLineAsync(cancellationToken);
        log.Add(ProtocolBytesRepr.Format(line));
        await new ProtocolLineWriter(connection).SendAsync("iHello gopher\tfake\t(NULL)\t0\r\n.\r\n", cancellationToken);
    }
}
