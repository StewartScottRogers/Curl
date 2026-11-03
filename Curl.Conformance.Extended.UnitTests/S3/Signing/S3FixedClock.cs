namespace Curl.Conformance.Extended.UnitTests.S3.Signing;

/// <summary>A clock that always reads one instant, so a signed request's <c>x-amz-date</c> is fixed; its timers are the system's.</summary>
/// <param name="now">The instant it reads.</param>
internal sealed class S3FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>The instant of the API reference's examples: 2013-05-24 00:00:00 UTC.</summary>
    public static readonly DateTimeOffset ExampleTime = new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
