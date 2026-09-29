namespace Curl.Networking;

/// <summary>
/// Pins <see cref="UnixSocketAddress" /> against curl 8.21.0 (mingw, Schannel), measured on
/// 2026-09-28 (BL-507 Notes): the remote address text cut to 45 characters, empty for an abstract
/// name, and <c>Unix socket path too long</c> from 108 bytes.
/// </summary>
[TestClass]
public sealed class UnixSocketAddressTests
{
    [TestMethod]
    [DataRow("s.sock", false, "s.sock")]
    [DataRow(@"C:\Users\Stewart Rogers\AppData\Local\Temp\bl", false, @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl")]
    [DataRow(@"C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock", false, @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl")]
    [DataRow("abs1", true, "")]
    public void RemoteIpText_IsThePathCutTo45CharactersOrNothingWhenAbstract(string path, bool isAbstract, string expected)
    {
        var address = new UnixSocketAddress(path, isAbstract);

        Assert.AreEqual(expected, address.RemoteIpText);
    }

    [TestMethod]
    [DataRow(107, false, false, false)]
    [DataRow(108, false, false, true)]
    [DataRow(107, true, false, false)]
    [DataRow(108, true, false, true)]
    [DataRow(103, false, true, false)]
    [DataRow(104, false, true, true)]
    public void IsTooLong_RefusesAPathWhoseBytesAndOneMoreDoNotFitSunPath(int length, bool isAbstract, bool runsOnMacOs, bool expected)
    {
        var address = new UnixSocketAddress(new string('a', length), isAbstract);

        Assert.AreEqual(expected, address.IsTooLong(runsOnMacOs));
    }

    [TestMethod]
    public void IsTooLong_CountsUtf8Bytes()
    {
        // 54 two-byte characters are 108 bytes.
        var address = new UnixSocketAddress(new string('é', 54), IsAbstract: false);

        Assert.IsTrue(address.IsTooLong(runsOnMacOs: false));
    }

    [TestMethod]
    public void ToEndPoint_ForAPath_IsThePath()
    {
        var endPoint = new UnixSocketAddress("/run/app.sock", IsAbstract: false).ToEndPoint();

        Assert.AreEqual("/run/app.sock", endPoint.ToString());
    }

    [TestMethod]
    public void ToEndPoint_ForAnAbstractName_StartsWithANul()
    {
        // UnixDomainSocketEndPoint shows a leading NUL as @, on every platform.
        var endPoint = new UnixSocketAddress("abs1", IsAbstract: true).ToEndPoint();

        Assert.AreEqual("@abs1", endPoint.ToString());
    }
}
