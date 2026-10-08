using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="TransferContext" />: an option left out of the initializer reports its
/// "not given" value, and every option set in the initializer reads back unchanged.
/// </summary>
[TestClass]
public sealed class TransferContextTests
{
    private static readonly CurlUrl AnyUrl = CurlUrl.Parse("tftp://example.com/file");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void FtpPort_WhenNotSet_IsNullForPassiveMode()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "Url and Output only");
        var context = new TransferContext { Url = AnyUrl, Output = Stream.Null };

        diagnostics.Act("FtpPort", context.FtpPort);
        diagnostics.Assert("FtpPort", null, context.FtpPort);
        Assert.IsNull(context.FtpPort);
    }

    [TestMethod]
    public void FtpUseEprt_WhenNotSet_IsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "Url and Output only");
        var context = new TransferContext { Url = AnyUrl, Output = Stream.Null };

        diagnostics.Act("FtpUseEprt", context.FtpUseEprt);
        diagnostics.Assert("FtpUseEprt", true, context.FtpUseEprt);
        Assert.IsTrue(context.FtpUseEprt);
    }

    [TestMethod]
    public void SslLevel_WhenNotSet_IsNone()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "Url and Output only");
        var context = new TransferContext { Url = AnyUrl, Output = Stream.Null };

        diagnostics.Act("SslLevel", context.SslLevel);
        diagnostics.Assert("SslLevel", TransportSecurityLevel.None, context.SslLevel);
        Assert.AreEqual(TransportSecurityLevel.None, context.SslLevel);
    }

    [TestMethod]
    public void FtpSslControlOnly_WhenNotSet_IsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "Url and Output only");
        var context = new TransferContext { Url = AnyUrl, Output = Stream.Null };

        diagnostics.Act("FtpSslControlOnly", context.FtpSslControlOnly);
        diagnostics.Assert("FtpSslControlOnly", false, context.FtpSslControlOnly);
        Assert.IsFalse(context.FtpSslControlOnly);
    }

    [TestMethod]
    public void FtpCommandChannelClearing_WhenNotSet_IsOff()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "Url and Output only");
        var context = new TransferContext { Url = AnyUrl, Output = Stream.Null };

        diagnostics.Act("FtpCommandChannelClearing", context.FtpCommandChannelClearing);
        diagnostics.Assert("FtpCommandChannelClearing", FtpCommandChannelClearing.Off, context.FtpCommandChannelClearing);
        Assert.AreEqual(FtpCommandChannelClearing.Off, context.FtpCommandChannelClearing);
    }

    [TestMethod]
    public void FtpCommandChannelClearing_WhenSet_ReadsBackUnchanged()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("FtpCommandChannelClearing", FtpCommandChannelClearing.Active);
        var context = new TransferContext { Url = AnyUrl, Output = Stream.Null, FtpCommandChannelClearing = FtpCommandChannelClearing.Active };

        diagnostics.Act("FtpCommandChannelClearing", context.FtpCommandChannelClearing);
        diagnostics.Assert("FtpCommandChannelClearing", FtpCommandChannelClearing.Active, context.FtpCommandChannelClearing);
        Assert.AreEqual(FtpCommandChannelClearing.Active, context.FtpCommandChannelClearing);
    }

    [TestMethod]
    public void RemoteTime_WhenSet_ReadsBackTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("RemoteTime", true);
        var context = new TransferContext { Url = AnyUrl, Output = Stream.Null, RemoteTime = true };

        diagnostics.Act("RemoteTime", context.RemoteTime);
        diagnostics.Assert("RemoteTime", true, context.RemoteTime);
        Assert.IsTrue(context.RemoteTime);
    }

    [TestMethod]
    public void FtpActiveModeAndTlsOptions_WhenSet_ReadBackUnchanged()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "FtpPort -, FtpUseEprt false, SslLevel Required, FtpSslControlOnly true");
        var context = new TransferContext
        {
            Url = AnyUrl,
            Output = Stream.Null,
            FtpPort = "-",
            FtpUseEprt = false,
            SslLevel = TransportSecurityLevel.Required,
            FtpSslControlOnly = true,
        };

        diagnostics.Act("FtpPort", context.FtpPort);
        diagnostics.Act("FtpUseEprt", context.FtpUseEprt);
        diagnostics.Act("SslLevel", context.SslLevel);
        diagnostics.Act("FtpSslControlOnly", context.FtpSslControlOnly);
        diagnostics.Assert("FtpPort", "-", context.FtpPort);
        Assert.AreEqual("-", context.FtpPort);
        Assert.IsFalse(context.FtpUseEprt);
        Assert.AreEqual(TransportSecurityLevel.Required, context.SslLevel);
        Assert.IsTrue(context.FtpSslControlOnly);
    }

    [TestMethod]
    public void TransferContext_OnlyRequiredMembersSet_ReportsNotGivenForEveryOption()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "Url and Output only");
        using var output = new MemoryStream();

        var context = new TransferContext { Url = AnyUrl, Output = output };

        diagnostics.Act("FtpFileMethod", context.FtpFileMethod);
        diagnostics.Act("CreateFileMode", context.CreateFileMode);
        diagnostics.Act("FtpSkipPasvIp", context.FtpSkipPasvIp);
        diagnostics.Assert("FtpFileMethod", FtpFileMethod.MultiCwd, context.FtpFileMethod);
        Assert.IsNull(context.Upload);
        Assert.IsNull(context.ResumeFrom);
        Assert.IsFalse(context.ResumeUploadFromUnknownOffset);
        Assert.IsNull(context.Range);
        Assert.IsNull(context.RangeText);
        Assert.IsNull(context.MaxFileSize);
        Assert.IsFalse(context.NoBody);
        Assert.IsNull(context.TimeCondition);
        Assert.IsFalse(context.RemoteTime);
        Assert.IsNull(context.HeaderOutput);
        Assert.IsNull(context.DumpHeaderOutput);
        Assert.IsNull(context.PostData);
        Assert.IsNull(context.Credentials);
        Assert.IsEmpty(context.TelnetOptions);
        Assert.IsNull(context.TftpBlockSize);
        Assert.IsFalse(context.TftpNoOptions);
        Assert.IsFalse(context.FtpDisableEpsv);
        Assert.IsTrue(context.FtpSkipPasvIp);
        Assert.AreEqual(FtpFileMethod.MultiCwd, context.FtpFileMethod);
        Assert.IsFalse(context.FtpCreateDirectories);
        Assert.IsNull(context.FtpAccount);
        Assert.IsNull(context.FtpAlternativeToUser);
        Assert.IsFalse(context.FtpSendPret);
        Assert.IsFalse(context.ListOnly);
        Assert.IsFalse(context.UseAscii);
        Assert.IsFalse(context.Append);
        Assert.IsEmpty(context.QuoteCommands);
        Assert.IsFalse(context.ConvertLineEndings);
        Assert.IsFalse(context.PathAsIs);
        Assert.AreEqual((UnixFileMode)0b110_100_100, context.CreateFileMode);
        Assert.IsNull(context.ConnectTimeout);
        Assert.IsNull(context.MaxTime);
        Assert.IsNull(context.OperationStarted);
        Assert.IsNull(context.Proxy);
        Assert.IsNull(context.Http);
        Assert.IsNull(context.Mail);
        Assert.IsNull(context.Ssh);
        Assert.AreSame(TimeProvider.System, context.TimeProvider);
        Assert.AreSame(NoTransferEvents.Instance, context.Events);
        Assert.AreSame(NoTransferProgress.Instance, context.Progress);
        Assert.AreEqual(CancellationToken.None, context.CancellationToken);
    }

    [TestMethod]
    public void TransferContext_EveryMemberSet_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using var output = new MemoryStream();
        using var upload = new MemoryStream();
        using var headerOutput = new MemoryStream();
        using var dumpHeaderOutput = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        var range = ByteRange.Bounded(1, 9);
        var timeCondition = new TimeCondition(
            new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
            TimeConditionKind.IfModifiedSince);
        ReadOnlyMemory<byte> postData = new byte[] { 0x78 };
        var credentials = new NetworkCredential("bob", "secret");
        string[] telnetOptions = ["TTYPE=vt100", "XDISPLOC=host:0"];
        string[] quoteCommands = ["+NOOP", "-DELE x"];
        var timeProvider = new StubTimeProvider();
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);
        var http = new HttpRequestOptions { CustomMethod = "PATCH" };
        var mail = new MailRequestOptions { From = "a@example.com" };
        var ssh = new SshOptions { Compression = true };
        var events = new StubTransferEvents();
        var progress = new StubTransferProgress();
        diagnostics.Arrange("options", "every TransferContext member set");

        var context = new TransferContext
        {
            Url = AnyUrl,
            Output = output,
            Upload = upload,
            ResumeFrom = 42,
            ResumeUploadFromUnknownOffset = true,
            Range = range,
            RangeText = "1-9,20-29",
            MaxFileSize = 1024,
            NoBody = true,
            TimeCondition = timeCondition,
            HeaderOutput = headerOutput,
            DumpHeaderOutput = dumpHeaderOutput,
            PostData = postData,
            Credentials = credentials,
            TelnetOptions = telnetOptions,
            TftpBlockSize = 70000,
            TftpNoOptions = true,
            FtpDisableEpsv = true,
            FtpSkipPasvIp = false,
            FtpFileMethod = FtpFileMethod.SingleCwd,
            FtpCreateDirectories = true,
            FtpAccount = "billing",
            FtpAlternativeToUser = "SITE AUTH",
            FtpSendPret = true,
            ListOnly = true,
            UseAscii = true,
            Append = true,
            QuoteCommands = quoteCommands,
            ConvertLineEndings = true,
            PathAsIs = true,
            CreateFileMode = UnixFileMode.UserRead,
            ConnectTimeout = TimeSpan.FromSeconds(3),
            MaxTime = TimeSpan.FromMilliseconds(12500),
            OperationStarted = 42,
            Proxy = proxy,
            Http = http,
            Mail = mail,
            Ssh = ssh,
            TimeProvider = timeProvider,
            Events = events,
            Progress = progress,
            CancellationToken = cancellation.Token,
        };

        diagnostics.Act("ResumeFrom", context.ResumeFrom);
        diagnostics.Act("RangeText", context.RangeText);
        diagnostics.Act("MaxTime", context.MaxTime);
        diagnostics.Assert("ResumeFrom", 42L, context.ResumeFrom);
        Assert.AreSame(AnyUrl, context.Url);
        Assert.AreSame(output, context.Output);
        Assert.AreSame(upload, context.Upload);
        Assert.AreEqual(42L, context.ResumeFrom);
        Assert.IsTrue(context.ResumeUploadFromUnknownOffset);
        Assert.AreEqual(range, context.Range);
        Assert.AreEqual("1-9,20-29", context.RangeText);
        Assert.AreEqual(1024L, context.MaxFileSize);
        Assert.IsTrue(context.NoBody);
        Assert.AreEqual(timeCondition, context.TimeCondition);
        Assert.AreSame(headerOutput, context.HeaderOutput);
        Assert.AreSame(dumpHeaderOutput, context.DumpHeaderOutput);
        Assert.IsTrue(context.PostData.HasValue);
        Assert.IsTrue(postData.Span.SequenceEqual(context.PostData.Value.Span));
        Assert.AreSame(credentials, context.Credentials);
        Assert.AreSame(telnetOptions, context.TelnetOptions);
        Assert.AreEqual(70000, context.TftpBlockSize);
        Assert.IsTrue(context.TftpNoOptions);
        Assert.IsTrue(context.FtpDisableEpsv);
        Assert.IsFalse(context.FtpSkipPasvIp);
        Assert.AreEqual(FtpFileMethod.SingleCwd, context.FtpFileMethod);
        Assert.IsTrue(context.FtpCreateDirectories);
        Assert.AreEqual("billing", context.FtpAccount);
        Assert.AreEqual("SITE AUTH", context.FtpAlternativeToUser);
        Assert.IsTrue(context.FtpSendPret);
        Assert.IsTrue(context.ListOnly);
        Assert.IsTrue(context.UseAscii);
        Assert.IsTrue(context.Append);
        Assert.AreSame(quoteCommands, context.QuoteCommands);
        Assert.IsTrue(context.ConvertLineEndings);
        Assert.IsTrue(context.PathAsIs);
        Assert.AreEqual(UnixFileMode.UserRead, context.CreateFileMode);
        Assert.AreEqual(TimeSpan.FromSeconds(3), context.ConnectTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(12500), context.MaxTime);
        Assert.AreEqual(42L, context.OperationStarted);
        Assert.AreSame(proxy, context.Proxy);
        Assert.AreSame(http, context.Http);
        Assert.AreSame(mail, context.Mail);
        Assert.AreSame(ssh, context.Ssh);
        Assert.AreSame(timeProvider, context.TimeProvider);
        Assert.AreSame(events, context.Events);
        Assert.AreSame(progress, context.Progress);
        Assert.AreEqual(cancellation.Token, context.CancellationToken);
    }

    private sealed class StubTimeProvider : TimeProvider;
}
