using System.Globalization;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// Whether each <c>--alt-svc</c> cache learns and looks up over plain <c>http</c>: only behind the
    /// harness seam, and then when <c>CURL_ALTSVC_HTTP</c> is set at all, as curl's debug build tests it
    /// with <c>getenv</c> (ADR-0497, BL-2263).
    /// </summary>
    /// <param name="honoursAltSvcHttpVariable">Whether the seam is on.</param>
    /// <param name="environment">Reads the run's environment.</param>
    /// <returns>Whether the caches serve <c>http</c> origins.</returns>
    internal static bool UsesAltSvcOverHttp(bool honoursAltSvcHttpVariable, Func<string, string?> environment) =>
        honoursAltSvcHttpVariable && environment(AltSvcHttpVariableName) is not null;

    /// <summary>The variable whose presence makes curl's debug build use Alt-Svc over plain <c>http</c>.</summary>
    internal const string AltSvcHttpVariableName = "CURL_ALTSVC_HTTP";

    /// <summary>
    /// Whether the run's HSTS cache learns over plain <c>http</c>: only behind the harness seam, and
    /// then when <c>CURL_HSTS_HTTP</c> is set at all, as curl's debug build tests it with <c>getenv</c>
    /// (ADR-0494, BL-2250).
    /// </summary>
    /// <param name="honoursHstsHttpVariable">Whether the seam is on.</param>
    /// <param name="environment">Reads the run's environment.</param>
    /// <returns>Whether the cache learns over <c>http</c>.</returns>
    internal static bool LearnsHstsOverHttp(bool honoursHstsHttpVariable, Func<string, string?> environment) =>
        honoursHstsHttpVariable && environment(HstsHttpVariableName) is not null;

    /// <summary>The variable curl's debug build reads its HSTS clock from, in whole seconds since the epoch.</summary>
    internal const string HstsTimeVariableName = "CURL_TIME";

    /// <summary>The variable whose presence makes curl's debug build learn HSTS over plain <c>http</c>.</summary>
    internal const string HstsHttpVariableName = "CURL_HSTS_HTTP";

    /// <summary>
    /// The HSTS clock a <c>CURL_TIME</c> value fixes: a <see cref="FixedWallClock" /> at that many
    /// seconds after the epoch, or <see langword="null" /> (the run's own clock) when the value is
    /// not set or is not a whole number of seconds curl's dates can hold (ADR-0493, BL-2234).
    /// </summary>
    /// <param name="value">The variable's value, or <see langword="null" /> when not set.</param>
    /// <returns>The clock, or <see langword="null" />.</returns>
    internal static FixedWallClock? HstsClockOf(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) && seconds <= MaximumHstsTimeSeconds
            ? new FixedWallClock(DateTimeOffset.FromUnixTimeSeconds(seconds))
            : null;

    /// <summary>The last second <see cref="DateTimeOffset" /> holds: 9999-12-31T23:59:59Z.</summary>
    private const long MaximumHstsTimeSeconds = 253402300799;
}
