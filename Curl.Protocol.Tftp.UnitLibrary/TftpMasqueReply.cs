using System.Globalization;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Reads an HTTP or HTTPS proxy's reply to the MASQUE <c>connect-udp</c> request and turns
/// it into the failure curl 8.21.0's Schannel build ends a <c>tftp://</c> transfer with
/// (ADR-0056, rule 4; ADR-0096).
/// </summary>
/// <remarks>
/// Measured by BL-398: a <c>101</c> or <c>2xx</c> status is exit 7
/// <c>bind() failed; Invalid arguments</c>; any other status is exit 7
/// <c>CONNECT-UDP tunnel failed, response N</c>, with <c>N</c> 0 when the first line is not an
/// HTTP status line; a proxy that closes before its header block ends is exit 56
/// <c>Proxy CONNECT aborted</c>. The reply is read one byte at a time up to the empty line
/// that ends its header block, and nothing after it is read.
/// </remarks>
internal static class TftpMasqueReply
{
    /// <summary>What curl 8.21.0's Schannel build reports once the proxy accepts the tunnel.</summary>
    public const string BindFailedMessage = "bind() failed; Invalid arguments";

    /// <summary>
    /// Reads the proxy's reply from <paramref name="connection" /> and returns the failure
    /// the transfer ends with.
    /// </summary>
    /// <param name="connection">The connection to the proxy, the request already written.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The transfer's result: always a failure, as curl never tunnels TFTP.</returns>
    public static async ValueTask<TransferResult> ReadAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var header = new List<byte>();
        var oneByte = new byte[1];
        var lineStart = 0;
        while (await connection.ReadAsync(oneByte, cancellationToken).ConfigureAwait(false) == 1)
        {
            header.Add(oneByte[0]);
            if (oneByte[0] != '\n')
            {
                continue;
            }

            if (IsEmptyLine(header, lineStart))
            {
                return ResultForStatus(ParseStatusCode(header));
            }

            lineStart = header.Count;
        }

        return TransferResult.Failure(CurlExitCode.RecvError, "Proxy CONNECT aborted");
    }

    private static TransferResult ResultForStatus(int statusCode) =>
        statusCode == 101 || statusCode / 100 == 2
            ? TransferResult.Failure(CurlExitCode.CouldntConnect, BindFailedMessage)
            : TransferResult.Failure(
                CurlExitCode.CouldntConnect,
                string.Create(CultureInfo.InvariantCulture, $"CONNECT-UDP tunnel failed, response {statusCode}"));

    // The line from lineStart to the LF just read holds nothing but that LF, or a CR and it.
    private static bool IsEmptyLine(List<byte> header, int lineStart) =>
        header.Count - lineStart == 1 || (header.Count - lineStart == 2 && header[lineStart] == '\r');

    // "HTTP/1.1 403 Forbidden": the version, a space, exactly three digits, then a space or
    // the end of the line; anything else is status 0 (measured: "garbage" is response 0).
    private static int ParseStatusCode(List<byte> header)
    {
        var firstLine = Encoding.Latin1.GetString([.. header[..header.IndexOf((byte)'\n')]]).TrimEnd('\r');
        var parts = firstLine.Split(' ', 3);
        return parts.Length >= 2
            && parts[0].StartsWith("HTTP/", StringComparison.Ordinal)
            && parts[1].Length == 3
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var statusCode)
                ? statusCode
                : 0;
    }
}
