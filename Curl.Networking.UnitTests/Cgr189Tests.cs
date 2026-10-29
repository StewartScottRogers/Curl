using System.Net;

namespace Curl.Networking;

/// <summary>
/// CGR-189 of the third-party gap report (BL-3718): on Windows a refused connect to a loopback address
/// other than <c>127.0.0.1</c> and <c>::1</c> takes about two seconds in curl, so only those two are
/// connected with SYN retransmissions stopped.
/// </summary>
[TestClass]
public sealed class Cgr189Tests
{
    [TestMethod]
    [DataRow("127.0.0.1", true)]
    [DataRow("::1", true)]
    [DataRow("127.0.0.2", false)]
    [DataRow("127.1.2.3", false)]
    [DataRow("10.0.0.1", false)]
    public void RefusesAtOnceOnWindows_Address_OnlyTheTwoPlainLoopbacksDo(string address, bool expected)
    {
        var result = TcpDialer.RefusesAtOnceOnWindows(IPAddress.Parse(address));

        Assert.AreEqual(expected, result);
    }
}
