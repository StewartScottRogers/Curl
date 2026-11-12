using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Wraps the run's diagnostic log so that the warnings and errors of the <c>--csharp</c>, <c>--powershell</c> and
/// <c>--otel-endpoint</c> extensions reach standard error as a <c>Warning:</c> line, the way curl prints a failed
/// <c>--libcurl</c> write (ADR-1138, ADR-1134), unless <c>-s</c> is given. When the user asked for those levels with
/// <c>--log-level</c> the inner log already prints them, so nothing is printed twice.
/// </summary>
/// <param name="inner">The run's diagnostic log.</param>
/// <param name="writeWarningLine">Writes one line to standard error.</param>
internal sealed class ExtensionWarningDiagnosticLog(IDiagnosticLog inner, Action<string> writeWarningLine) : IDiagnosticLog
{
    private static readonly string[] Components = ["code-generation", "otel"];

    /// <summary>Wraps <paramref name="inner" /> so the warnings go to the stream <paramref name="standardError" /> gives at each write.</summary>
    /// <param name="inner">The run's diagnostic log.</param>
    /// <param name="standardError">Gives the run's standard error.</param>
    /// <param name="encoding">The encoding of the bytes written.</param>
    /// <returns>The wrapping log.</returns>
    internal static ExtensionWarningDiagnosticLog OverStandardError(IDiagnosticLog inner, Func<Stream> standardError, System.Text.Encoding encoding) =>
        new(inner, line =>
        {
            var stream = standardError();
            stream.Write(encoding.GetBytes(line + Environment.NewLine));
            stream.Flush();
        });

    /// <inheritdoc />
    public bool IsEnabled(DiagnosticLogLevel level) => inner.IsEnabled(level);

    /// <inheritdoc />
    public void Write(DiagnosticLogLevel level, string component, string message)
    {
        if (level is DiagnosticLogLevel.Warning or DiagnosticLogLevel.Error
            && !inner.IsEnabled(level)
            && Array.IndexOf(Components, component) >= 0)
        {
            writeWarningLine("Warning: " + message);
            return;
        }

        inner.Write(level, component, message);
    }
}
