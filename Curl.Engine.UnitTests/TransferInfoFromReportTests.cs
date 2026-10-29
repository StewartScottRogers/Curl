using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Api.Info;
using Curl.Console;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class TransferInfoFromReportTests
{
    private const string RequestUrl = "http://h/x";

    private static readonly TransferTimings Timings = new(
        Started: 1000,
        Connect: new ConnectTimings(1000, NameResolved: 1001, Connected: 1003, TlsHandshakeCompleted: 1006),
        RequestReady: 1010,
        RequestSent: 1015,
        FirstByteReceived: 1021,
        Completed: 1500)
    {
        RedirectDuration = TimeSpan.FromMilliseconds(7),
    };

    private static readonly TransferReport FullReport = new()
    {
        ResponseCode = 401,
        ProxyConnectResponseCode = 200,
        UsedProxy = true,
        HttpVersion = new Version(1, 1),
        Method = "POST",
        ContentType = "text/plain",
        RedirectUrl = "http://h/next",
        EffectiveUrl = "https://h/final",
        RedirectCount = 2,
        Referer = "http://ref/",
        HeaderSize = 120,
        RequestSize = 80,
        DownloadSize = 1000,
        UploadSize = 500,
        ConnectionCount = 1,
        LocalEndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 50000),
        RemoteEndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.2"), 443),
        Timings = Timings,
        FtpEntryPath = "/home",
        AuthSchemePicked = HttpAuthSchemes.Digest,
        SslVerifyResult = 19,
        ProxySslVerifyResult = 20,
        OsErrno = 10061,
        ProxyError = CurlProxyCode.ReplyConnectionRefused,
        ProxyAuthSchemePicked = HttpAuthSchemes.Ntlm,
        RtspSessionId = "s1",
        RtspClientSequenceNumber = 3,
        RtspServerSequenceNumber = 4,
        RtspSequenceNumberReceived = 2,
        ResponseHeaders =
        [
            new("Content-Length", "1000"),
            new("WWW-Authenticate", "Basic realm=\"r\""),
            new("WWW-Authenticate", " NTLM"),
            new("WWW-Authenticate", "Unknown x"),
        ],
    };

    private static TransferInfo Full => TransferInfoFromReport.Of(
        new TransferResult(CurlExitCode.Ok, 1000) { Report = FullReport, TimeConditionUnmet = true, SourceLastWriteUnixSeconds = 1234 },
        RequestUrl,
        fileTimeRequested: true,
        new ManualTimeProvider());

    private static TransferInfo Of(TransferReport report, long bytesTransferred = 0) =>
        TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, bytesTransferred) { Report = report }, RequestUrl, fileTimeRequested: false, new ManualTimeProvider());

    [TestMethod]
    public void Of_ReportWithEffectiveUrl_TakesIt() => Assert.AreEqual("https://h/final", Full.EffectiveUrl);

    [TestMethod]
    public void Of_ReportWithoutEffectiveUrl_TakesTheRequestUrl() => Assert.AreEqual(RequestUrl, Of(new TransferReport()).EffectiveUrl);

    [TestMethod]
    public void Of_ReportWithMethod_TakesIt() => Assert.AreEqual("POST", Full.EffectiveMethod);

    [TestMethod]
    public void Of_ReportWithoutMethod_AnswersGet() => Assert.AreEqual("GET", Of(new TransferReport()).EffectiveMethod);

    [TestMethod]
    public void Of_Report_TakesTheRedirectUrl() => Assert.AreEqual("http://h/next", Full.RedirectUrl);

    [TestMethod]
    public void Of_Report_TakesTheContentType() => Assert.AreEqual("text/plain", Full.ContentType);

    [TestMethod]
    public void Of_ResponseWithRetryAfterSeconds_TakesTheLastOne() =>
        Assert.AreEqual(
            TimeSpan.FromSeconds(120),
            Of(new TransferReport { ResponseCode = 200, ResponseHeaders = [new("Retry-After", "5"), new("retry-after", " 120")] }).RetryAfter);

    [TestMethod]
    public void Of_ResponseWithoutRetryAfter_AnswersZero() => Assert.AreEqual(TimeSpan.Zero, Full.RetryAfter);

    [TestMethod]
    public void Of_Report_TakesTheFtpEntryPath() => Assert.AreEqual("/home", Full.FtpEntryPath);

    [TestMethod]
    public void Of_Report_TakesTheReferer() => Assert.AreEqual("http://ref/", Full.Referer);

    [TestMethod]
    public void Of_Report_TakesTheSslVerifyResult() => Assert.AreEqual(19, Full.SslVerifyResult);

    [TestMethod]
    public void Of_Report_TakesTheProxySslVerifyResult() => Assert.AreEqual(20, Full.ProxySslVerifyResult);

    [TestMethod]
    public void Of_ProxyNtlmPicked_AnswersCurlAuthNtlm() => Assert.AreEqual(8, Full.ProxyAuthUsed);

    [TestMethod]
    public void Of_Report_TakesTheRtspSessionId() => Assert.AreEqual("s1", Full.RtspSessionId);

    [TestMethod]
    public void Of_Report_TakesTheRtspClientCSeq() => Assert.AreEqual(3, Full.RtspClientCSeq);

    [TestMethod]
    public void Of_Report_TakesTheRtspServerCSeq() => Assert.AreEqual(4, Full.RtspServerCSeq);

    [TestMethod]
    public void Of_Report_TakesTheRtspCSeqReceived() => Assert.AreEqual(2, Full.RtspCSeqReceived);

    [TestMethod]
    public void Of_ReportWithoutProxyAuthOrRtsp_AnswersTheDefaults()
    {
        TransferInfo info = Of(new TransferReport());

        Assert.AreEqual(0, info.ProxyAuthUsed);
        Assert.IsNull(info.RtspSessionId);
        Assert.AreEqual(0, info.RtspClientCSeq);
        Assert.AreEqual(0, info.RtspCSeqReceived);
    }

    [TestMethod]
    public void Of_ReportWithoutVerifyResults_AnswersZeroForEach()
    {
        TransferInfo info = Of(new TransferReport());

        Assert.AreEqual(0, info.SslVerifyResult);
        Assert.AreEqual(0, info.ProxySslVerifyResult);
    }

    [TestMethod]
    public void Of_Report_TakesTheOsErrno() => Assert.AreEqual(10061, Full.OsErrno);

    [TestMethod]
    public void Of_ReportWithoutOsErrno_AnswersZero() => Assert.AreEqual(0, Of(new TransferReport()).OsErrno);

    [TestMethod]
    public void Of_Report_TakesTheProxyError() => Assert.AreEqual(19, Full.ProxyError);

    [TestMethod]
    public void Of_ReportWithoutProxyError_AnswersZero() => Assert.AreEqual(0, Of(new TransferReport()).ProxyError);

    [TestMethod]
    public void Of_Report_TakesTheResponseCode() => Assert.AreEqual(401, Full.ResponseCode);

    [TestMethod]
    public void Of_Report_TakesTheConnectCode() => Assert.AreEqual(200, Full.HttpConnectCode);

    [TestMethod]
    [DataRow(1, 0, 1L)]
    [DataRow(1, 1, 2L)]
    [DataRow(2, 0, 3L)]
    [DataRow(3, 0, 30L)]
    [DataRow(0, 9, 0L)]
    public void Of_StatusLineVersion_AnswersItsCurlHttpVersion(int major, int minor, long expected) =>
        Assert.AreEqual(expected, Of(new TransferReport { HttpVersion = new Version(major, minor) }).HttpVersion);

    [TestMethod]
    public void Of_NoStatusLineVersion_AnswersZero() => Assert.AreEqual(0, Of(new TransferReport()).HttpVersion);

    [TestMethod]
    public void Of_Report_TakesTheRedirectCount() => Assert.AreEqual(2, Full.RedirectCount);

    [TestMethod]
    public void Of_Report_TakesTheHeaderSize() => Assert.AreEqual(120, Full.HeaderSize);

    [TestMethod]
    public void Of_Report_TakesTheRequestSize() => Assert.AreEqual(80, Full.RequestSize);

    [TestMethod]
    public void Of_Report_TakesTheConnectionCount() => Assert.AreEqual(1, Full.NumConnects);

    [TestMethod]
    public void Of_TimeConditionUnmet_AnswersOne() => Assert.AreEqual(1, Full.ConditionUnmet);

    [TestMethod]
    public void Of_TimeConditionMet_AnswersZero() => Assert.AreEqual(0, Of(new TransferReport()).ConditionUnmet);

    [TestMethod]
    public void Of_ProxyUsed_AnswersOne() => Assert.AreEqual(1, Full.UsedProxy);

    [TestMethod]
    public void Of_NoProxy_AnswersZero() => Assert.AreEqual(0, Of(new TransferReport()).UsedProxy);

    [TestMethod]
    public void Of_AllSchemesPicked_AnswersEveryCurlAuthBit() =>
        Assert.AreEqual(1 | 2 | 4 | 8 | 64, Of(new TransferReport { AuthSchemePicked = HttpAuthSchemes.Any | HttpAuthSchemes.Bearer }).HttpAuthUsed);

    [TestMethod]
    public void Of_DigestPicked_AnswersCurlAuthDigest() => Assert.AreEqual(2, Full.HttpAuthUsed);

    [TestMethod]
    public void Of_ContentLengthHeader_AnswersIt() => Assert.AreEqual(1000, Full.ContentLengthDownload);

    [TestMethod]
    public void Of_UnparsableContentLength_AnswersMinusOne() =>
        Assert.AreEqual(-1, Of(new TransferReport { ResponseHeaders = [new("Content-Length", "x")] }).ContentLengthDownload);

    [TestMethod]
    public void Of_NoContentLength_AnswersMinusOne() => Assert.AreEqual(-1, Of(new TransferReport()).ContentLengthDownload);

    [TestMethod]
    public void Of_RemoteEndPoint_TakesItsAddress() => Assert.AreEqual("10.0.0.2", Full.PrimaryIp);

    [TestMethod]
    public void Of_UnixSocket_TakesItsRemoteIpText() =>
        Assert.AreEqual("/tmp/sock", Of(new TransferReport { UnixSocketRemoteIp = "/tmp/sock" }).PrimaryIp);

    [TestMethod]
    public void Of_NoRemoteEndPoint_AnswersEmptyPrimaryIp() => Assert.AreEqual(string.Empty, Of(new TransferReport()).PrimaryIp);

    [TestMethod]
    public void Of_RemoteEndPoint_TakesItsPort() => Assert.AreEqual(443, Full.PrimaryPort);

    [TestMethod]
    public void Of_NoRemoteEndPoint_AnswersPrimaryPortZero() => Assert.AreEqual(0, Of(new TransferReport()).PrimaryPort);

    [TestMethod]
    public void Of_LocalEndPoint_TakesItsAddress() => Assert.AreEqual("10.0.0.1", Full.LocalIp);

    [TestMethod]
    public void Of_LocalEndPoint_TakesItsPort() => Assert.AreEqual(50000, Full.LocalPort);

    [TestMethod]
    public void Of_NoLocalEndPoint_AnswersLocalPortZero() => Assert.AreEqual(0, Of(new TransferReport()).LocalPort);

    [TestMethod]
    public void Of_Report_TakesTheDownloadSize() => Assert.AreEqual(1000, Full.SizeDownload);

    [TestMethod]
    public void Of_NoReport_TakesTheBytesTransferredAsDownloadSize() =>
        Assert.AreEqual(42, TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, 42), RequestUrl, false, new ManualTimeProvider()).SizeDownload);

    [TestMethod]
    public void Of_Report_TakesTheUploadSize() => Assert.AreEqual(500, Full.SizeUpload);

    [TestMethod]
    public void Of_Timings_AnswersDownloadSpeedOverTheTotalTime() => Assert.AreEqual(2000, Full.SpeedDownload);

    [TestMethod]
    public void Of_Timings_AnswersUploadSpeedOverTheTotalTime() => Assert.AreEqual(1000, Full.SpeedUpload);

    [TestMethod]
    public void Of_NoTimings_AnswersSpeedZero() => Assert.AreEqual(0, Of(new TransferReport { DownloadSize = 10 }).SpeedDownload);

    [TestMethod]
    public void Of_HugeSizeOverMoreThanASecond_DividesBySeconds() =>
        Assert.AreEqual(long.MaxValue / 2, Of(new TransferReport { DownloadSize = long.MaxValue, Timings = Timings with { Completed = 3000 } }).SpeedDownload);

    [TestMethod]
    public void Of_HugeSizeUnderASecond_AnswersTheLargestSpeed() =>
        Assert.AreEqual(long.MaxValue, Of(new TransferReport { DownloadSize = long.MaxValue, Timings = Timings }).SpeedDownload);

    [TestMethod]
    public void Of_Timings_AnswersTheTotalTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(500), Full.TotalTime);

    [TestMethod]
    public void Of_Timings_AnswersOneMicrosecondOfQueueTime() => Assert.AreEqual(TimeSpan.FromMicroseconds(1), Full.QueueTime);

    [TestMethod]
    public void Of_Timings_AnswersTheNameLookupTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(1), Full.NameLookupTime);

    [TestMethod]
    public void Of_Timings_AnswersTheConnectTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(3), Full.ConnectTime);

    [TestMethod]
    public void Of_Timings_AnswersTheAppConnectTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(6), Full.AppConnectTime);

    [TestMethod]
    public void Of_Timings_AnswersThePreTransferTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(10), Full.PreTransferTime);

    [TestMethod]
    public void Of_Timings_AnswersThePostTransferTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(15), Full.PostTransferTime);

    [TestMethod]
    public void Of_Timings_AnswersTheStartTransferTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(21), Full.StartTransferTime);

    [TestMethod]
    public void Of_Timings_AnswersTheRedirectTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(7), Full.RedirectTime);

    [TestMethod]
    public void Of_TimingsWithoutConnect_AnswersConnectTimeZero() =>
        Assert.AreEqual(TimeSpan.Zero, Of(new TransferReport { Timings = Timings with { Connect = null } }).ConnectTime);

    [TestMethod]
    public void Of_NoTimings_AnswersTotalTimeZero() => Assert.AreEqual(TimeSpan.Zero, Of(new TransferReport()).TotalTime);

    [TestMethod]
    public void Of_Report_TakesTheEffectiveUrlsScheme() => Assert.AreEqual("https", Full.Scheme);

    [TestMethod]
    public void Of_Report_AnswersTheEffectiveUrlsCurlProtoBit() => Assert.AreEqual(2, Full.Protocol);

    [TestMethod]
    public void Of_WssUrl_AnswersCurlProtoWss() => Assert.AreEqual(1L << 31, Of(new TransferReport { EffectiveUrl = "wss://h/" }).Protocol);

    [TestMethod]
    public void Of_UnknownScheme_AnswersProtocolZero() => Assert.AreEqual(0, Of(new TransferReport { EffectiveUrl = "nope://h/" }).Protocol);

    [TestMethod]
    public void Of_UnparsableEffectiveUrl_AnswersNoScheme() => Assert.IsNull(Of(new TransferReport { EffectiveUrl = "http://[::1/" }).Scheme);

    [TestMethod]
    public void Of_UnparsableEffectiveUrl_AnswersProtocolZero() => Assert.AreEqual(0, Of(new TransferReport { EffectiveUrl = "http://[::1/" }).Protocol);

    [TestMethod]
    public void Of_NoReport_AnswersNoScheme() =>
        Assert.IsNull(TransferInfoFromReport.Of(new TransferResult(CurlExitCode.CouldntConnect, 0), RequestUrl, false, new ManualTimeProvider()).Scheme);

    [TestMethod]
    public void Of_RedirectRefusedByProtocolList_KeepsTheRequestUrlsSchemeAndProtocol()
    {
        TransferResult result = new(CurlExitCode.UnsupportedProtocol, 0, "Protocol \"file\" is disabled (in redirect)")
        {
            Report = new TransferReport { EffectiveUrl = "file:///x" },
        };
        TransferInfo info = TransferInfoFromReport.Of(result, "http://h/loc-file", false, new ManualTimeProvider());
        Assert.AreEqual("http", info.Scheme);
        Assert.AreEqual(1, info.Protocol);
    }

    [TestMethod]
    public void Of_UnsupportedProtocolWithNoMessage_TakesTheEffectiveUrlsScheme()
    {
        TransferResult result = new(CurlExitCode.UnsupportedProtocol, 0) { Report = new TransferReport { EffectiveUrl = "ftp://h/" } };
        Assert.AreEqual("ftp", TransferInfoFromReport.Of(result, "http://h/", false, new ManualTimeProvider()).Scheme);
    }
    [TestMethod]
    public void Of_FileThatCannotBeRead_AnswersFileSchemeAndProtocol()
    {
        TransferInfo info = TransferInfoFromReport.Of(TransferResult.Failure(CurlExitCode.FileCouldntReadFile, "x"), "file:///dir/nope.txt", false, new ManualTimeProvider());
        Assert.AreEqual("file", info.Scheme);
        Assert.AreEqual(1024, info.Protocol);
    }
    [TestMethod]
    public void Of_FileTimeRequested_TakesTheSourceTime() => Assert.AreEqual(1234, Full.FileTime);

    [TestMethod]
    public void Of_FileTimeRequestedButUnknown_AnswersMinusOne() =>
        Assert.AreEqual(-1, TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport() }, RequestUrl, true, new ManualTimeProvider()).FileTime);

    [TestMethod]
    public void Of_FileTimeNotRequestedForAFileUrl_TakesTheSourceTime() =>
        Assert.AreEqual(1711956244, TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, 0) { SourceLastWriteUnixSeconds = 1711956244 }, "file:///dir/x", false, new ManualTimeProvider()).FileTime);

    [TestMethod]
    public void Of_FileTimeNotRequestedForAFileUrlThatFailed_AnswersMinusOne() =>
        Assert.AreEqual(-1, TransferInfoFromReport.Of(TransferResult.Failure(CurlExitCode.FileCouldntReadFile, "x"), "file:///dir/nope.txt", false, new ManualTimeProvider()).FileTime);

    [TestMethod]
    public void Of_FileTimeNotRequested_AnswersMinusOne() =>
        Assert.AreEqual(-1, TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, 0, null, DateTimeOffset.UnixEpoch.AddSeconds(5)) { Report = new TransferReport() }, RequestUrl, false, new ManualTimeProvider()).FileTime);

    [TestMethod]
    public void Of_CertificateInfoRequested_GivesOneListPerPeerCertificate()
    {
        ReadOnlyMemory<byte>[] chain = [SelfSignedCertificate("CN=leaf"), SelfSignedCertificate("CN=root")];

        TransferInfo info = TransferInfoFromReport.Of(
            new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport { PeerCertificates = chain } }, RequestUrl, false, new ManualTimeProvider(), certificateInfoRequested: true, tlsBackend: TlsBackend.Schannel);

        Assert.HasCount(2, info.CertInfo);
        Assert.AreEqual("Subject:CN=leaf", info.CertInfo[0][0]);
        Assert.AreEqual("Subject:CN=root", info.CertInfo[1][0]);
    }

    [TestMethod]
    public void Of_CertificateInfoNotRequested_LeavesTheListsEmpty()
    {
        TransferInfo info = TransferInfoFromReport.Of(
            new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport { PeerCertificates = [SelfSignedCertificate("CN=leaf")] } }, RequestUrl, false, new ManualTimeProvider());

        Assert.IsEmpty(info.CertInfo);
    }

    [TestMethod]
    public void Of_CertificateInfoRequestedWithOpenSsl_GivesTheOpenSslRecordsWithTheAuthorityKeyIdentifierInOneString()
    {
        TransferInfo info = TransferInfoFromReport.Of(
            new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport { PeerCertificates = [Convert.FromBase64String(CaSignedLeaf)] } },
            RequestUrl, false, new ManualTimeProvider(), certificateInfoRequested: true, tlsBackend: TlsBackend.OpenSsl);

        IReadOnlyList<string> records = info.CertInfo[0];
        Assert.HasCount(20, records);
        Assert.AreEqual("Subject:CN = leaf.test, O = \"Acme, Inc\"", records[0]);
        Assert.AreEqual(
            "X509v3 Authority Key Identifier:keyid:56:A2:63:F8:2C:9F:0A:A6:C4:BD:9B:A7:EA:A4:42:9B:C7:40:A4:B5\nDirName:/C=US/O=Test CA/CN=Root\nserial:54:43:1D:E3:69:AF:65:45:03:37:33:6E:F3:81:75:A2:26:4B:59:D8",
            records[7]);
        Assert.AreEqual("X509v3 Subject Alternative Name:DNS:leaf.test, IP Address:0:0:0:0:0:0:0:1, email:a@b.test, URI:http://x.test/", records[8]);
        StringAssert.StartsWith(records[^1], "Cert:-----BEGIN CERTIFICATE-----\n");
    }

    [TestMethod]
    public void Of_CertificateInfoRequestedWithSchannel_GivesTheX509Asn1Records()
    {
        TransferInfo info = TransferInfoFromReport.Of(
            new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport { PeerCertificates = [Convert.FromBase64String(CaSignedLeaf)] } },
            RequestUrl, false, new ManualTimeProvider(), certificateInfoRequested: true, tlsBackend: TlsBackend.Schannel);

        Assert.AreEqual("Subject:CN=leaf.test, O=Acme, Inc", info.CertInfo[0][0]);
        StringAssert.StartsWith(info.CertInfo[0][^1], "Cert:-----BEGIN CERTIFICATE-----\n");
    }

    [TestMethod]
    public void Of_CertificateInfoRequestedWithoutABackend_FollowsThePlatform()
    {
        TransferInfo info = TransferInfoFromReport.Of(
            new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport { PeerCertificates = [SelfSignedCertificate("CN=leaf")] } },
            RequestUrl, false, new ManualTimeProvider(), certificateInfoRequested: true);

        Assert.AreEqual(PlatformTlsBackend.ForProcess == TlsBackend.OpenSsl ? "Subject:CN = leaf" : "Subject:CN=leaf", info.CertInfo[0][0]);
    }

    private const string CaSignedLeaf =
        "MIIECDCCAvCgAwIBAgICAIAwDQYJKoZIhvcNAQELBQAwLjELMAkGA1UEBhMCVVMxEDAOBgNVBAoMB1Rlc3QgQ0ExDTALBgNVBAMMBFJvb3QwHhcNMjYxMDI2MjAxOTI0WhcNMjYxMTI1MjAxOTI0WjAoMRIwEAYDVQQDDAlsZWFmLnRlc3QxEjAQBgNVBAoMCUFjbWUsIEluYzCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAO+4X3MNI6hSDPt6Pl6mi8JfHuyG3yYtGhLPGkaPUgpUQeXOVNe7HotrYAUytCbqfPpjnaKr65SrD+BcSZIMgDIPk7NMPWAQYYzNb7cS1hD2M1HwP3fkLnsIoJQAjHXiX4HeA2dVqAF2bCCUoJezgsBFYvekmDMxBduUx4BLZdCJnkmxqrJX9+J95ykHpjfyolZYIHOA+wSVXXWrhksTKjNSjNDORCcGPdjuYpTAhArZ1PA52kuXh0qvE54nzyrO/EH6QmaaV5cW8+Lg6UcjRFf738Vz7ZHt8iZpWJRKxpk4aLM0EN9nAeaWwaBVYFsgVmQZaZaT9ocEKKg+AnHwb4UCAwEAAaOCATQwggEwMAkGA1UdEwQCMAAwaQYDVR0jBGIwYIAUVqJj+CyfCqbEvZun6qRCm8dApLWhMqQwMC4xCzAJBgNVBAYTAlVTMRAwDgYDVQQKDAdUZXN0IENBMQ0wCwYDVQQDDARSb290ghRUQx3jaa9lRQM3M27zgXWiJktZ2DBABgNVHREEOTA3gglsZWFmLnRlc3SHEAAAAAAAAAAAAAAAAAAAAAGBCGFAYi50ZXN0hg5odHRwOi8veC50ZXN0LzARBgMqAwQECgwIaGkgdGhlcmUwDAYDVR0PBAUDAwdfgDA2BgNVHSUELzAtBggrBgEFBQcDAwYIKwYBBQUHAwQGCCsGAQUFBwMIBggrBgEFBQcDCQYDKgMFMB0GA1UdDgQWBBQ6RyXqdqz4bE+Uzexy3feDTg1NczANBgkqhkiG9w0BAQsFAAOCAQEAb/o79SPjEi8tfra7qC1u46X4yJD71d7TNEMoYXqS7wtAnWvy84s5S5JfEsj76l3zSjKSqUYSVnbYNxX9374HArtI4slr6iUdmhxs7hQ54J3RUZcNrg4vGSIU+tCxXrXGem97ZycTne4fxFOaJWveJlamjnXf9Pumvgpnn/e+3eG7lNvlM0IbIRBD5nyzzjy45gO6Y2kOoMASQTyS88ZuwXF4oUvYOKi1r1zzbzt7ExJvQlmn7XSliQog4QBvZyUcw0MKwgjpUJVwa4ICMnd+Tb9aFqUvACUmgHtL+Q6SOJ0r9I9vzPHID/AsAQgj202oPtdQXLn0bX/V0ECtDPC4Yg==";

    [TestMethod]
    public void CertInfoRecordsOf_CertificateText_GivesEachLineAndThePemAsCert()
    {
        string text = PeerCertificateText.Format(SelfSignedCertificate("CN=leaf"));
        int pemStart = text.IndexOf("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal);

        IReadOnlyList<string> records = TransferInfoFromReport.CertInfoRecordsOf(text, TlsBackend.Schannel);

        Assert.AreEqual("Cert:" + text[pemStart..], records[^1]);
        Assert.AreEqual(text[..pemStart], string.Concat(records.Take(records.Count - 1).Select(record => record + "\n")));
    }

    [TestMethod]
    public void CertInfoRecordsOf_UnreadableCertificate_GivesNoStrings() =>
        Assert.IsEmpty(TransferInfoFromReport.CertInfoRecordsOf(PeerCertificateText.Format(new byte[] { 1, 2, 3 }), TlsBackend.Schannel));

    private static ReadOnlyMemory<byte> SelfSignedCertificate(string subject)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(1));
        return certificate.RawData;
    }
}
