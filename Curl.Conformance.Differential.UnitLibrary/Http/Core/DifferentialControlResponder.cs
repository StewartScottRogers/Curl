using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// Answers one request on server.py's control port: <c>/reset</c> clears the request log and the counters
/// and answers <c>ok</c>; any other path answers the request log joined by <c>\0SEP\0</c> and clears it.
/// </summary>
public sealed class DifferentialControlResponder(DifferentialResponderState state)
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

        var body = TakeAnswer(parts[1]);
        var head = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n"));
        await DifferentialHttpResponseWriter.WriteRawAsync(connection, (byte[])[.. head, .. body], cancellationToken);
    }

    private byte[] TakeAnswer(string path)
    {
        if (path == "/reset")
        {
            state.Reset();
            return "ok"u8.ToArray();
        }

        return state.TakeRequestLog();
    }
}
