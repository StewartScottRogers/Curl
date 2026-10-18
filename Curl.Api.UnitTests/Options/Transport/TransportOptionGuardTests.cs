using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;

namespace Curl.Api.UnitTests.Options.Transport;

[TestClass]
public sealed class TransportOptionGuardTests
{
    [TestMethod]
    public void NonNegative_AcceptsZeroAndNamesTheMemberWhenNegative()
    {
        Assert.AreEqual(TimeSpan.Zero, TransportOptionGuard.NonNegative(TimeSpan.Zero, "Member"));

        ArgumentOutOfRangeException error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => TransportOptionGuard.NonNegative(TimeSpan.FromTicks(-1), "Member"));

        Assert.AreEqual("Member", error.ParamName);
    }

    [TestMethod]
    public void NonNegativeOrInfinite_AcceptsInfiniteAndRejectsOtherNegatives()
    {
        Assert.AreEqual(Timeout.InfiniteTimeSpan, TransportOptionGuard.NonNegativeOrInfinite(Timeout.InfiniteTimeSpan, "Member"));
        Assert.AreEqual(TimeSpan.FromSeconds(1), TransportOptionGuard.NonNegativeOrInfinite(TimeSpan.FromSeconds(1), "Member"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TransportOptionGuard.NonNegativeOrInfinite(TimeSpan.FromSeconds(-2), "Member"));
    }

    [TestMethod]
    public void InRange_AcceptsBothEndsAndRejectsBeyondThem()
    {
        Assert.AreEqual(0, TransportOptionGuard.InRange(0, 0, 7, "Member"));
        Assert.AreEqual(7, TransportOptionGuard.InRange(7, 0, 7, "Member"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TransportOptionGuard.InRange(-1, 0, 7, "Member"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TransportOptionGuard.InRange(8, 0, 7, "Member"));
    }

    [TestMethod]
    public void Port_AcceptsZeroTo65535()
    {
        Assert.AreEqual(65535, TransportOptionGuard.Port(65535, "Member"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TransportOptionGuard.Port(65536, "Member"));
    }

    [TestMethod]
    public void Defined_RejectsAnUnnamedValueAndNamesTheMember()
    {
        Assert.AreEqual(SslVersion.Tls13, TransportOptionGuard.Defined(SslVersion.Tls13, "Member"));

        ArgumentOutOfRangeException error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => TransportOptionGuard.Defined((SslVersion)42, "Member"));

        Assert.AreEqual("Member", error.ParamName);
        Assert.AreEqual((SslVersion)42, error.ActualValue);
    }

    [TestMethod]
    public void Flags_AcceptEveryDefinedBitAndRejectAnUndefinedOne()
    {
        SslOptions allSsl = SslOptions.AllowBeast | SslOptions.NoRevoke | SslOptions.NoPartialChain | SslOptions.RevokeBestEffort
            | SslOptions.NativeCa | SslOptions.AutoClientCert | SslOptions.EarlyData;
        Socks5Authentication allSocks = Socks5Authentication.Basic | Socks5Authentication.GssApi;

        Assert.AreEqual(allSsl, TransportOptionGuard.Flags(allSsl, "Member"));
        Assert.AreEqual(allSocks, TransportOptionGuard.Flags(allSocks, "Member"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TransportOptionGuard.Flags(SslOptions.EarlyData | (SslOptions)(1 << 8), "Member"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TransportOptionGuard.Flags((Socks5Authentication)(1 << 1), "Member"));
    }

    [TestMethod]
    public void NotNull_ReturnsTheValueOrThrowsNamingTheMember()
    {
        string[] value = [];

        Assert.AreSame(value, TransportOptionGuard.NotNull(value, "Member"));
        Assert.AreEqual("Member", Assert.ThrowsExactly<ArgumentNullException>(() => TransportOptionGuard.NotNull<string[]>(null!, "Member")).ParamName);
    }
}
