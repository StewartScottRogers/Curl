namespace Curl.Api.Info;

[TestClass]
public sealed class TransferInfoTests
{
    private static readonly TransferInfo Defaults = new();

    [TestMethod]
    public void New_TransferInfo_AnswersAnEmptyUrlAndAddressesAndTheGetMethod()
    {
        Assert.AreEqual("", Defaults.EffectiveUrl);
        Assert.AreEqual("GET", Defaults.EffectiveMethod);
        Assert.AreEqual("", Defaults.PrimaryIp);
        Assert.AreEqual("", Defaults.LocalIp);
    }

    [TestMethod]
    public void New_TransferInfo_LeavesEveryOtherStringNull()
    {
        Assert.IsNull(Defaults.RedirectUrl);
        Assert.IsNull(Defaults.ContentType);
        Assert.IsNull(Defaults.FtpEntryPath);
        Assert.IsNull(Defaults.RtspSessionId);
        Assert.IsNull(Defaults.Scheme);
        Assert.IsNull(Defaults.Referer);
        Assert.IsNull(Defaults.CaInfo);
        Assert.IsNull(Defaults.CaPath);
    }

    [TestMethod]
    public void New_TransferInfo_AnswersZeroForEveryCountCodeAndBitmask()
    {
        long[] zeros =
        [
            Defaults.ResponseCode, Defaults.HttpConnectCode, Defaults.HttpVersion, Defaults.RedirectCount,
            Defaults.HeaderSize, Defaults.RequestSize, Defaults.SslVerifyResult, Defaults.ProxySslVerifyResult,
            Defaults.HttpAuthAvailable, Defaults.ProxyAuthAvailable, Defaults.HttpAuthUsed, Defaults.ProxyAuthUsed,
            Defaults.OsErrno, Defaults.NumConnects, Defaults.PrimaryPort, Defaults.LocalPort,
            Defaults.ConditionUnmet, Defaults.RtspClientCSeq, Defaults.RtspServerCSeq, Defaults.RtspCSeqReceived,
            Defaults.Protocol, Defaults.ProxyError, Defaults.UsedProxy, Defaults.EarlyDataSent,
            Defaults.SizeUpload, Defaults.SizeDownload, Defaults.SpeedUpload, Defaults.SpeedDownload,
        ];

        CollectionAssert.AreEqual(new long[zeros.Length], zeros);
    }

    [TestMethod]
    public void New_TransferInfo_AnswersMinusOneForTheUnknownFileTimeLengthsIdsAndSockets()
    {
        Assert.AreEqual(-1L, Defaults.FileTime);
        Assert.AreEqual(-1L, Defaults.ContentLengthDownload);
        Assert.AreEqual(-1L, Defaults.ContentLengthUpload);
        Assert.AreEqual(-1L, Defaults.TransferId);
        Assert.AreEqual(-1L, Defaults.ConnectionId);
        Assert.AreEqual(-1L, Defaults.LastSocket);
        Assert.AreEqual(-1L, Defaults.ActiveSocket);
    }

    [TestMethod]
    public void New_TransferInfo_AnswersZeroForEveryTime()
    {
        TimeSpan[] times =
        [
            Defaults.TotalTime, Defaults.NameLookupTime, Defaults.ConnectTime, Defaults.AppConnectTime,
            Defaults.PreTransferTime, Defaults.PostTransferTime, Defaults.StartTransferTime,
            Defaults.RedirectTime, Defaults.QueueTime, Defaults.RetryAfter,
        ];

        CollectionAssert.AreEqual(new TimeSpan[times.Length], times);
    }

    [TestMethod]
    public void New_TransferInfo_AnswersEmptyEngineCookieAndCertificateLists()
    {
        Assert.AreEqual(0, Defaults.SslEngines.Count);
        Assert.AreEqual(0, Defaults.CookieList.Count);
        Assert.AreEqual(0, Defaults.CertInfo.Count);
    }

    [TestMethod]
    public void New_TransferInfo_AnswersTheSchannelBackendForTlsSslPtr()
    {
        Assert.AreEqual(8L, Defaults.TlsSslBackend);
    }

