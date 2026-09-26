namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the host and port a <see cref="ConnectTarget" /> accepts, at both ends of the
/// port range.
/// </summary>
[TestClass]
public sealed class ConnectTargetTests
{
    [TestMethod]
    public void Constructor_WithValidValues_RoundTripsEveryValue()
    {
        var target = new ConnectTarget("example.com", 443, true);

        Assert.AreEqual("example.com", target.Host);
        Assert.AreEqual(443, target.Port);
        Assert.IsTrue(target.UseTls);
    }

    [TestMethod]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        string? host = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new ConnectTarget(host!, 80, false));

        Assert.AreEqual("Host", exception.ParamName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t")]
    public void Constructor_WithEmptyOrWhitespaceHost_ThrowsArgumentException(string host)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new ConnectTarget(host, 80, false));

        Assert.AreEqual("Host", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Constructor_WithPortOutsideRange_ThrowsArgumentOutOfRangeException(int port)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ConnectTarget("example.com", port, false));

        Assert.AreEqual("Port", exception.ParamName);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(65535)]
    public void Constructor_WithPortAtRangeBound_Accepts(int port)
    {
        var target = new ConnectTarget("example.com", port, false);

        Assert.AreEqual(port, target.Port);
    }

    [TestMethod]
    public void Equals_ForTwoTargetsBuiltTheSameWay_ReturnsTrue()
    {
        var first = new ConnectTarget("example.com", 21, false);
        var second = new ConnectTarget("example.com", 21, false);

        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void With_ChangingUseTls_KeepsHostAndPort()
    {
        var plain = new ConnectTarget("example.com", 21, false);

        var secure = plain with { UseTls = true };

        Assert.AreEqual("example.com", secure.Host);
        Assert.AreEqual(21, secure.Port);
        Assert.IsTrue(secure.UseTls);
    }
}
