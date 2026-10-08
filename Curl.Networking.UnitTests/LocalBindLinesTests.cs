using System.Net;
using System.Net.Sockets;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="LocalBindLines" />: curl's <c>-v</c> bind lines, measured with curl 8.21.0 on Windows
/// and curl 8.18.0 on Linux (BL-1027 Notes). Each platform's numbers are pinned through the flags, so
/// every answer is checked on every platform.
/// </summary>
[TestClass]
public sealed class LocalBindLinesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, true, false, 2, DisplayName = "IPv4 on Windows")]
    [DataRow(AddressFamily.InterNetwork, false, true, 2, DisplayName = "IPv4 on Linux")]
    [DataRow(AddressFamily.InterNetworkV6, true, false, 23, DisplayName = "IPv6 on Windows")]
    [DataRow(AddressFamily.InterNetworkV6, false, true, 10, DisplayName = "IPv6 on Linux")]
    [DataRow(AddressFamily.InterNetworkV6, false, false, 30, DisplayName = "IPv6 on macOS")]
    public void FamilyNumber_GivesThePlatformsAfNumber(AddressFamily family, bool onWindows, bool onLinux, int expected)
    {
        Diagnostics.Arrange("family", family);
        ArrangePlatform(onWindows, onLinux);

        var number = LocalBindLines.FamilyNumber(family, onWindows, onLinux);

        Diagnostics.Act("family number", number);
        Diagnostics.Assert("family number", expected, number);
        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    public void NameResolved_NamesTheHostAndBothFamilies()
    {
        // curl 8.21.0 Windows, --interface 127.0.0.1 to localhost's ::1.
        Diagnostics.Arrange("name", "127.0.0.1");
        Diagnostics.Arrange("target family", AddressFamily.InterNetworkV6);
        Diagnostics.Arrange("resolved address", IPAddress.Loopback);
        ArrangePlatform(onWindows: true, onLinux: false);

        var line = LocalBindLines.NameResolved("127.0.0.1", AddressFamily.InterNetworkV6, IPAddress.Loopback, onWindows: true, onLinux: false);

        WriteLine("Name '127.0.0.1' family 23 resolved to '127.0.0.1' family 2", line);
        Assert.AreEqual("Name '127.0.0.1' family 23 resolved to '127.0.0.1' family 2", line);
    }

    [TestMethod]
    public void LocalInterface_NamesTheInterfaceItsAddressAndThePlatformsFamilyNumber()
    {
        // curl 8.22.0 Linux, --interface lo http://[::1]:1/ with SO_BINDTODEVICE refused (BL-1079).
        Diagnostics.Arrange("interface", "lo");
        Diagnostics.Arrange("address", IPAddress.IPv6Loopback);
        ArrangePlatform(onWindows: false, onLinux: true);

        var line = LocalBindLines.LocalInterface("lo", IPAddress.IPv6Loopback, onWindows: false, onLinux: true);

        WriteLine("Local Interface lo is ip ::1 using address family 10", line);
        Assert.AreEqual("Local Interface lo is ip ::1 using address family 10", line);
    }

    [TestMethod]
    public void CouldNotResolveHost_NamesTheHost()
    {
        Diagnostics.Arrange("host", "bogus0");

        var line = LocalBindLines.CouldNotResolveHost("bogus0");

        WriteLine("Could not resolve host: bogus0", line);
        Assert.AreEqual("Could not resolve host: bogus0", LocalBindLines.CouldNotResolveHost("bogus0"));
    }

    [TestMethod]
    [DataRow(true, "Could not bind to 'bogus0' with errno 0: No error", DisplayName = "Windows")]
    [DataRow(false, "Could not bind to 'bogus0' with errno 22: Invalid argument", DisplayName = "Linux")]
    public void CouldNotBindHost_GivesThePlatformsErrno(bool onWindows, string expected)
    {
        Diagnostics.Arrange("host", "bogus0");
        Diagnostics.Arrange("on Windows", onWindows);

        var line = LocalBindLines.CouldNotBindHost("bogus0", onWindows);

        WriteLine(expected, line);
        Assert.AreEqual(expected, LocalBindLines.CouldNotBindHost("bogus0", onWindows));
    }

    [TestMethod]
    [DataRow(true, "Could not bind to interface 'Ethernet' with errno 0: No error", DisplayName = "Windows")]
    [DataRow(false, "Could not bind to interface 'Ethernet' with errno 19: No such device", DisplayName = "Linux")]
    public void CouldNotBindInterface_GivesThePlatformsErrno(bool onWindows, string expected)
    {
        Diagnostics.Arrange("interface", "Ethernet");
        Diagnostics.Arrange("on Windows", onWindows);

        var line = LocalBindLines.CouldNotBindInterface("Ethernet", onWindows);

        WriteLine(expected, line);
        Assert.AreEqual(expected, LocalBindLines.CouldNotBindInterface("Ethernet", onWindows));
    }

    [TestMethod]
    public void DeviceBound_NamesTheDevice()
    {
        Diagnostics.Arrange("device", "lo");

        var line = LocalBindLines.DeviceBound("lo");

        WriteLine("socket successfully bound to interface 'lo'", line);
        Assert.AreEqual("socket successfully bound to interface 'lo'", LocalBindLines.DeviceBound("lo"));
    }

    [TestMethod]
    public void LocalPort_NamesThePort()
    {
        Diagnostics.Arrange("port", 40010);

        var line = LocalBindLines.LocalPort(40010);

        WriteLine("Local port: 40010", line);
        Assert.AreEqual("Local port: 40010", LocalBindLines.LocalPort(40010));
    }

    [TestMethod]
    public void PortFailedTryingNext_NamesThePort()
    {
        Diagnostics.Arrange("port", 40000);

        var line = LocalBindLines.PortFailedTryingNext(40000);

        WriteLine("Bind to local port 40000 failed, trying next", line);
        Assert.AreEqual("Bind to local port 40000 failed, trying next", LocalBindLines.PortFailedTryingNext(40000));
    }

    [TestMethod]
    public void BindFailed_OnWindows_GivesTheErrnoAndCurlsWords()
    {
        var exception = new SocketException((int)SocketError.AddressAlreadyInUse);
        Diagnostics.Arrange("socket error", SocketError.AddressAlreadyInUse);
        Diagnostics.Arrange("on Windows", true);

        var line = LocalBindLines.BindFailed(exception, onWindows: true);

        // The errno is the platform's own number, so the lines show it as <errno>.
        WriteLine("bind failed with errno <errno>: Address already in use", line.Replace($"errno {exception.NativeErrorCode}:", "errno <errno>:", StringComparison.Ordinal));
        Assert.AreEqual($"bind failed with errno {exception.NativeErrorCode}: Address already in use", line);
    }

    [TestMethod]
    public void BindFailed_OffWindows_GivesTheErrnoAndTheSystemsWords()
    {
        var exception = new SocketException((int)SocketError.AddressAlreadyInUse);
        Diagnostics.Arrange("socket error", SocketError.AddressAlreadyInUse);
        Diagnostics.Arrange("on Windows", false);

        var line = LocalBindLines.BindFailed(exception, onWindows: false);

        // The errno and the system's words are the platform's own, so the lines show them as placeholders.
        WriteLine(
            "bind failed with errno <errno>: <system message>",
            line.Replace($"errno {exception.NativeErrorCode}:", "errno <errno>:", StringComparison.Ordinal).Replace(exception.Message, "<system message>", StringComparison.Ordinal));
        Assert.AreEqual($"bind failed with errno {exception.NativeErrorCode}: {exception.Message}", line);
    }

    private void ArrangePlatform(bool onWindows, bool onLinux)
    {
        Diagnostics.Arrange("on Windows", onWindows);
        Diagnostics.Arrange("on Linux", onLinux);
    }

    private void WriteLine(string expected, string line)
    {
        Diagnostics.Act("line", line);
        Diagnostics.Diff("line", expected, line);
    }
}
