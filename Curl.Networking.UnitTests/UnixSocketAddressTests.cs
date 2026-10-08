using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="UnixSocketAddress" /> against curl 8.21.0 (mingw, Schannel), measured on
/// 2026-09-28 (BL-507 Notes): the remote address text cut to 45 characters, empty for an abstract
/// name, and <c>Unix socket path too long</c> from 108 bytes.
/// </summary>
[TestClass]
public sealed class UnixSocketAddressTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("s.sock", false, "s.sock")]
    [DataRow(@"C:\Users\Stewart Rogers\AppData\Local\Temp\bl", false, @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl")]
    [DataRow(@"C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock", false, @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl")]
    [DataRow("abs1", true, "")]
    public void RemoteIpText_IsThePathCutTo45CharactersOrNothingWhenAbstract(string path, bool isAbstract, string expected)
    {
        Diagnostics.Arrange("path", path);
        Diagnostics.Arrange("path length", path.Length);
        Diagnostics.Arrange("is abstract", isAbstract);
        var address = new UnixSocketAddress(path, isAbstract);

        var remoteIpText = address.RemoteIpText;

        Diagnostics.Act("remote IP text", remoteIpText);
        Diagnostics.Assert("remote IP text", expected, remoteIpText);
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
        Diagnostics.Arrange("path length", length);
        Diagnostics.Arrange("is abstract", isAbstract);
        Diagnostics.Arrange("runs on macOS", runsOnMacOs);
        var address = new UnixSocketAddress(new string('a', length), isAbstract);

        var isTooLong = address.IsTooLong(runsOnMacOs);

        Diagnostics.Act("is too long", isTooLong);
        Diagnostics.Assert("is too long", expected, isTooLong);
        Assert.AreEqual(expected, address.IsTooLong(runsOnMacOs));
    }

    [TestMethod]
    public void IsTooLong_CountsUtf8Bytes()
    {
        // 54 two-byte characters are 108 bytes.
        var address = new UnixSocketAddress(new string('é', 54), IsAbstract: false);
        Diagnostics.Arrange("path", "54 x U+00E9, 108 UTF-8 bytes");
        Diagnostics.Arrange("runs on macOS", false);

        var isTooLong = address.IsTooLong(runsOnMacOs: false);

        Diagnostics.Act("is too long", isTooLong);
        Diagnostics.Assert("is too long", true, isTooLong);
        Assert.IsTrue(address.IsTooLong(runsOnMacOs: false));
    }

    [TestMethod]
    public void ToEndPoint_ForAPath_IsThePath()
    {
        Diagnostics.Arrange("path", "/run/app.sock");
        Diagnostics.Arrange("is abstract", false);

        var endPoint = new UnixSocketAddress("/run/app.sock", IsAbstract: false).ToEndPoint();

        Diagnostics.Act("end point", endPoint);
        Diagnostics.Assert("end point", "/run/app.sock", endPoint.ToString());
        Assert.AreEqual("/run/app.sock", endPoint.ToString());
    }

    [TestMethod]
    public void ToEndPoint_ForAnAbstractName_StartsWithANul()
    {
        Diagnostics.Arrange("name", "abs1");
        Diagnostics.Arrange("is abstract", true);

        // UnixDomainSocketEndPoint shows a leading NUL as @, on every platform.
        var endPoint = new UnixSocketAddress("abs1", IsAbstract: true).ToEndPoint();

        Diagnostics.Act("end point", endPoint);
        Diagnostics.Assert("end point", "@abs1", endPoint.ToString());
        Assert.AreEqual("@abs1", endPoint.ToString());
    }
}
