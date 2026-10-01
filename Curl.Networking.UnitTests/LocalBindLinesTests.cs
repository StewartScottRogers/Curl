using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="LocalBindLines" />: curl's <c>-v</c> bind lines, measured with curl 8.21.0 on Windows
/// and curl 8.18.0 on Linux (BL-1027 Notes). Each platform's numbers are pinned through the flags, so
/// every answer is checked on every platform.
/// </summary>
[TestClass]
public sealed class LocalBindLinesTests
{
    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, true, false, 2, DisplayName = "IPv4 on Windows")]
    [DataRow(AddressFamily.InterNetwork, false, true, 2, DisplayName = "IPv4 on Linux")]
    [DataRow(AddressFamily.InterNetworkV6, true, false, 23, DisplayName = "IPv6 on Windows")]
    [DataRow(AddressFamily.InterNetworkV6, false, true, 10, DisplayName = "IPv6 on Linux")]
    [DataRow(AddressFamily.InterNetworkV6, false, false, 30, DisplayName = "IPv6 on macOS")]
    public void FamilyNumber_GivesThePlatformsAfNumber(AddressFamily family, bool onWindows, bool onLinux, int expected)
    {
        var number = LocalBindLines.FamilyNumber(family, onWindows, onLinux);

        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    public void NameResolved_NamesTheHostAndBothFamilies()
    {
        // curl 8.21.0 Windows, --interface 127.0.0.1 to localhost's ::1.
        var line = LocalBindLines.NameResolved("127.0.0.1", AddressFamily.InterNetworkV6, IPAddress.Loopback, onWindows: true, onLinux: false);

        Assert.AreEqual("Name '127.0.0.1' family 23 resolved to '127.0.0.1' family 2", line);
    }

    [TestMethod]
    public void CouldNotResolveHost_NamesTheHost()
    {
        Assert.AreEqual("Could not resolve host: bogus0", LocalBindLines.CouldNotResolveHost("bogus0"));
    }

    [TestMethod]
    [DataRow(true, "Could not bind to 'bogus0' with errno 0: No error", DisplayName = "Windows")]
    [DataRow(false, "Could not bind to 'bogus0' with errno 22: Invalid argument", DisplayName = "Linux")]
    public void CouldNotBindHost_GivesThePlatformsErrno(bool onWindows, string expected)
    {
        Assert.AreEqual(expected, LocalBindLines.CouldNotBindHost("bogus0", onWindows));
    }

    [TestMethod]
    [DataRow(true, "Could not bind to interface 'Ethernet' with errno 0: No error", DisplayName = "Windows")]
    [DataRow(false, "Could not bind to interface 'Ethernet' with errno 19: No such device", DisplayName = "Linux")]
    public void CouldNotBindInterface_GivesThePlatformsErrno(bool onWindows, string expected)
    {
        Assert.AreEqual(expected, LocalBindLines.CouldNotBindInterface("Ethernet", onWindows));
    }

    [TestMethod]
    public void DeviceBound_NamesTheDevice()
    {
        Assert.AreEqual("socket successfully bound to interface 'lo'", LocalBindLines.DeviceBound("lo"));
    }

    [TestMethod]
    public void LocalPort_NamesThePort()
    {
        Assert.AreEqual("Local port: 40010", LocalBindLines.LocalPort(40010));
    }

    [TestMethod]
    public void PortFailedTryingNext_NamesThePort()
    {
        Assert.AreEqual("Bind to local port 40000 failed, trying next", LocalBindLines.PortFailedTryingNext(40000));
    }

    [TestMethod]
    public void BindFailed_OnWindows_GivesTheErrnoAndCurlsWords()
    {
        var exception = new SocketException((int)SocketError.AddressAlreadyInUse);

        var line = LocalBindLines.BindFailed(exception, onWindows: true);

        Assert.AreEqual($"bind failed with errno {exception.NativeErrorCode}: Address already in use", line);
    }

    [TestMethod]
    public void BindFailed_OffWindows_GivesTheErrnoAndTheSystemsWords()
    {
        var exception = new SocketException((int)SocketError.AddressAlreadyInUse);

        var line = LocalBindLines.BindFailed(exception, onWindows: false);

        Assert.AreEqual($"bind failed with errno {exception.NativeErrorCode}: {exception.Message}", line);
    }
}
