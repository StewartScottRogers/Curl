namespace Curl.Protocol.Abstractions;

/// <summary>
/// Where a component writes Curl's own diagnostic log, the <c>--log-level</c> output that
/// explains what the implementation did (ADR-0222). Separate from
/// <see cref="ITransferEvents" />, which writes curl's own <c>-v</c> and <c>--trace</c>
/// bytes.
/// </summary>
/// <remarks>
/// Test <see cref="IsEnabled" /> before building a message, so a disabled level costs no
/// formatting. Never write a credential (ADR-0222, decision 7).
/// </remarks>
public interface IDiagnosticLog
{
    /// <summary>
    /// Gets whether a line at <paramref name="level" /> would be written.
    /// </summary>
    /// <param name="level">The level of the line about to be written.</param>
    /// <returns><see langword="true" /> when a line at that level is written.</returns>
    bool IsEnabled(DiagnosticLogLevel level);

    /// <summary>
    /// Writes one whole diagnostic line, if <paramref name="level" /> is enabled.
    /// </summary>
    /// <param name="level">The line's level.</param>
    /// <param name="component">
    /// The area writing it, one of the <see cref="DiagnosticLogComponents" /> names.
    /// </param>
    /// <param name="message">The message, without a line end.</param>
    void Write(DiagnosticLogLevel level, string component, string message);
}
