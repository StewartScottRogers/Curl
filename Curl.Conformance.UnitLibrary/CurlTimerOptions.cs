namespace Curl.Conformance;

/// <summary>
/// Finds the options that start a timer in curl which races the server - <c>--max-time</c>,
/// <c>--connect-timeout</c>, <c>--speed-time</c>, <c>--speed-limit</c> and
/// <c>--expect100-timeout</c> - in a command line, so a case naming one runs its emulated server
/// on the real clock and every other case on a <see cref="WaitSkippingTimeProvider"/> (BL-1355).
/// </summary>
public static class CurlTimerOptions
{
    private static readonly HashSet<string> LongTimerOptions =
        new(["--max-time", "--connect-timeout", "--speed-time", "--speed-limit", "--expect100-timeout"], StringComparer.Ordinal);

    // -m, -y and -Y; a bundle of short options such as -sm is searched letter by letter, so an
    // attached value holding one of these letters counts too, which only costs the case real time.
    private static readonly char[] ShortTimerOptions = ['m', 'y', 'Y'];

    /// <summary>Whether any argument names a timer option, long or short, alone or in a bundle of short options.</summary>
    /// <param name="arguments">The arguments curl is given.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    public static bool AnyIn(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Any(NamesATimer);
    }

    private static bool NamesATimer(string argument) =>
        argument.StartsWith("--", StringComparison.Ordinal)
            ? LongTimerOptions.Contains(argument)
            : argument.StartsWith('-') && argument.AsSpan(1).IndexOfAny(ShortTimerOptions) >= 0;
}
