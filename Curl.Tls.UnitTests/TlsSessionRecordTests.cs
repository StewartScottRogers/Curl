namespace Curl.Tls;

/// <summary>Pins when a <see cref="TlsSessionRecord" /> can be offered and the ticket age it reports (RFC 8446 sections 4.2.11.1 and 4.6.1).</summary>
[TestClass]
public sealed class TlsSessionRecordTests
{
    private static readonly DateTimeOffset Received = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(0.0, true, DisplayName = "the moment it arrives")]
    [DataRow(599.0, true, DisplayName = "within its lifetime")]
    [DataRow(600.0, false, DisplayName = "at the end of its lifetime")]
    [DataRow(-1.0, false, DisplayName = "before it arrived")]
    public void CanResumeWithinItsLifetime(double secondsLater, bool expected) =>
        Assert.AreEqual(expected, Session(600).CanResumeAt(Received.AddSeconds(secondsLater)));

    [TestMethod]
    public void LifetimeIsCappedAtSevenDays()
    {
        TlsSessionRecord session = Session(10 * 24 * 3600);

        Assert.IsTrue(session.CanResumeAt(Received.AddDays(7).AddSeconds(-1)));
        Assert.IsFalse(session.CanResumeAt(Received.AddDays(7)));
    }

    [TestMethod]
    public void Tls12SessionCannotResumeATls13Handshake() =>
        Assert.IsFalse((Session(600) with { Version = 0x0303 }).CanResumeAt(Received));

    [TestMethod]
    public void ObfuscatedTicketAgeAddsTheAgeAddModulo32Bits() =>
        Assert.AreEqual(4u, (Session(600) with { TicketAgeAdd = uint.MaxValue - 5 }).ObfuscatedTicketAgeAt(Received.AddMilliseconds(10)));

    private static TlsSessionRecord Session(uint lifetime) => new(0x0304, 0x1301, [], [1], [2], lifetime, 0, 0, Received);
}
