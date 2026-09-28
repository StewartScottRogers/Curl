namespace Curl.Output;

/// <summary>
/// Formats the current time for a <c>-w</c> <c>%time{format}</c> directive as curl 8.21.0
/// prints it through the C runtime <see cref="WriteOutTimeDialect"/> names.
/// </summary>
public static class WriteOutTimeFormatter
{
    /// <summary>
    /// Formats the time <paramref name="timeProvider"/> reads now with <paramref name="format"/>,
    /// the text between the braces of <c>%time{…}</c>, in <paramref name="dialect"/>.
    /// </summary>
    /// <param name="format">The format, without the braces.</param>
    /// <param name="dialect">The C runtime whose <c>strftime</c> curl hands the format to.</param>
    /// <param name="timeProvider">Supplies the current UTC time and the local time zone.</param>
    /// <returns>The formatted time, or an empty string when curl would print nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dialect"/> is not a <see cref="WriteOutTimeDialect"/> value.</exception>
    public static string Format(string format, WriteOutTimeDialect dialect, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return dialect switch
        {
            WriteOutTimeDialect.WindowsCRuntime => WindowsCRuntimeTimeFormat.Format(format, timeProvider),
            WriteOutTimeDialect.Glibc => GlibcTimeFormat.Format(format, timeProvider),
            _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Not a WriteOutTimeDialect value."),
        };
    }
}
