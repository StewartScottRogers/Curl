using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that every <see cref="TransferReport" /> member defaults to "not known" and
/// carries what a handler or follower sets.
/// </summary>
[TestClass]
public sealed class TransferReportTests
{
    [TestMethod]
    public void New_WithNoMembersSet_ReportsNothingKnown()
    {
        var report = new TransferReport();

        Assert.AreEqual(0, report.ResponseCode);
        Assert.AreEqual(0, report.ProxyConnectResponseCode);
        Assert.IsFalse(report.UsedProxy);
        Assert.IsNull(report.HttpVersion);
        Assert.IsNull(report.Method);
        Assert.IsEmpty(report.ResponseHeaders);
        Assert.IsEmpty(report.PseudoHeaders);
        Assert.IsNull(report.ContentType);
        Assert.IsNull(report.RedirectUrl);
        Assert.IsNull(report.EffectiveUrl);
        Assert.AreEqual(0, report.RedirectCount);
        Assert.AreEqual(0L, report.HeaderSize);
        Assert.AreEqual(0L, report.RequestSize);
        Assert.AreEqual(0L, report.DownloadSize);
        Assert.AreEqual(0L, report.UploadSize);
        Assert.AreEqual(0, report.ConnectionCount);
        Assert.IsNull(report.LocalEndPoint);
        Assert.IsNull(report.RemoteEndPoint);
        Assert.IsNull(report.Timings);
        Assert.IsEmpty(report.PeerCertificates);
    }

    [TestMethod]
    public void New_WithPeerCertificates_CarriesThem()
    {
        ReadOnlyMemory<byte>[] certificates = [new byte[] { 0x30, 0x00 }];

        var report = new TransferReport { PeerCertificates = certificates };

        Assert.AreSame(certificates, report.PeerCertificates);
    }

    [TestMethod]
    public void New_WithEveryMemberSet_CarriesEachOne()
    {
        KeyValuePair<string, string>[] headers =
        [
            new("Set-Cookie", "a=1"),
            new("Set-Cookie", "b=2"),
        ];
        KeyValuePair<string, string>[] pseudoHeaders = [new("Accept-ranges", "bytes")];
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
        var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var timings = new TransferTimings(0, null, 1, 2, 3, 4);

        var report = new TransferReport
        {
            ResponseCode = 302,
            ProxyConnectResponseCode = 200,
            UsedProxy = true,
            HttpVersion = new Version(1, 1),
            Method = "POST",
            ResponseHeaders = headers,
            PseudoHeaders = pseudoHeaders,
            ContentType = "text/html",
            RedirectUrl = "http://example.com/next",
            EffectiveUrl = "http://example.com/",
            RedirectCount = 2,
            HeaderSize = 120,
            RequestSize = 80,
            DownloadSize = 1000,
            UploadSize = 10,
            ConnectionCount = 1,
            LocalEndPoint = localEndPoint,
            RemoteEndPoint = remoteEndPoint,
            Timings = timings,
        };

        Assert.AreEqual(302, report.ResponseCode);
        Assert.AreEqual(200, report.ProxyConnectResponseCode);
        Assert.IsTrue(report.UsedProxy);
        Assert.AreEqual(new Version(1, 1), report.HttpVersion);
        Assert.AreEqual("POST", report.Method);
        Assert.AreSame(headers, report.ResponseHeaders);
        Assert.AreSame(pseudoHeaders, report.PseudoHeaders);
        Assert.AreEqual("text/html", report.ContentType);
        Assert.AreEqual("http://example.com/next", report.RedirectUrl);
        Assert.AreEqual("http://example.com/", report.EffectiveUrl);
        Assert.AreEqual(2, report.RedirectCount);
        Assert.AreEqual(120L, report.HeaderSize);
        Assert.AreEqual(80L, report.RequestSize);
        Assert.AreEqual(1000L, report.DownloadSize);
        Assert.AreEqual(10L, report.UploadSize);
        Assert.AreEqual(1, report.ConnectionCount);
        Assert.AreSame(localEndPoint, report.LocalEndPoint);
        Assert.AreSame(remoteEndPoint, report.RemoteEndPoint);
        Assert.AreSame(timings, report.Timings);
    }

    [TestMethod]
    public void With_SettingEffectiveUrlAndRedirectCount_KeepsTheHandlersMembers()
    {
        var fromHandler = new TransferReport { ResponseCode = 200, Method = "GET" };

        var fromFollower = fromHandler with { EffectiveUrl = "http://example.com/last", RedirectCount = 1 };

        Assert.AreEqual(200, fromFollower.ResponseCode);
        Assert.AreEqual("GET", fromFollower.Method);
        Assert.AreEqual("http://example.com/last", fromFollower.EffectiveUrl);
        Assert.AreEqual(1, fromFollower.RedirectCount);
    }
}
