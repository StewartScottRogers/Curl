using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how <see cref="TransferContextFactory" /> copies the parsed command line, the URL and
/// the resolved per-transfer values into a <see cref="TransferContext" />, and that only a
/// <c>telnet</c> transfer uploads standard input.
/// </summary>
[TestClass]
public sealed class TransferContextFactoryTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Create_PerTransferValues_AreCopiedAsGiven()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using MemoryStream headerOutput = new();
        CurlUrl url = CurlUrl.Parse("file:///x.txt");
        ByteRange range = ByteRange.Bounded(2, 5);

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///x.txt"), url, output, range, 7, headerOutput);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Url is url", true, ReferenceEquals(url, context.Url));
        Assert.AreSame(url, context.Url);
        Diagnostics.Assert("context.Output is output", true, ReferenceEquals(output, context.Output));
        Assert.AreSame(output, context.Output);
        Diagnostics.Assert("context.HeaderOutput is headerOutput", true, ReferenceEquals(headerOutput, context.HeaderOutput));
        Assert.AreSame(headerOutput, context.HeaderOutput);
        Diagnostics.Assert("context.Range", range, context.Range);
        Assert.AreEqual(range, context.Range);
        Diagnostics.Assert("context.ResumeFrom", 7L, context.ResumeFrom);
        Assert.AreEqual(7L, context.ResumeFrom);
    }

    [TestMethod]
    public void Create_Progress_IsTheSinkGiven()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        TransferProgressRecorder progress = new(new ManualTimeProvider());

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null, progress: progress);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Progress is progress", true, ReferenceEquals(progress, context.Progress));
        Assert.AreSame(progress, context.Progress);
    }

    [TestMethod]
    public void Create_ClockGiven_IsTheContextsTimeProvider()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        ManualTimeProvider clock = new();

        TransferContext context = new TransferContextFactory(standardInput, clock)
            .Create(Parse("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.TimeProvider is clock", true, ReferenceEquals(clock, context.TimeProvider));
        Assert.AreSame(clock, context.TimeProvider);
    }

    [TestMethod]
    public void Create_NoClock_IsTheSystemClock()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.TimeProvider is TimeProvider.System", true, ReferenceEquals(TimeProvider.System, context.TimeProvider));
        Assert.AreSame(TimeProvider.System, context.TimeProvider);
    }

    [TestMethod]
    public void Create_NoProgress_IsNoTransferProgress()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Progress is NoTransferProgress.Instance", true, ReferenceEquals(NoTransferProgress.Instance, context.Progress));
        Assert.AreSame(NoTransferProgress.Instance, context.Progress);
    }

    [TestMethod]
    public void Create_NoOptions_LeavesEveryOptionAtItsDefault()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.HeaderOutput", "null", context.HeaderOutput is null ? "null" : "set");
        Assert.IsNull(context.HeaderOutput);
        Diagnostics.Assert("context.Range", "null", context.Range is null ? "null" : "set");
        Assert.IsNull(context.Range);
        Diagnostics.Assert("context.ResumeFrom", "null", context.ResumeFrom is null ? "null" : "set");
        Assert.IsNull(context.ResumeFrom);
        Diagnostics.Assert("context.MaxFileSize", "null", context.MaxFileSize is null ? "null" : "set");
        Assert.IsNull(context.MaxFileSize);
        Diagnostics.Assert("context.Upload", "null", context.Upload is null ? "null" : "set");
        Assert.IsNull(context.Upload);
        Diagnostics.Assert("context.PostData", "null", context.PostData is null ? "null" : "set");
        Assert.IsNull(context.PostData);
        Diagnostics.Assert("context.Credentials", "null", context.Credentials is null ? "null" : "set");
        Assert.IsNull(context.Credentials);
        Diagnostics.Assert("context.TelnetOptions", string.Empty, string.Join(", ", context.TelnetOptions));
        Assert.IsEmpty(context.TelnetOptions);
        Diagnostics.Assert("context.TftpBlockSize", "null", context.TftpBlockSize is null ? "null" : "set");
        Assert.IsNull(context.TftpBlockSize);
        Diagnostics.Assert("context.TftpNoOptions", false, context.TftpNoOptions);
        Assert.IsFalse(context.TftpNoOptions);
        Diagnostics.Assert("context.FtpDisableEpsv", false, context.FtpDisableEpsv);
        Assert.IsFalse(context.FtpDisableEpsv);
        Diagnostics.Assert("context.FtpSkipPasvIp", true, context.FtpSkipPasvIp);
        Assert.IsTrue(context.FtpSkipPasvIp);
        Diagnostics.Assert("context.FtpFileMethod", FtpFileMethod.MultiCwd, context.FtpFileMethod);
        Assert.AreEqual(FtpFileMethod.MultiCwd, context.FtpFileMethod);
        Diagnostics.Assert("context.FtpCreateDirectories", false, context.FtpCreateDirectories);
        Assert.IsFalse(context.FtpCreateDirectories);
        Diagnostics.Assert("context.FtpAccount", "null", context.FtpAccount is null ? "null" : "set");
        Assert.IsNull(context.FtpAccount);
        Diagnostics.Assert("context.FtpAlternativeToUser", "null", context.FtpAlternativeToUser is null ? "null" : "set");
        Assert.IsNull(context.FtpAlternativeToUser);
        Diagnostics.Assert("context.FtpSendPret", false, context.FtpSendPret);
        Assert.IsFalse(context.FtpSendPret);
        Diagnostics.Assert("context.ListOnly", false, context.ListOnly);
        Assert.IsFalse(context.ListOnly);
        Diagnostics.Assert("context.UseAscii", false, context.UseAscii);
        Assert.IsFalse(context.UseAscii);
        Diagnostics.Assert("context.Append", false, context.Append);
        Assert.IsFalse(context.Append);
        Diagnostics.Assert("context.ConvertLineEndings", false, context.ConvertLineEndings);
        Assert.IsFalse(context.ConvertLineEndings);
        Diagnostics.Assert("context.QuoteCommands", string.Empty, string.Join(", ", context.QuoteCommands));
        Assert.IsEmpty(context.QuoteCommands);
        Diagnostics.Assert("context.CreateFileMode", TransferContext.DefaultCreateFileMode, context.CreateFileMode);
        Assert.AreEqual(TransferContext.DefaultCreateFileMode, context.CreateFileMode);
        Diagnostics.Assert("context.ConnectTimeout", "null", context.ConnectTimeout is null ? "null" : "set");
        Assert.IsNull(context.ConnectTimeout);
        Diagnostics.Assert("context.MaxTime", "null", context.MaxTime is null ? "null" : "set");
        Assert.IsNull(context.MaxTime);
        Diagnostics.Assert("context.TimeCondition", "null", context.TimeCondition is null ? "null" : "set");
        Assert.IsNull(context.TimeCondition);
        Diagnostics.Assert("context.RemoteTime", false, context.RemoteTime);
        Assert.IsFalse(context.RemoteTime);
        Diagnostics.Assert("context.PathAsIs", false, context.PathAsIs);
        Assert.IsFalse(context.PathAsIs);
    }

    [TestMethod]
    public void Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        CommandLineOptions options = Parse(
            "--max-filesize", "100",
            "-d", "a=b",
            "-u", "user:secret",
            "-t", "TTYPE=vt100",
            "--tftp-blksize", "1024",
            "--tftp-no-options",
            "--disable-epsv",
            "--no-ftp-skip-pasv-ip",
            "--ftp-method", "singlecwd",
            "--ftp-create-dirs",
            "--ftp-account", "billing",
            "--ftp-alternative-to-user", "SITE AUTH",
            "--ftp-pret",
            "-l",
            "-B",
            "-a",
            "--crlf",
            "-Q", "NOOP",
            "-Q", "-DELE x",
            "--create-file-mode", "0600",
            "--connect-timeout", "3",
            "--path-as-is",
            "-R",
            "-m", "9",
            "-z", "Sun, 06 Nov 1994 08:49:37 GMT",
            "tftp://example.com/x");

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(options, CurlUrl.Parse("tftp://example.com/x"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.MaxFileSize", 100L, context.MaxFileSize);
        Assert.AreEqual(100L, context.MaxFileSize);
        Diagnostics.Assert("System.Text.Encoding.ASCII.GetString(context.PostData!.Value.Span)", "a=b", System.Text.Encoding.ASCII.GetString(context.PostData!.Value.Span));
        Assert.AreEqual("a=b", System.Text.Encoding.ASCII.GetString(context.PostData!.Value.Span));
        Diagnostics.Assert("context.Credentials!.UserName", "user", context.Credentials!.UserName);
        Assert.AreEqual("user", context.Credentials!.UserName);
        Diagnostics.Assert("context.Credentials.Password", "secret", context.Credentials.Password);
        Assert.AreEqual("secret", context.Credentials.Password);
        Diagnostics.Assert("context.TelnetOptions.ToArray()", string.Join(", ", new[] { "TTYPE=vt100" }), string.Join(", ", context.TelnetOptions.ToArray()));
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100" }, context.TelnetOptions.ToArray());
        Diagnostics.Assert("context.TftpBlockSize", 1024, context.TftpBlockSize);
        Assert.AreEqual(1024, context.TftpBlockSize);
        Diagnostics.Assert("context.TftpNoOptions", true, context.TftpNoOptions);
        Assert.IsTrue(context.TftpNoOptions);
        Diagnostics.Assert("context.FtpDisableEpsv", true, context.FtpDisableEpsv);
        Assert.IsTrue(context.FtpDisableEpsv);
        Diagnostics.Assert("context.FtpSkipPasvIp", false, context.FtpSkipPasvIp);
        Assert.IsFalse(context.FtpSkipPasvIp);
        Diagnostics.Assert("context.FtpFileMethod", FtpFileMethod.SingleCwd, context.FtpFileMethod);
        Assert.AreEqual(FtpFileMethod.SingleCwd, context.FtpFileMethod);
        Diagnostics.Assert("context.FtpCreateDirectories", true, context.FtpCreateDirectories);
        Assert.IsTrue(context.FtpCreateDirectories);
        Diagnostics.Assert("context.FtpAccount", "billing", context.FtpAccount);
        Assert.AreEqual("billing", context.FtpAccount);
        Diagnostics.Assert("context.FtpAlternativeToUser", "SITE AUTH", context.FtpAlternativeToUser);
        Assert.AreEqual("SITE AUTH", context.FtpAlternativeToUser);
        Diagnostics.Assert("context.FtpSendPret", true, context.FtpSendPret);
        Assert.IsTrue(context.FtpSendPret);
        Diagnostics.Assert("context.ListOnly", true, context.ListOnly);
        Assert.IsTrue(context.ListOnly);
        Diagnostics.Assert("context.UseAscii", true, context.UseAscii);
        Assert.IsTrue(context.UseAscii);
        Diagnostics.Assert("context.Append", true, context.Append);
        Assert.IsTrue(context.Append);
        Diagnostics.Assert("context.ConvertLineEndings", true, context.ConvertLineEndings);
        Assert.IsTrue(context.ConvertLineEndings);
        Diagnostics.Assert("context.QuoteCommands.ToArray()", string.Join(", ", new[] { "NOOP", "-DELE x" }), string.Join(", ", context.QuoteCommands.ToArray()));
        CollectionAssert.AreEqual(new[] { "NOOP", "-DELE x" }, context.QuoteCommands.ToArray());
        Diagnostics.Assert("context.CreateFileMode", UnixFileMode.UserRead | UnixFileMode.UserWrite, context.CreateFileMode);
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, context.CreateFileMode);
        Diagnostics.Assert("context.ConnectTimeout", TimeSpan.FromSeconds(3), context.ConnectTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(3), context.ConnectTimeout);
        Diagnostics.Assert("context.MaxTime", TimeSpan.FromSeconds(9), context.MaxTime);
        Assert.AreEqual(TimeSpan.FromSeconds(9), context.MaxTime);
        Diagnostics.Assert("context.PathAsIs", true, context.PathAsIs);
        Assert.IsTrue(context.PathAsIs);
        Diagnostics.Assert("context.TimeCondition", options.TimeCondition, context.TimeCondition);
        Assert.AreEqual(options.TimeCondition, context.TimeCondition);
        Diagnostics.Assert("context.TimeCondition", "set", context.TimeCondition is null ? "null" : "set");
        Assert.IsNotNull(context.TimeCondition);
        Diagnostics.Assert("context.RemoteTime", true, context.RemoteTime);
        Assert.IsTrue(context.RemoteTime);
    }

    [TestMethod]
    public void Create_NoFtpActiveOrTlsOptions_IsPassiveWithEprtAndNoTls()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.FtpPort", "null", context.FtpPort is null ? "null" : "set");
        Assert.IsNull(context.FtpPort);
        Diagnostics.Assert("context.FtpUseEprt", true, context.FtpUseEprt);
        Assert.IsTrue(context.FtpUseEprt);
        Diagnostics.Assert("context.SslLevel", TransportSecurityLevel.None, context.SslLevel);
        Assert.AreEqual(TransportSecurityLevel.None, context.SslLevel);
        Diagnostics.Assert("context.FtpSslControlOnly", false, context.FtpSslControlOnly);
        Assert.IsFalse(context.FtpSslControlOnly);
    }

    [TestMethod]
    [DataRow(new string[0], FtpCommandChannelClearing.Off)]
    [DataRow(new[] { "--ftp-ssl-ccc" }, FtpCommandChannelClearing.Passive)]
    [DataRow(new[] { "--ftp-ssl-ccc-mode", "passive" }, FtpCommandChannelClearing.Passive)]
    [DataRow(new[] { "--ftp-ssl-ccc-mode", "active" }, FtpCommandChannelClearing.Active)]
    [DataRow(new[] { "--ftp-ssl-ccc-mode", "active", "--no-ftp-ssl-ccc" }, FtpCommandChannelClearing.Off)]
    public void Create_FtpSslCccOptions_AreTheCommandChannelClearing(string[] options, FtpCommandChannelClearing expected)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse([.. options, "ftp://example.com/f"]), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.FtpCommandChannelClearing", expected, context.FtpCommandChannelClearing);
        Assert.AreEqual(expected, context.FtpCommandChannelClearing);
    }

    [TestMethod]
    public void Create_FtpPort_IsCopiedVerbatim()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("-P", "-", "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.FtpPort", "-", context.FtpPort);
        Assert.AreEqual("-", context.FtpPort);
    }

    [TestMethod]
    public void Create_DisableEprt_TurnsFtpUseEprtOff()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("--disable-eprt", "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.FtpUseEprt", false, context.FtpUseEprt);
        Assert.IsFalse(context.FtpUseEprt);
    }

    [TestMethod]
    [DataRow("--ssl", TransportSecurityLevel.Try)]
    [DataRow("--ssl-reqd", TransportSecurityLevel.Required)]
    public void Create_SslOption_IsTheSslLevel(string option, TransportSecurityLevel expected)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse(option, "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.SslLevel", expected, context.SslLevel);
        Assert.AreEqual(expected, context.SslLevel);
        Diagnostics.Assert("context.FtpSslControlOnly", false, context.FtpSslControlOnly);
        Assert.IsFalse(context.FtpSslControlOnly);
    }

    [TestMethod]
    public void Create_FtpSslControl_IsControlOnlyAndRequired()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("--ftp-ssl-control", "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.FtpSslControlOnly", true, context.FtpSslControlOnly);
        Assert.IsTrue(context.FtpSslControlOnly);
        Diagnostics.Assert("context.SslLevel", TransportSecurityLevel.Required, context.SslLevel);
        Assert.AreEqual(TransportSecurityLevel.Required, context.SslLevel);
    }

    [TestMethod]
    public void Create_HttpRequestOptions_AreMappedOntoHttp()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        CommandLineOptions options = Parse(
            "-X", "PATCH", "-H", "X: 1", "--proxy-header", "X-P: 1", "--proxy-header", "X-Q: 2", "-A", "a/1", "-e", "http://r/", "-d", "a=b", "http://example.com/");

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(options, CurlUrl.Parse("http://example.com/"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Http!.CustomMethod", "PATCH", context.Http!.CustomMethod);
        Assert.AreEqual("PATCH", context.Http!.CustomMethod);
        Diagnostics.Assert("context.Http.Headers.ToArray()", string.Join(", ", new[] { "X: 1" }), string.Join(", ", context.Http.Headers.ToArray()));
        CollectionAssert.AreEqual(new[] { "X: 1" }, context.Http.Headers.ToArray());
        Diagnostics.Assert("context.Http.ProxyHeaders.ToArray()", string.Join(", ", new[] { "X-P: 1", "X-Q: 2" }), string.Join(", ", context.Http.ProxyHeaders.ToArray()));
        CollectionAssert.AreEqual(new[] { "X-P: 1", "X-Q: 2" }, context.Http.ProxyHeaders.ToArray());
        Diagnostics.Assert("context.Http.UserAgent", "a/1", context.Http.UserAgent);
        Assert.AreEqual("a/1", context.Http.UserAgent);
        Diagnostics.Assert("context.Http.Referer", "http://r/", context.Http.Referer);
        Assert.AreEqual("http://r/", context.Http.Referer);
        BytesBody body = (BytesBody)context.Http.Body!;
        Diagnostics.Assert("System.Text.Encoding.ASCII.GetString(body.Content.Span)", "a=b", System.Text.Encoding.ASCII.GetString(body.Content.Span));
        Assert.AreEqual("a=b", System.Text.Encoding.ASCII.GetString(body.Content.Span));
        Diagnostics.Assert("body.ContentType", HttpRequestOptionsMapping.FormUrlEncoded, body.ContentType);
        Assert.AreEqual(HttpRequestOptionsMapping.FormUrlEncoded, body.ContentType);
    }

    [TestMethod]
    [DataRow(new string[0], 50)]
    [DataRow(new[] { "--max-redirs", "3" }, 3)]
    [DataRow(new[] { "--max-redirs", "-1" }, -1)]
    public void Create_MaxRedirs_IsMappedOntoHttpForThe417Resend(string[] arguments, int expected)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        CommandLineOptions options = Parse([.. arguments, "http://example.com/"]);

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(options, CurlUrl.Parse("http://example.com/"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Http!.MaxRedirects", expected, context.Http!.MaxRedirects);
        Assert.AreEqual(expected, context.Http!.MaxRedirects);
        Diagnostics.Assert("context.Http.RedirectsFollowed", 0, context.Http.RedirectsFollowed);
        Assert.AreEqual(0, context.Http.RedirectsFollowed);
    }

    [TestMethod]
    [DataRow("-", true, true)]
    [DataRow("0", true, false)]
    [DataRow("-", false, false)]
    public void Create_ContinueAt_ResumesAnUploadFromAnUnknownOffsetOnlyForDashWithAnUpload(string continueAt, bool withUpload, bool expected)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using MemoryStream upload = new();
        CommandLineOptions options = Parse("-C", continueAt, "-T", "f.txt", "http://example.com/up");

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(options, CurlUrl.Parse("http://example.com/up"), output, null, null, null, upload: withUpload ? upload : null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.ResumeUploadFromUnknownOffset", expected, context.ResumeUploadFromUnknownOffset);
        Assert.AreEqual(expected, context.ResumeUploadFromUnknownOffset);
    }

    [TestMethod]
    public void Create_TelnetUrl_UploadsStandardInput()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("telnet://example.com/"), CurlUrl.Parse("telnet://example.com/"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Upload is standardInput", true, ReferenceEquals(standardInput, context.Upload));
        Assert.AreSame(standardInput, context.Upload);
    }

    [TestMethod]
    [DataRow("tftp://example.com/x")]
    [DataRow("TELNETS://example.com/")]
    public void Create_OtherScheme_UploadsNothing(string url)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse(url), CurlUrl.Parse(url), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Upload", "null", context.Upload is null ? "null" : "set");
        Assert.IsNull(context.Upload);
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> as if every path exists and returns the accepted options.
    /// </summary>
    [TestMethod]
    public void Create_AbortTokenWithoutWatchdog_IsTheContextsToken()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using CancellationTokenSource abort = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null, abortToken: abort.Token);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.CancellationToken", abort.Token, context.CancellationToken);
        Assert.AreEqual(abort.Token, context.CancellationToken);
    }

    [TestMethod]
    public void Create_WatchdogWithoutAbortToken_IsTheWatchdogsToken()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using LowSpeedWatchdog watchdog = new(1, TimeSpan.FromHours(1), TimeProvider.System);

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null, lowSpeedWatchdog: watchdog);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.CancellationToken", watchdog.Token, context.CancellationToken);
        Assert.AreEqual(watchdog.Token, context.CancellationToken);
    }

    [TestMethod]
    public void Create_WatchdogAndAbortToken_IsCancelledByTheAbort()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using LowSpeedWatchdog watchdog = new(1, TimeSpan.FromHours(1), TimeProvider.System);
        using CancellationTokenSource abort = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null, lowSpeedWatchdog: watchdog, abortToken: abort.Token);
        Diagnostics.Act("context URL", context.Url.OriginalString);
        abort.Cancel();

        Diagnostics.Assert("context.CancellationToken.IsCancellationRequested", true, context.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(context.CancellationToken.IsCancellationRequested);
        Diagnostics.Assert("watchdog.Token.IsCancellationRequested", false, watchdog.Token.IsCancellationRequested);
        Assert.IsFalse(watchdog.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Create_MaxTimeWatchdogAlone_IsTheWatchdogsTokenAndStart()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        SteppingTimeProvider clock = new();
        clock.Advance(TimeSpan.FromSeconds(4));
        using MaxTimeWatchdog watchdog = new(TimeSpan.FromHours(1), clock);

        TransferContext context = new TransferContextFactory(standardInput, clock)
            .Create(Parse("dict://h/d:x"), CurlUrl.Parse("dict://h/d:x"), output, null, null, null, maxTimeWatchdog: watchdog);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.CancellationToken", watchdog.Token, context.CancellationToken);
        Assert.AreEqual(watchdog.Token, context.CancellationToken);
        Diagnostics.Assert("context.OperationStarted", TimeSpan.FromSeconds(4).Ticks, context.OperationStarted);
        Assert.AreEqual(TimeSpan.FromSeconds(4).Ticks, context.OperationStarted);
    }

    [TestMethod]
    public void Create_NoMaxTimeWatchdog_LeavesOperationStartedUnset()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("dict://h/d:x"), CurlUrl.Parse("dict://h/d:x"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.OperationStarted", "null", context.OperationStarted is null ? "null" : "set");
        Assert.IsNull(context.OperationStarted);
    }

    [TestMethod]
    public void Create_BothWatchdogsAndAbortToken_IsCancelledByTheMaxTimeWatchdogAndReportsToBoth()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        SteppingTimeProvider clock = new();
        using LowSpeedWatchdog lowSpeed = new(1, TimeSpan.FromHours(1), clock);
        using MaxTimeWatchdog maxTime = new(TimeSpan.FromSeconds(1), clock);
        using CancellationTokenSource abort = new();

        TransferContext context = new TransferContextFactory(standardInput, clock)
            .Create(Parse("dict://h/d:x"), CurlUrl.Parse("dict://h/d:x"), output, null, null, null, lowSpeedWatchdog: lowSpeed, abortToken: abort.Token, maxTimeWatchdog: maxTime);
        Diagnostics.Act("context URL", context.Url.OriginalString);
        context.Progress.ReportTransferStarted();
        context.Progress.ReportDownloaded(7, null);
        clock.Advance(TimeSpan.FromSeconds(1));

        Diagnostics.Assert("context.CancellationToken.IsCancellationRequested", true, context.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(context.CancellationToken.IsCancellationRequested);
        Diagnostics.Assert("abort.Token.IsCancellationRequested", false, abort.Token.IsCancellationRequested);
        Assert.IsFalse(abort.Token.IsCancellationRequested);
        Diagnostics.Assert("maxTime.Failure.ErrorMessage", "Operation timed out after 1000 milliseconds with 7 bytes received", maxTime.Failure.ErrorMessage);
        Assert.AreEqual("Operation timed out after 1000 milliseconds with 7 bytes received", maxTime.Failure.ErrorMessage);
    }

    [TestMethod]
    public void Create_DumpHeader_DumpHeaderOutputIsTheDumpHeaderStream()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using MemoryStream dumpHeader = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("-D", "h.txt", "gopher://h/1sel"), CurlUrl.Parse("gopher://h/1sel"), output, null, null, dumpHeader);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.DumpHeaderOutput is dumpHeader", true, ReferenceEquals(dumpHeader, context.DumpHeaderOutput));
        Assert.AreSame(dumpHeader, context.DumpHeaderOutput);
    }

    [TestMethod]
    public void Create_IncludeAlone_DumpHeaderOutputIsNull()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("-i", "gopher://h/1sel"), CurlUrl.Parse("gopher://h/1sel"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.HeaderOutput", "set", context.HeaderOutput is null ? "null" : "set");
        Assert.IsNotNull(context.HeaderOutput);
        Diagnostics.Assert("context.DumpHeaderOutput", "null", context.DumpHeaderOutput is null ? "null" : "set");
        Assert.IsNull(context.DumpHeaderOutput);
    }

    [TestMethod]
    public void Create_NeitherDumpHeaderNorInclude_DumpHeaderOutputIsNull()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("gopher://h/1sel"), CurlUrl.Parse("gopher://h/1sel"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.DumpHeaderOutput", "null", context.DumpHeaderOutput is null ? "null" : "set");
        Assert.IsNull(context.DumpHeaderOutput);
    }

    [TestMethod]
    public void Create_DumpHeaderWithInclude_DumpHeaderOutputIsTheDumpHeaderStreamNotStandardOutput()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using MemoryStream dumpHeader = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("-D", "h.txt", "-i", "ftp://h/"), CurlUrl.Parse("ftp://h/"), output, null, null, dumpHeader);
        Diagnostics.Act("context URL", context.Url.OriginalString);
        context.DumpHeaderOutput!.Write("220 hi\r\n"u8);

        Diagnostics.Assert("context.DumpHeaderOutput is dumpHeader", true, ReferenceEquals(dumpHeader, context.DumpHeaderOutput));
        Assert.AreSame(dumpHeader, context.DumpHeaderOutput);
        Diagnostics.Assert("output.Length", 0L, output.Length);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public void Create_DumpHeader_WritesToDumpHeaderOutputAndHeaderOutputLandInOrder()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using MemoryStream dumpHeader = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("-D", "h.txt", "ftp://h/"), CurlUrl.Parse("ftp://h/"), output, null, null, dumpHeader);
        Diagnostics.Act("context URL", context.Url.OriginalString);
        context.DumpHeaderOutput!.Write("220 hi\r\n"u8);
        context.HeaderOutput!.Write("Content-Length: 3\r\n"u8);
        context.DumpHeaderOutput.Write("226 done\r\n"u8);

        Diagnostics.Assert("System.Text.Encoding.ASCII.GetString(dumpHeader.ToArray())", "220 hi\r\nContent-Length: 3\r\n226 done\r\n", System.Text.Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Assert.AreEqual("220 hi\r\nContent-Length: 3\r\n226 done\r\n", System.Text.Encoding.ASCII.GetString(dumpHeader.ToArray()));
    }

    // curl 8.21.0 tunnels through an HTTP proxy when --connect-to sends the URL to another host or
    // port (lib/url.c parse_connect_to_slist; upstream tests 2050 and 2055).
    [TestMethod]
    [DataRow("::connect.example:8990", ProxyKind.Http, true)]
    [DataRow("::CONNECT.example:80", ProxyKind.Http10, true)]
    [DataRow("::www.example:8990", ProxyKind.Https, true)]
    [DataRow("::WWW.example:80", ProxyKind.Http, false)]
    [DataRow("other:80:connect.example:8990", ProxyKind.Http, false)]
    [DataRow("::connect.example:x", ProxyKind.Http, false)]
    [DataRow("::connect.example:8990", ProxyKind.Socks5, false)]
    public void ConnectToTunnelsThroughProxy_ForAMappingAndProxy_TunnelsOnlyWhenTheHostOrPortChangesThroughAnHttpProxy(string mapping, ProxyKind kind, bool expected)
    {
        CommandLineOptions options = Parse("--connect-to", mapping, "http://www.example/");

        bool tunnels = TransferContextFactory.ConnectToTunnelsThroughProxy(options, CurlUrl.Parse("http://www.example/"), new ProxyEndpoint(kind, "127.0.0.1", 8991, null));

        Diagnostics.Assert("tunnels", expected, tunnels);
        Assert.AreEqual(expected, tunnels);
    }

    [TestMethod]
    public void ConnectToTunnelsThroughProxy_WithoutAProxyOrAMapping_DoesNotTunnel()
    {
        CurlUrl url = CurlUrl.Parse("http://www.example/");

        bool withoutProxy = TransferContextFactory.ConnectToTunnelsThroughProxy(Parse("--connect-to", "::other:1", "http://www.example/"), url, null);
        bool withoutMapping = TransferContextFactory.ConnectToTunnelsThroughProxy(Parse("http://www.example/"), url, new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 8991, null));

        Diagnostics.Assert("tunnels without proxy, without mapping", (false, false), (withoutProxy, withoutMapping));
        Assert.IsFalse(withoutProxy);
        Assert.IsFalse(withoutMapping);
    }

    [TestMethod]
    public void Create_WithAConnectToMappingThroughAnHttpProxy_SetsProxyTunnel()
    {
        using var standardInput = new MemoryStream();
        using var output = new MemoryStream();

        TransferContext context = new TransferContextFactory(standardInput).Create(
            Parse("--connect-to", "::connect.example:8990", "http://www.example/"),
            CurlUrl.Parse("http://www.example/"),
            output,
            null,
            null,
            null,
            proxy: new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 8991, null));

        Diagnostics.Assert("proxy tunnel", true, context.Http!.ProxyTunnel);
        Assert.IsTrue(context.Http!.ProxyTunnel);
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("command line", string.Join(' ', arguments));
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
