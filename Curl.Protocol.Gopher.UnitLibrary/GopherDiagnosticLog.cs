using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Writes the Gopher transfer steps to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Gopher" /> (ADR-0222, BL-928): the failure that ends a
/// transfer as <c>error</c>, and the selector sent and the transfer's end as <c>info</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting.
/// </remarks>
internal sealed class GopherDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>
    /// Logs, at <c>info</c>, the selector sent, its bytes read as UTF-8 and without the
    /// CRLF that follows it.
    /// </summary>
    /// <param name="selector">The selector's bytes, as sent.</param>
    public void SelectorSent(byte[] selector)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "sent selector " + Encoding.UTF8.GetString(selector));
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

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Gopher, message);
}
