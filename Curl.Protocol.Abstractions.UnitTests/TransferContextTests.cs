using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="TransferContext" />: an option left out of the initializer reports its
/// "not given" value, and every option set in the initializer reads back unchanged.
/// </summary>
[TestClass]
public sealed class TransferContextTests
{
    private static readonly CurlUrl AnyUrl = CurlUrl.Parse("tftp://example.com/file");

    [TestMethod]
    public void TransferContext_OnlyRequiredMembersSet_ReportsNotGivenForEveryOption()
    {
        using var output = new MemoryStream();

        var context = new TransferContext { Url = AnyUrl, Output = output };

        Assert.IsNull(context.Upload);
        Assert.IsNull(context.ResumeFrom);
        Assert.IsFalse(context.ResumeUploadFromUnknownOffset);
        Assert.IsNull(context.Range);
        Assert.IsNull(context.RangeText);
        Assert.IsNull(context.MaxFileSize);
        Assert.IsFalse(context.NoBody);
        Assert.IsNull(context.TimeCondition);
        Assert.IsNull(context.HeaderOutput);
        Assert.IsNull(context.PostData);
        Assert.IsNull(context.Credentials);
        Assert.IsEmpty(context.TelnetOptions);
        Assert.IsNull(context.TftpBlockSize);
        Assert.IsFalse(context.TftpNoOptions);
        Assert.IsFalse(context.FtpDisableEpsv);
        Assert.IsTrue(context.FtpSkipPasvIp);
        Assert.AreEqual(FtpFileMethod.MultiCwd, context.FtpFileMethod);
        Assert.IsFalse(context.FtpCreateDirectories);
        Assert.IsFalse(context.ListOnly);
        Assert.IsEmpty(context.QuoteCommands);
        Assert.IsFalse(context.ConvertLineEndings);
        Assert.IsFalse(context.PathAsIs);
        Assert.AreEqual((UnixFileMode)0b110_100_100, context.CreateFileMode);
        Assert.IsNull(context.ConnectTimeout);
        Assert.IsNull(context.MaxTime);
        Assert.IsNull(context.OperationStarted);
        Assert.IsNull(context.Proxy);
        Assert.IsNull(context.Http);
        Assert.AreSame(TimeProvider.System, context.TimeProvider);
        Assert.AreSame(NoTransferEvents.Instance, context.Events);
        Assert.AreSame(NoTransferProgress.Instance, context.Progress);
        Assert.AreEqual(CancellationToken.None, context.CancellationToken);
    }

    [TestMethod]
    public void TransferContext_EveryMemberSet_RoundTripsEveryValue()
    {
        using var output = new MemoryStream();
        using var upload = new MemoryStream();
        using var headerOutput = new MemoryStream();
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
        var events = new StubTransferEvents();
        var progress = new StubTransferProgress();

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
            PostData = postData,
            Credentials = credentials,
            TelnetOptions = telnetOptions,
            TftpBlockSize = 70000,
            TftpNoOptions = true,
            FtpDisableEpsv = true,
            FtpSkipPasvIp = false,
            FtpFileMethod = FtpFileMethod.SingleCwd,
            FtpCreateDirectories = true,
            ListOnly = true,
            QuoteCommands = quoteCommands,
            ConvertLineEndings = true,
            PathAsIs = true,
            CreateFileMode = UnixFileMode.UserRead,
            ConnectTimeout = TimeSpan.FromSeconds(3),
            MaxTime = TimeSpan.FromMilliseconds(12500),
            OperationStarted = 42,
            Proxy = proxy,
            Http = http,
            TimeProvider = timeProvider,
            Events = events,
            Progress = progress,
            CancellationToken = cancellation.Token,
        };

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
        Assert.IsTrue(context.ListOnly);
        Assert.AreSame(quoteCommands, context.QuoteCommands);
        Assert.IsTrue(context.ConvertLineEndings);
        Assert.IsTrue(context.PathAsIs);
        Assert.AreEqual(UnixFileMode.UserRead, context.CreateFileMode);
        Assert.AreEqual(TimeSpan.FromSeconds(3), context.ConnectTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(12500), context.MaxTime);
        Assert.AreEqual(42L, context.OperationStarted);
        Assert.AreSame(proxy, context.Proxy);
        Assert.AreSame(http, context.Http);
        Assert.AreSame(timeProvider, context.TimeProvider);
        Assert.AreSame(events, context.Events);
        Assert.AreSame(progress, context.Progress);
        Assert.AreEqual(cancellation.Token, context.CancellationToken);
    }

    private sealed class StubTimeProvider : TimeProvider;
}
