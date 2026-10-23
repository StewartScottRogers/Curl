using System.Globalization;
using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>
/// A content- or transfer-encoding route of server.py: it answers one path, whatever the method, with fixed
/// bytes and says whether the connection closes afterwards (server.py's <c>return True</c>).
/// </summary>
public abstract class EncodingRoute(string path, bool closesConnection) : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == path;

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteRawAsync(context.Connection, ResponseBytes(), cancellationToken);
        return closesConnection;
    }

    /// <summary>The bytes the route sends.</summary>
    protected abstract byte[] ResponseBytes();

    /// <summary>The bytes of <paramref name="text"/>, as Python's <c>str.encode()</c> gives for ASCII text.</summary>
    protected static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary><paramref name="text"/> repeated <paramref name="count"/> times as bytes, as Python's <c>b"..." * n</c>.</summary>
    protected static byte[] Repeated(string text, int count) => Ascii(string.Concat(Enumerable.Repeat(text, count)));

    /// <summary>What server.py's <c>resp(c, 200, headers, body)</c> sends.</summary>
    protected static byte[] OkResponse(IReadOnlyList<(string Name, string Value)> headers, byte[] body) =>
        DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(200, headers, body));

    /// <summary>
    /// What <c>/te-gzip-chunked</c> sends: a head with no <c>Date</c> under <c>Transfer-Encoding: gzip, chunked</c>,
    /// then <paramref name="member"/> as one chunk with its size in lower-case hexadecimal, then the last chunk.
    /// </summary>
    protected static byte[] ChunkedGzip(byte[] member) =>
    [
        .. Ascii("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n"),
        .. Ascii(member.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n"),
        .. member,
        .. Ascii("\r\n0\r\n\r\n"),
    ];
}
