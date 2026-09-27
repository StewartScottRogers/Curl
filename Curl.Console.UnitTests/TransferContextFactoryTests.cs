using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins how <see cref="TransferContextFactory" /> copies the parsed command line, the URL and
/// the resolved per-transfer values into a <see cref="TransferContext" />, and that only a
/// <c>telnet</c> transfer uploads standard input.
/// </summary>
[TestClass]
public sealed class TransferContextFactoryTests
{
    [TestMethod]
    public void Create_PerTransferValues_AreCopiedAsGiven()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using MemoryStream headerOutput = new();
        CurlUrl url = CurlUrl.Parse("file:///C:/x.txt");
        ByteRange range = ByteRange.Bounded(2, 5);

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///C:/x.txt"), url, output, range, 7, headerOutput);

        Assert.AreSame(url, context.Url);
        Assert.AreSame(output, context.Output);
        Assert.AreSame(headerOutput, context.HeaderOutput);
        Assert.AreEqual(range, context.Range);
        Assert.AreEqual(7L, context.ResumeFrom);
    }

    [TestMethod]
    public void Create_Progress_IsTheSinkGiven()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        TransferProgressRecorder progress = new(new ManualTimeProvider());

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///C:/x.txt"), CurlUrl.Parse("file:///C:/x.txt"), output, null, null, null, progress: progress);

        Assert.AreSame(progress, context.Progress);
    }

    [TestMethod]
    public void Create_ClockGiven_IsTheContextsTimeProvider()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        ManualTimeProvider clock = new();

        TransferContext context = new TransferContextFactory(standardInput, clock)
            .Create(Parse("file:///C:/x.txt"), CurlUrl.Parse("file:///C:/x.txt"), output, null, null, null);

        Assert.AreSame(clock, context.TimeProvider);
    }

    [TestMethod]
    public void Create_NoClock_IsTheSystemClock()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///C:/x.txt"), CurlUrl.Parse("file:///C:/x.txt"), output, null, null, null);

        Assert.AreSame(TimeProvider.System, context.TimeProvider);
    }

    [TestMethod]
    public void Create_NoProgress_IsNoTransferProgress()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///C:/x.txt"), CurlUrl.Parse("file:///C:/x.txt"), output, null, null, null);

        Assert.AreSame(NoTransferProgress.Instance, context.Progress);
    }

    [TestMethod]
    public void Create_NoOptions_LeavesEveryOptionAtItsDefault()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("file:///C:/x.txt"), CurlUrl.Parse("file:///C:/x.txt"), output, null, null, null);

        Assert.IsNull(context.HeaderOutput);
        Assert.IsNull(context.Range);
        Assert.IsNull(context.ResumeFrom);
        Assert.IsNull(context.MaxFileSize);
        Assert.IsNull(context.Upload);
        Assert.IsNull(context.PostData);
        Assert.IsNull(context.Credentials);
        Assert.IsEmpty(context.TelnetOptions);
        Assert.IsNull(context.TftpBlockSize);
        Assert.IsFalse(context.TftpNoOptions);
        Assert.AreEqual(TransferContext.DefaultCreateFileMode, context.CreateFileMode);
        Assert.IsNull(context.ConnectTimeout);
        Assert.IsNull(context.MaxTime);
        Assert.IsNull(context.TimeCondition);
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
            "--create-file-mode", "0600",
            "--connect-timeout", "3",
            "--path-as-is",
            "-m", "9",
            "-z", "Sun, 06 Nov 1994 08:49:37 GMT",
            "tftp://example.com/x");

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(options, CurlUrl.Parse("tftp://example.com/x"), output, null, null, null);

        Assert.AreEqual(100L, context.MaxFileSize);
        Assert.AreEqual("a=b", System.Text.Encoding.ASCII.GetString(context.PostData!.Value.Span));
        Assert.AreEqual("user", context.Credentials!.UserName);
        Assert.AreEqual("secret", context.Credentials.Password);
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100" }, context.TelnetOptions.ToArray());
        Assert.AreEqual(1024, context.TftpBlockSize);
        Assert.IsTrue(context.TftpNoOptions);
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, context.CreateFileMode);
        Assert.AreEqual(TimeSpan.FromSeconds(3), context.ConnectTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(9), context.MaxTime);
        Assert.IsTrue(context.PathAsIs);
        Assert.AreEqual(options.TimeCondition, context.TimeCondition);
        Assert.IsNotNull(context.TimeCondition);
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

        Assert.AreEqual("PATCH", context.Http!.CustomMethod);
        CollectionAssert.AreEqual(new[] { "X: 1" }, context.Http.Headers.ToArray());
        CollectionAssert.AreEqual(new[] { "X-P: 1", "X-Q: 2" }, context.Http.ProxyHeaders.ToArray());
        Assert.AreEqual("a/1", context.Http.UserAgent);
        Assert.AreEqual("http://r/", context.Http.Referer);
        BytesBody body = (BytesBody)context.Http.Body!;
        Assert.AreEqual("a=b", System.Text.Encoding.ASCII.GetString(body.Content.Span));
        Assert.AreEqual(HttpRequestOptionsMapping.FormUrlEncoded, body.ContentType);
    }

    [TestMethod]
    public void Create_TelnetUrl_UploadsStandardInput()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(Parse("telnet://example.com/"), CurlUrl.Parse("telnet://example.com/"), output, null, null, null);

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

        Assert.IsNull(context.Upload);
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> as if every path exists and returns the accepted options.
    /// </summary>
    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