    [TestMethod]
    public void With_EachStringProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(Defaults with { EffectiveUrl = "http://h/" }, o => o with { EffectiveUrl = "" });
        AssertChangesOnly(Defaults with { EffectiveMethod = "POST" }, o => o with { EffectiveMethod = "GET" });
        AssertChangesOnly(Defaults with { RedirectUrl = "http://r/" }, o => o with { RedirectUrl = null });
        AssertChangesOnly(Defaults with { ContentType = "text/html" }, o => o with { ContentType = null });
        AssertChangesOnly(Defaults with { PrimaryIp = "127.0.0.1" }, o => o with { PrimaryIp = "" });
        AssertChangesOnly(Defaults with { LocalIp = "127.0.0.1" }, o => o with { LocalIp = "" });
        AssertChangesOnly(Defaults with { FtpEntryPath = "/" }, o => o with { FtpEntryPath = null });
        AssertChangesOnly(Defaults with { RtspSessionId = "s" }, o => o with { RtspSessionId = null });
        AssertChangesOnly(Defaults with { Scheme = "https" }, o => o with { Scheme = null });
        AssertChangesOnly(Defaults with { Referer = "http://f/" }, o => o with { Referer = null });
        AssertChangesOnly(Defaults with { CaInfo = "ca.pem" }, o => o with { CaInfo = null });
        AssertChangesOnly(Defaults with { CaPath = "certs" }, o => o with { CaPath = null });
    }

    [TestMethod]
    public void With_EachLongProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(Defaults with { ResponseCode = 200 }, o => o with { ResponseCode = 0 });
        AssertChangesOnly(Defaults with { HttpConnectCode = 200 }, o => o with { HttpConnectCode = 0 });
        AssertChangesOnly(Defaults with { HttpVersion = 2 }, o => o with { HttpVersion = 0 });
        AssertChangesOnly(Defaults with { FileTime = 1 }, o => o with { FileTime = -1 });
        AssertChangesOnly(Defaults with { RedirectCount = 1 }, o => o with { RedirectCount = 0 });
        AssertChangesOnly(Defaults with { HeaderSize = 1 }, o => o with { HeaderSize = 0 });
        AssertChangesOnly(Defaults with { RequestSize = 1 }, o => o with { RequestSize = 0 });
        AssertChangesOnly(Defaults with { SslVerifyResult = 1 }, o => o with { SslVerifyResult = 0 });
        AssertChangesOnly(Defaults with { ProxySslVerifyResult = 1 }, o => o with { ProxySslVerifyResult = 0 });
        AssertChangesOnly(Defaults with { HttpAuthAvailable = 1 }, o => o with { HttpAuthAvailable = 0 });
        AssertChangesOnly(Defaults with { ProxyAuthAvailable = 1 }, o => o with { ProxyAuthAvailable = 0 });
        AssertChangesOnly(Defaults with { HttpAuthUsed = 1 }, o => o with { HttpAuthUsed = 0 });
        AssertChangesOnly(Defaults with { ProxyAuthUsed = 1 }, o => o with { ProxyAuthUsed = 0 });
        AssertChangesOnly(Defaults with { OsErrno = 1 }, o => o with { OsErrno = 0 });
        AssertChangesOnly(Defaults with { NumConnects = 1 }, o => o with { NumConnects = 0 });
        AssertChangesOnly(Defaults with { PrimaryPort = 80 }, o => o with { PrimaryPort = 0 });
        AssertChangesOnly(Defaults with { LocalPort = 80 }, o => o with { LocalPort = 0 });
        AssertChangesOnly(Defaults with { ConditionUnmet = 1 }, o => o with { ConditionUnmet = 0 });
        AssertChangesOnly(Defaults with { RtspClientCSeq = 1 }, o => o with { RtspClientCSeq = 0 });
        AssertChangesOnly(Defaults with { RtspServerCSeq = 1 }, o => o with { RtspServerCSeq = 0 });
        AssertChangesOnly(Defaults with { RtspCSeqReceived = 1 }, o => o with { RtspCSeqReceived = 0 });
        AssertChangesOnly(Defaults with { Protocol = 1 }, o => o with { Protocol = 0 });
        AssertChangesOnly(Defaults with { ProxyError = 1 }, o => o with { ProxyError = 0 });
        AssertChangesOnly(Defaults with { UsedProxy = 1 }, o => o with { UsedProxy = 0 });
        AssertChangesOnly(Defaults with { TransferId = 0 }, o => o with { TransferId = -1 });
        AssertChangesOnly(Defaults with { ConnectionId = 0 }, o => o with { ConnectionId = -1 });
        AssertChangesOnly(Defaults with { LastSocket = 3 }, o => o with { LastSocket = -1 });
        AssertChangesOnly(Defaults with { ActiveSocket = 3 }, o => o with { ActiveSocket = -1 });
        AssertChangesOnly(Defaults with { TlsSslBackend = 1 }, o => o with { TlsSslBackend = 8 });
    }

    [TestMethod]
    public void With_EachTimeProperty_ChangesOnlyThatProperty()
    {
        var second = TimeSpan.FromSeconds(1);
        AssertChangesOnly(Defaults with { TotalTime = second }, o => o with { TotalTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { NameLookupTime = second }, o => o with { NameLookupTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { ConnectTime = second }, o => o with { ConnectTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { AppConnectTime = second }, o => o with { AppConnectTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { PreTransferTime = second }, o => o with { PreTransferTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { PostTransferTime = second }, o => o with { PostTransferTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { StartTransferTime = second }, o => o with { StartTransferTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { RedirectTime = second }, o => o with { RedirectTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { QueueTime = second }, o => o with { QueueTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { RetryAfter = second }, o => o with { RetryAfter = TimeSpan.Zero });
    }

    [TestMethod]
    public void With_EachSizeAndListProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(Defaults with { EarlyDataSent = 1 }, o => o with { EarlyDataSent = 0 });
        AssertChangesOnly(Defaults with { SizeUpload = 1 }, o => o with { SizeUpload = 0 });
        AssertChangesOnly(Defaults with { SizeDownload = 1 }, o => o with { SizeDownload = 0 });
        AssertChangesOnly(Defaults with { SpeedUpload = 1 }, o => o with { SpeedUpload = 0 });
        AssertChangesOnly(Defaults with { SpeedDownload = 1 }, o => o with { SpeedDownload = 0 });
        AssertChangesOnly(Defaults with { ContentLengthDownload = 1 }, o => o with { ContentLengthDownload = -1 });
        AssertChangesOnly(Defaults with { ContentLengthUpload = 1 }, o => o with { ContentLengthUpload = -1 });
        AssertChangesOnly(Defaults with { SslEngines = ["e"] }, o => o with { SslEngines = Defaults.SslEngines });
        AssertChangesOnly(Defaults with { CookieList = ["c"] }, o => o with { CookieList = Defaults.CookieList });
        AssertChangesOnly(Defaults with { CertInfo = [["Subject:x"]] }, o => o with { CertInfo = Defaults.CertInfo });
    }

    private static void AssertChangesOnly(TransferInfo changed, Func<TransferInfo, TransferInfo> restore)
    {
        Assert.AreNotEqual(Defaults, changed);
        Assert.AreEqual(Defaults, restore(changed));
    }
}
