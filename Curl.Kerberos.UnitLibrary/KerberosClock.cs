namespace Curl.Kerberos;

/// <summary>Reads the time as Kerberos messages carry it: to the second, with the microseconds past it apart.</summary>
internal static class KerberosClock
{
    /// <summary>Gives the time to the second and the microseconds past it, as timestamps and authenticators carry them.</summary>
    /// <param name="timeProvider">The clock.</param>
    /// <returns>The time without its fraction, and the fraction in microseconds.</returns>
    public static (DateTimeOffset Time, int Microseconds) Now(TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        long subsecond = now.UtcTicks % TimeSpan.TicksPerSecond;
        return (new DateTimeOffset(now.UtcTicks - subsecond, TimeSpan.Zero), (int)(subsecond / TimeSpan.TicksPerMicrosecond));
    }
}
