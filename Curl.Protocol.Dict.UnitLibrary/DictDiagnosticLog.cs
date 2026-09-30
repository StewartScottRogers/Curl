using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Dict;

/// <summary>
/// Writes the DICT transfer steps to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Dict" /> (ADR-0222, BL-928): the failure that ends a
/// transfer as <c>error</c>, and the command sent and the transfer's end as <c>info</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. The command logged is the one line between
/// <c>CLIENT</c> and <c>QUIT</c>; a URL's user name and password are never part of it.
/// </remarks>
internal sealed class DictDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>The bytes after the command line: its CRLF, then <c>QUIT</c> and its CRLF.</summary>
    private const int CommandTrailerLength = 8;

    /// <summary>The bytes before the command line: the <c>CLIENT</c> line and its CRLF.</summary>
    private static readonly int CommandStart = DictRequest.ClientLine.Length + 2;

    /// <summary>
    /// Logs, at <c>info</c>, the command line of the request sent, each byte read as
    /// Latin-1 so a byte that is not ASCII survives as one character.
    /// </summary>
    /// <param name="request">The whole request, as <see cref="DictRequest.TryEncode" /> built it.</param>
    public void CommandSent(byte[] request)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            string command = Encoding.Latin1.GetString(request, CommandStart, request.Length - CommandStart - CommandTrailerLength);
            Write(DiagnosticLogLevel.Info, "sent " + command);
        }
    }

    /// <summary>
    /// Logs how the transfer ended: <c>info</c> with the bytes and milliseconds for a success,
    /// <c>error</c> with the <see cref="CurlExitCode" /> and message for a failure.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="elapsed">How long the transfer took.</param>
    public void TransferEnded(TransferResult result, TimeSpan elapsed)
    {
        if (!result.IsSuccess)
        {
            Failed(result);
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"transfer finished: {result.BytesTransferred} bytes in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>Logs, at <c>error</c>, a failure that ends the transfer with its <see cref="CurlExitCode" />.</summary>
    /// <param name="result">The failure.</param>
    private void Failed(TransferResult result)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, string.Create(
                CultureInfo.InvariantCulture,
                $"failed with {result.ExitCode} ({(int)result.ExitCode}): {result.ErrorMessage}"));
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Dict, message);
}
