namespace Curl.Output;

/// <summary>A clock that always reads one instant, in a fixed local time zone.</summary>
/// <param name="utcNow">The instant <see cref="GetUtcNow"/> returns.</param>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    /// <summary>The standard name of <see cref="LocalTimeZone"/>, as the reference machine's Windows reported it.</summary>
    public const string TimeZoneStandardName = "US Mountain Standard Time";

    private static readonly TimeZoneInfo MountainStandardTime =
        TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(-7), "Test", TimeZoneStandardName);

    public override TimeZoneInfo LocalTimeZone => MountainStandardTime;

    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }
}
