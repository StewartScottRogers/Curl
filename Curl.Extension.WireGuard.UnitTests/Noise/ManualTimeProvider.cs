namespace Curl.Extension.WireGuard.UnitTests.Noise;

/// <summary>A clock the test moves by hand; its timestamps are ticks of the wall clock.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => now;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => now.UtcTicks;

    public void Advance(TimeSpan by) => now += by;
}
