namespace Curl.Conformance.Extended.UnitTests.GoogleCloudStorage.Signing;

/// <summary>A clock that always reads one instant, so a signed request's <c>x-goog-date</c> is fixed; its timers are the system's.</summary>
/// <param name="now">The instant it reads.</param>
internal sealed class GcsFixedClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>The instant the signing cases run at: 2026-11-02 10:30:00 UTC.</summary>
    public static readonly DateTimeOffset CaseTime = new(2026, 11, 2, 10, 30, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
