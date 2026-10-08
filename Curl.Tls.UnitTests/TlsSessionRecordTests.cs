using Curl.Testing;

namespace Curl.Tls;

/// <summary>Pins when a <see cref="TlsSessionRecord" /> can be offered and the ticket age it reports (RFC 8446 sections 4.2.11.1 and 4.6.1).</summary>
[TestClass]
public sealed class TlsSessionRecordTests
{
    private static readonly DateTimeOffset Received = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(0.0, true, DisplayName = "the moment it arrives")]
    [DataRow(599.0, true, DisplayName = "within its lifetime")]
    [DataRow(600.0, false, DisplayName = "at the end of its lifetime")]
    [DataRow(-1.0, false, DisplayName = "before it arrived")]
    public void CanResumeWithinItsLifetime(double secondsLater, bool expected)
    {
        Diagnostics.Arrange("lifetime and seconds after receipt", $"600, {secondsLater}");

        bool canResume = Session(600).CanResumeAt(Received.AddSeconds(secondsLater));
        Diagnostics.Act("can resume", canResume);

        Diagnostics.Assert("can resume", expected, canResume);
        Assert.AreEqual(expected, canResume);
    }

    [TestMethod]
    public void LifetimeIsCappedAtSevenDays()
    {
        TlsSessionRecord session = Session(10 * 24 * 3600);
        Diagnostics.Arrange("lifetime", "10 days");

        bool justBefore = session.CanResumeAt(Received.AddDays(7).AddSeconds(-1));
        bool atSevenDays = session.CanResumeAt(Received.AddDays(7));
        Diagnostics.Act("can resume one second before and at 7 days", $"{justBefore}, {atSevenDays}");

        Diagnostics.Assert("can resume at 7 days", false, atSevenDays);
        Assert.IsTrue(justBefore);
        Assert.IsFalse(atSevenDays);
    }

    [TestMethod]
    public void Tls12SessionCannotResumeATls13Handshake()
    {
        TlsSessionRecord session = Session(600) with { Version = 0x0303 };
        Diagnostics.Arrange("session version", "0x0303");

        bool canResume = session.CanResumeAt(Received);
        Diagnostics.Act("can resume", canResume);

        Diagnostics.Assert("can resume", false, canResume);
        Assert.IsFalse(canResume);
    }

    [TestMethod]
    public void ObfuscatedTicketAgeAddsTheAgeAddModulo32Bits()
    {
        TlsSessionRecord session = Session(600) with { TicketAgeAdd = uint.MaxValue - 5 };
        Diagnostics.Arrange("ticket age add and age", "uint.MaxValue - 5, 10 ms");

        uint obfuscated = session.ObfuscatedTicketAgeAt(Received.AddMilliseconds(10));
        Diagnostics.Act("obfuscated ticket age", obfuscated);

        Diagnostics.Assert("obfuscated ticket age", 4u, obfuscated);
        Assert.AreEqual(4u, obfuscated);
    }

    private static TlsSessionRecord Session(uint lifetime) => new(0x0304, 0x1301, [], [1], [2], lifetime, 0, 0, Received);
}
