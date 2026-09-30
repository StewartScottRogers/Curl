using System.Text;

namespace Curl.Console;

/// <summary>
/// The <see cref="TextWriter" /> the diagnostic log writes to when there is no <c>--log-file</c>:
/// the runner's standard error as it stands at each write, so the lines follow <c>--stderr</c> and
/// the run's write gate (ADR-0222, decision 4). Text is written as UTF-8.
/// </summary>
/// <param name="currentStandardError">Gets the stream standard error goes to now.</param>
internal sealed class StandardErrorLogTarget(Func<Stream> currentStandardError) : TextWriter
{
    /// <inheritdoc />
    /// <returns>UTF-8 without a byte order mark.</returns>
    public override Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes <paramref name="value" /> to standard error as UTF-8, in one write.
    /// </summary>
    /// <param name="value">The text; <see langword="null" /> writes nothing.</param>
    public override void Write(string? value) => currentStandardError().Write(Encoding.GetBytes(value ?? string.Empty));

    /// <summary>
    /// Flushes standard error.
    /// </summary>
    public override void Flush() => currentStandardError().Flush();
}
