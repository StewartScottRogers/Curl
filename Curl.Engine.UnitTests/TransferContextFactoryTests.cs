using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using EasyFtpFileMethod = Curl.Api.Options.Protocols.FtpFileMethod;
using EasyFtpOptions = Curl.Api.Options.Protocols.EasyFtpOptions;
using EasyProtocolOptions = Curl.Api.Options.Protocols.EasyProtocolOptions;
using EasyTelnetOptions = Curl.Api.Options.Protocols.EasyTelnetOptions;
using EasyTftpOptions = Curl.Api.Options.Protocols.EasyTftpOptions;
using FtpCreateMissingDirectories = Curl.Api.Options.Protocols.FtpCreateMissingDirectories;
using FtpFileMethod = Curl.Protocol.Abstractions.FtpFileMethod;
using TimeCondition = Curl.Api.Options.Behaviour.TimeCondition;

namespace Curl.Engine;

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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), url, output, range, 7, headerOutput);
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
        ITransferProgress progress = new SilentTransferProgress();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null, progress: progress);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.BehaviourOf(options), EasyOptionsFromCommandLine.TransferOf(options), EasyOptionsFromCommandLine.ProtocolOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options), EasyOptionsFromCommandLine.NetworkOf(options), CurlUrl.Parse("tftp://example.com/x"), output, null, null, null);
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
    [DataRow(TimeCondition.IfModifiedSince, TimeConditionKind.IfModifiedSince)]
    [DataRow(TimeCondition.IfUnmodifiedSince, TimeConditionKind.IfUnmodifiedSince)]
    public void Create_TimeConditionPastYear9999_KeepsItsWholeSeconds(TimeCondition condition, TimeConditionKind expectedKind)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        const long pastYear9999 = 253402300800 + 86400;
        EasyTransferOptions transfer = new() { TimeCondition = condition, TimeValue = pastYear9999 };
        Diagnostics.Arrange("transfer options", $"{condition} {pastYear9999}");

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("http://h/")), HttpOf("http://h/"), BehaviourOf("http://h/"), transfer, new EasyProtocolOptions(), new EasyProxyOptions(), new EasyAuthenticationOptions(), new EasyNetworkOptions(), CurlUrl.Parse("http://h/"), output, null, null, null);
        Diagnostics.Act("context.TimeCondition", context.TimeCondition);

        Diagnostics.Assert("seconds and kind", $"{pastYear9999} {expectedKind}", $"{context.TimeCondition?.ValueUnixSeconds} {context.TimeCondition?.Kind}");
        Assert.AreEqual(pastYear9999, context.TimeCondition?.ValueUnixSeconds);
        Assert.AreEqual(expectedKind, context.TimeCondition?.Kind);
    }

    [TestMethod]
    public void Create_ZeroMaxFileSizeAndTimeout_GiveNoLimitAsLibcurlsZeroDoes()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        string[] arguments = ["--max-filesize", "0", "-m", "0", "smtp://h/"];

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse(arguments)), HttpOf(arguments), BehaviourOf(arguments), TransferOf(arguments), ProtocolOf(arguments), ProxyOf(arguments), AuthenticationOf(arguments), NetworkOf(arguments), CurlUrl.Parse("smtp://h/"), output, null, null, null);
        Diagnostics.Act("MaxFileSize / MaxTime", $"{context.MaxFileSize?.ToString() ?? "null"} / {context.MaxTime?.ToString() ?? "null"}");

        Diagnostics.Assert("context.MaxFileSize", "null", context.MaxFileSize?.ToString() ?? "null");
        Assert.IsNull(context.MaxFileSize);
        Diagnostics.Assert("context.MaxTime", "null", context.MaxTime?.ToString() ?? "null");
        Assert.IsNull(context.MaxTime);
    }

    [TestMethod]
    public void Create_NoFtpActiveOrTlsOptions_IsPassiveWithEprtAndNoTls()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("ftp://example.com/f")), HttpOf("ftp://example.com/f"), BehaviourOf("ftp://example.com/f"), TransferOf("ftp://example.com/f"), ProtocolOf("ftp://example.com/f"), ProxyOf("ftp://example.com/f"), AuthenticationOf("ftp://example.com/f"), NetworkOf("ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse([.. options, "ftp://example.com/f"])), HttpOf([.. options, "ftp://example.com/f"]), BehaviourOf([.. options, "ftp://example.com/f"]), TransferOf([.. options, "ftp://example.com/f"]), ProtocolOf([.. options, "ftp://example.com/f"]), ProxyOf([.. options, "ftp://example.com/f"]), AuthenticationOf([.. options, "ftp://example.com/f"]), NetworkOf([.. options, "ftp://example.com/f"]), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("-P", "-", "ftp://example.com/f")), HttpOf("-P", "-", "ftp://example.com/f"), BehaviourOf("-P", "-", "ftp://example.com/f"), TransferOf("-P", "-", "ftp://example.com/f"), ProtocolOf("-P", "-", "ftp://example.com/f"), ProxyOf("-P", "-", "ftp://example.com/f"), AuthenticationOf("-P", "-", "ftp://example.com/f"), NetworkOf("-P", "-", "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.FtpPort", "-", context.FtpPort);
        Assert.AreEqual("-", context.FtpPort);
    }

    [TestMethod]
    [DataRow(EasyFtpFileMethod.Default, FtpFileMethod.MultiCwd)]
    [DataRow(EasyFtpFileMethod.MultiCwd, FtpFileMethod.MultiCwd)]
    [DataRow(EasyFtpFileMethod.NoCwd, FtpFileMethod.NoCwd)]
    [DataRow(EasyFtpFileMethod.SingleCwd, FtpFileMethod.SingleCwd)]
    public void Create_FtpFileMethod_IsTheHandlersPathWalk(EasyFtpFileMethod method, FtpFileMethod expected)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        EasyProtocolOptions protocol = new() { Ftp = new EasyFtpOptions { FileMethod = method } };

        TransferContext context = CreateFtpContext(standardInput, output, protocol);

        Diagnostics.Assert("context.FtpFileMethod", expected, context.FtpFileMethod);
        Assert.AreEqual(expected, context.FtpFileMethod);
    }

    [TestMethod]
    [DataRow(FtpCreateMissingDirectories.None, false)]
    [DataRow(FtpCreateMissingDirectories.Create, true)]
    [DataRow(FtpCreateMissingDirectories.CreateAndRetry, true)]
    public void Create_FtpCreateMissingDirectories_CreatesDirectoriesUnlessNone(FtpCreateMissingDirectories create, bool expected)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        EasyProtocolOptions protocol = new() { Ftp = new EasyFtpOptions { CreateMissingDirectories = create } };

        TransferContext context = CreateFtpContext(standardInput, output, protocol);

        Diagnostics.Assert("context.FtpCreateDirectories", expected, context.FtpCreateDirectories);
        Assert.AreEqual(expected, context.FtpCreateDirectories);
    }

    [TestMethod]
    public void Create_QuotePreQuoteAndPostQuote_AreOneListMarkedByPrefix()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        EasyProtocolOptions protocol = new()
        {
            Ftp = new EasyFtpOptions { Quote = ["NOOP", "*SITE x"], PreQuote = ["PRE", "*-y"], PostQuote = ["POST"] },
        };

        TransferContext context = CreateFtpContext(standardInput, output, protocol);

        string[] expected = ["NOOP", "*SITE x", "+PRE", "+*-y", "-POST"];
        Diagnostics.Assert("context.QuoteCommands", string.Join(" | ", expected), string.Join(" | ", context.QuoteCommands));
        CollectionAssert.AreEqual(expected, context.QuoteCommands.ToArray());
    }

    [TestMethod]
    public void Create_FtpOptions_AreCopiedFromTheProtocolGroup()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        EasyProtocolOptions protocol = new()
        {
            Ftp = new EasyFtpOptions
            {
                ActiveAddress = "-",
                UseEprt = false,
                UseEpsv = false,
                UsePret = true,
                SkipPasvIp = false,
                Account = "acct",
                AlternativeToUser = "USER alt",
                Append = true,
            },
        };

        TransferContext context = CreateFtpContext(standardInput, output, protocol);

        string actual = $"{context.FtpPort} {context.FtpUseEprt} {context.FtpDisableEpsv} {context.FtpSendPret} {context.FtpSkipPasvIp} {context.FtpAccount} {context.FtpAlternativeToUser} {context.Append}";
        Diagnostics.Assert("FTP fields", "- False True True False acct USER alt True", actual);
        Assert.AreEqual("- False True True False acct USER alt True", actual);
    }

    [TestMethod]
    public void Create_TftpAndTelnetOptions_AreCopiedFromTheProtocolGroup()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        EasyProtocolOptions protocol = new()
        {
            Tftp = new EasyTftpOptions { BlockSize = 1024, NoOptions = true },
            Telnet = new EasyTelnetOptions { TelnetOptions = ["TTYPE=vt100"] },
        };

        TransferContext context = CreateFtpContext(standardInput, output, protocol);

        Diagnostics.Assert("TFTP block size, no options, telnet options", "1024 True TTYPE=vt100", $"{context.TftpBlockSize} {context.TftpNoOptions} {string.Join(",", context.TelnetOptions)}");
        Assert.AreEqual(1024, context.TftpBlockSize);
        Assert.IsTrue(context.TftpNoOptions);
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100" }, context.TelnetOptions.ToArray());
    }

    [TestMethod]
    public void Create_DisableEprt_TurnsFtpUseEprtOff()
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("--disable-eprt", "ftp://example.com/f")), HttpOf("--disable-eprt", "ftp://example.com/f"), BehaviourOf("--disable-eprt", "ftp://example.com/f"), TransferOf("--disable-eprt", "ftp://example.com/f"), ProtocolOf("--disable-eprt", "ftp://example.com/f"), ProxyOf("--disable-eprt", "ftp://example.com/f"), AuthenticationOf("--disable-eprt", "ftp://example.com/f"), NetworkOf("--disable-eprt", "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse(option, "ftp://example.com/f")), HttpOf(option, "ftp://example.com/f"), BehaviourOf(option, "ftp://example.com/f"), TransferOf(option, "ftp://example.com/f"), ProtocolOf(option, "ftp://example.com/f"), ProxyOf(option, "ftp://example.com/f"), AuthenticationOf(option, "ftp://example.com/f"), NetworkOf(option, "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("--ftp-ssl-control", "ftp://example.com/f")), HttpOf("--ftp-ssl-control", "ftp://example.com/f"), BehaviourOf("--ftp-ssl-control", "ftp://example.com/f"), TransferOf("--ftp-ssl-control", "ftp://example.com/f"), ProtocolOf("--ftp-ssl-control", "ftp://example.com/f"), ProxyOf("--ftp-ssl-control", "ftp://example.com/f"), AuthenticationOf("--ftp-ssl-control", "ftp://example.com/f"), NetworkOf("--ftp-ssl-control", "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.FtpSslControlOnly", true, context.FtpSslControlOnly);
        Assert.IsTrue(context.FtpSslControlOnly);
        Diagnostics.Assert("context.SslLevel", TransportSecurityLevel.Required, context.SslLevel);
        Assert.AreEqual(TransportSecurityLevel.Required, context.SslLevel);
    }

    [TestMethod]
    [DataRow("--ftp-ssl-control", "--ssl-reqd")]
    [DataRow("--ssl-reqd", "--ftp-ssl-control")]
    public void Create_SslReqdBesideFtpSslControl_IsRequiredForEveryConnection(string first, string second)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();

        // --ssl-reqd outranks --ftp-ssl-control (ADR-0102), so FTP sends PROT P, not PROT C.
        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse(first, second, "ftp://example.com/f")), HttpOf(first, second, "ftp://example.com/f"), BehaviourOf(first, second, "ftp://example.com/f"), TransferOf(first, second, "ftp://example.com/f"), ProtocolOf(first, second, "ftp://example.com/f"), ProxyOf(first, second, "ftp://example.com/f"), AuthenticationOf(first, second, "ftp://example.com/f"), NetworkOf(first, second, "ftp://example.com/f"), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.FtpSslControlOnly", false, context.FtpSslControlOnly);
        Assert.IsFalse(context.FtpSslControlOnly);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.BehaviourOf(options), EasyOptionsFromCommandLine.TransferOf(options), EasyOptionsFromCommandLine.ProtocolOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options), EasyOptionsFromCommandLine.NetworkOf(options), CurlUrl.Parse("http://example.com/"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.BehaviourOf(options), EasyOptionsFromCommandLine.TransferOf(options), EasyOptionsFromCommandLine.ProtocolOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options), EasyOptionsFromCommandLine.NetworkOf(options), CurlUrl.Parse("http://example.com/"), output, null, null, null);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Http!.MaxRedirects", expected, context.Http!.MaxRedirects);
        Assert.AreEqual(expected, context.Http!.MaxRedirects);
        Diagnostics.Assert("context.Http.RedirectsFollowed", 0, context.Http.RedirectsFollowed);
        Assert.AreEqual(0, context.Http.RedirectsFollowed);
    }

    [TestMethod]
    [DataRow(-1L, true, true, true)]
    [DataRow(0L, true, true, false)]
    [DataRow(-1L, false, true, false)]
    [DataRow(-1L, true, false, false)]
    public void Create_UploadResumingFromMinusOne_ResumesFromAnUnknownOffsetOnlyWithAnUploadStream(long resumeFrom, bool upload, bool withStream, bool expected)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        using MemoryStream uploadStream = new();
        CommandLineOptions options = Parse("-T", "f.txt", "http://example.com/up");
        EasyTransferOptions transfer = new() { Upload = upload, ResumeFrom = resumeFrom };
        Diagnostics.Arrange("transfer options", $"Upload={upload} ResumeFrom={resumeFrom} stream={withStream}");

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.BehaviourOf(options), transfer, new EasyProtocolOptions(), new EasyProxyOptions(), new EasyAuthenticationOptions(), new EasyNetworkOptions(), CurlUrl.Parse("http://example.com/up"), output, null, null, null, upload: withStream ? uploadStream : null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("telnet://example.com/")), HttpOf("telnet://example.com/"), BehaviourOf("telnet://example.com/"), TransferOf("telnet://example.com/"), ProtocolOf("telnet://example.com/"), ProxyOf("telnet://example.com/"), AuthenticationOf("telnet://example.com/"), NetworkOf("telnet://example.com/"), CurlUrl.Parse("telnet://example.com/"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse(url)), HttpOf(url), BehaviourOf(url), TransferOf(url), ProtocolOf(url), ProxyOf(url), AuthenticationOf(url), NetworkOf(url), CurlUrl.Parse(url), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null, abortToken: abort.Token);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null, lowSpeedWatchdog: watchdog);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("file:///x.txt")), HttpOf("file:///x.txt"), BehaviourOf("file:///x.txt"), TransferOf("file:///x.txt"), ProtocolOf("file:///x.txt"), ProxyOf("file:///x.txt"), AuthenticationOf("file:///x.txt"), NetworkOf("file:///x.txt"), CurlUrl.Parse("file:///x.txt"), output, null, null, null, lowSpeedWatchdog: watchdog, abortToken: abort.Token);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("dict://h/d:x")), HttpOf("dict://h/d:x"), BehaviourOf("dict://h/d:x"), TransferOf("dict://h/d:x"), ProtocolOf("dict://h/d:x"), ProxyOf("dict://h/d:x"), AuthenticationOf("dict://h/d:x"), NetworkOf("dict://h/d:x"), CurlUrl.Parse("dict://h/d:x"), output, null, null, null, maxTimeWatchdog: watchdog);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("dict://h/d:x")), HttpOf("dict://h/d:x"), BehaviourOf("dict://h/d:x"), TransferOf("dict://h/d:x"), ProtocolOf("dict://h/d:x"), ProxyOf("dict://h/d:x"), AuthenticationOf("dict://h/d:x"), NetworkOf("dict://h/d:x"), CurlUrl.Parse("dict://h/d:x"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("dict://h/d:x")), HttpOf("dict://h/d:x"), BehaviourOf("dict://h/d:x"), TransferOf("dict://h/d:x"), ProtocolOf("dict://h/d:x"), ProxyOf("dict://h/d:x"), AuthenticationOf("dict://h/d:x"), NetworkOf("dict://h/d:x"), CurlUrl.Parse("dict://h/d:x"), output, null, null, null, lowSpeedWatchdog: lowSpeed, abortToken: abort.Token, maxTimeWatchdog: maxTime);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("-D", "h.txt", "gopher://h/1sel")), HttpOf("-D", "h.txt", "gopher://h/1sel"), BehaviourOf("-D", "h.txt", "gopher://h/1sel"), TransferOf("-D", "h.txt", "gopher://h/1sel"), ProtocolOf("-D", "h.txt", "gopher://h/1sel"), ProxyOf("-D", "h.txt", "gopher://h/1sel"), AuthenticationOf("-D", "h.txt", "gopher://h/1sel"), NetworkOf("-D", "h.txt", "gopher://h/1sel"), CurlUrl.Parse("gopher://h/1sel"), output, null, null, dumpHeader);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("-i", "gopher://h/1sel")), HttpOf("-i", "gopher://h/1sel"), BehaviourOf("-i", "gopher://h/1sel"), TransferOf("-i", "gopher://h/1sel"), ProtocolOf("-i", "gopher://h/1sel"), ProxyOf("-i", "gopher://h/1sel"), AuthenticationOf("-i", "gopher://h/1sel"), NetworkOf("-i", "gopher://h/1sel"), CurlUrl.Parse("gopher://h/1sel"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("gopher://h/1sel")), HttpOf("gopher://h/1sel"), BehaviourOf("gopher://h/1sel"), TransferOf("gopher://h/1sel"), ProtocolOf("gopher://h/1sel"), ProxyOf("gopher://h/1sel"), AuthenticationOf("gopher://h/1sel"), NetworkOf("gopher://h/1sel"), CurlUrl.Parse("gopher://h/1sel"), output, null, null, null);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("-D", "h.txt", "-i", "ftp://h/")), HttpOf("-D", "h.txt", "-i", "ftp://h/"), BehaviourOf("-D", "h.txt", "-i", "ftp://h/"), TransferOf("-D", "h.txt", "-i", "ftp://h/"), ProtocolOf("-D", "h.txt", "-i", "ftp://h/"), ProxyOf("-D", "h.txt", "-i", "ftp://h/"), AuthenticationOf("-D", "h.txt", "-i", "ftp://h/"), NetworkOf("-D", "h.txt", "-i", "ftp://h/"), CurlUrl.Parse("ftp://h/"), output, null, null, dumpHeader);
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
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("-D", "h.txt", "ftp://h/")), HttpOf("-D", "h.txt", "ftp://h/"), BehaviourOf("-D", "h.txt", "ftp://h/"), TransferOf("-D", "h.txt", "ftp://h/"), ProtocolOf("-D", "h.txt", "ftp://h/"), ProxyOf("-D", "h.txt", "ftp://h/"), AuthenticationOf("-D", "h.txt", "ftp://h/"), NetworkOf("-D", "h.txt", "ftp://h/"), CurlUrl.Parse("ftp://h/"), output, null, null, dumpHeader);
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
        EasyNetworkOptions network = new() { ConnectTo = [mapping] };

        bool tunnels = TransferContextFactory.ConnectToTunnelsThroughProxy(network, CurlUrl.Parse("http://www.example/"), new ProxyEndpoint(kind, "127.0.0.1", 8991, null));

        Diagnostics.Assert("tunnels", expected, tunnels);
        Assert.AreEqual(expected, tunnels);
    }

    [TestMethod]
    public void ConnectToTunnelsThroughProxy_WithoutAProxyOrAMapping_DoesNotTunnel()
    {
        CurlUrl url = CurlUrl.Parse("http://www.example/");

        bool withoutProxy = TransferContextFactory.ConnectToTunnelsThroughProxy(new EasyNetworkOptions { ConnectTo = ["::other:1"] }, url, null);
        bool withoutMapping = TransferContextFactory.ConnectToTunnelsThroughProxy(new EasyNetworkOptions(), url, new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 8991, null));

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
            EasyOptionsFromCommandLine.TransferSettingsOf(Parse("--connect-to", "::connect.example:8990", "http://www.example/")),
            HttpOf("--connect-to", "::connect.example:8990", "http://www.example/"),
            new EasyBehaviourOptions(),
            new EasyTransferOptions(),
            new EasyProtocolOptions(),
            new EasyProxyOptions(),
            new EasyAuthenticationOptions(),
            NetworkOf("--connect-to", "::connect.example:8990", "http://www.example/"),
            CurlUrl.Parse("http://www.example/"),
            output,
            null,
            null,
            null,
            proxy: new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 8991, null));

        Diagnostics.Assert("proxy tunnel", true, context.Http!.ProxyTunnel);
        Assert.IsTrue(context.Http!.ProxyTunnel);
    }

    // The record's ConnectTimeout of zero means not set (ADR-0513), which the context carries as null.
    [TestMethod]
    [DataRow(0, null)]
    [DataRow(2500, 2500)]
    public void Create_NetworkOptionsConnectTimeout_IsTheContextsConnectTimeoutWithZeroAsNull(int milliseconds, int? expectedMilliseconds)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        EasyNetworkOptions network = new() { ConnectTimeout = TimeSpan.FromMilliseconds(milliseconds) };
        Diagnostics.Arrange("network.ConnectTimeout", network.ConnectTimeout);

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("dict://h/d:x")), HttpOf("dict://h/d:x"), new EasyBehaviourOptions(), new EasyTransferOptions(), new EasyProtocolOptions(), new EasyProxyOptions(), new EasyAuthenticationOptions(), network, CurlUrl.Parse("dict://h/d:x"), output, null, null, null);
        Diagnostics.Act("context.ConnectTimeout", context.ConnectTimeout);

        TimeSpan? expected = expectedMilliseconds is { } given ? TimeSpan.FromMilliseconds(given) : null;
        Diagnostics.Assert("context.ConnectTimeout", expected, context.ConnectTimeout);
        Assert.AreEqual(expected, context.ConnectTimeout);
    }

    // CURLOPT_ACCEPTTIMEOUT_MS reaches the FTP handler; zero is libcurl's not set, curl's 60 seconds (BL-2622).
    [TestMethod]
    [DataRow(0, 60000)]
    [DataRow(8000, 8000)]
    public void Create_NetworkOptionsAcceptTimeout_IsTheContextsFtpAcceptTimeoutWithZeroAsSixtySeconds(int milliseconds, int expectedMilliseconds)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        EasyNetworkOptions network = new() { AcceptTimeout = TimeSpan.FromMilliseconds(milliseconds) };
        Diagnostics.Arrange("network.AcceptTimeout", network.AcceptTimeout);

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("ftp://h/f")), HttpOf("ftp://h/f"), new EasyBehaviourOptions(), new EasyTransferOptions(), new EasyProtocolOptions(), new EasyProxyOptions(), new EasyAuthenticationOptions(), network, CurlUrl.Parse("ftp://h/f"), output, null, null, null);
        Diagnostics.Act("context.FtpAcceptTimeout", context.FtpAcceptTimeout);

        Diagnostics.Assert("context.FtpAcceptTimeout", TimeSpan.FromMilliseconds(expectedMilliseconds), context.FtpAcceptTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), context.FtpAcceptTimeout);
    }

    // CURLOPT_SERVER_RESPONSE_TIMEOUT reaches the FTP handler; zero is libcurl's not set, curl's 60 seconds (BL-2724).
    [TestMethod]
    [DataRow(0, 60000)]
    [DataRow(8000, 8000)]
    public void Create_NetworkOptionsServerResponseTimeout_IsTheContextsFtpServerResponseTimeoutWithZeroAsSixtySeconds(int milliseconds, int expectedMilliseconds)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        EasyNetworkOptions network = new() { ServerResponseTimeout = TimeSpan.FromMilliseconds(milliseconds) };
        Diagnostics.Arrange("network.ServerResponseTimeout", network.ServerResponseTimeout);

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("ftp://h/f")), HttpOf("ftp://h/f"), new EasyBehaviourOptions(), new EasyTransferOptions(), new EasyProtocolOptions(), new EasyProxyOptions(), new EasyAuthenticationOptions(), network, CurlUrl.Parse("ftp://h/f"), output, null, null, null);
        Diagnostics.Act("context.FtpServerResponseTimeout", context.FtpServerResponseTimeout);

        Diagnostics.Assert("context.FtpServerResponseTimeout", TimeSpan.FromMilliseconds(expectedMilliseconds), context.FtpServerResponseTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), context.FtpServerResponseTimeout);
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("command line", string.Join(' ', arguments));
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    [TestMethod]
    [DataRow("http://example.com/")]
    [DataRow("https://example.com/")]
    [DataRow("ws://example.com/")]
    [DataRow("wss://example.com/")]
    public void Create_SaslAuthzidWithNtlmAndNoUser_CarriesEmptyCredentials(string url)
    {
        string[] arguments = ["--sasl-authzid", "x", "--ntlm", url];

        TransferContext context = CreateContext(arguments, url);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Credentials user:password", ":", $"{context.Credentials?.UserName}:{context.Credentials?.Password}");
        Assert.AreEqual(string.Empty, context.Credentials!.UserName);
        Assert.AreEqual(string.Empty, context.Credentials.Password);
    }

    [TestMethod]
    [DataRow("http://example.com/", "--basic")]
    [DataRow("http://example.com/", "--digest")]
    [DataRow("http://example.com/", "--anyauth")]
    [DataRow("ftp://example.com/", "--ntlm")]
    public void Create_SaslAuthzidWithoutNtlmAloneOverHttp_CarriesNoCredentials(string url, string method)
    {
        string[] arguments = ["--sasl-authzid", "x", method, url];

        TransferContext context = CreateContext(arguments, url);
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Credentials", "null", context.Credentials is null ? "null" : "set");
        Assert.IsNull(context.Credentials);
    }

    [TestMethod]
    public void Create_NtlmWithoutSaslAuthzid_CarriesNoCredentials()
    {
        string[] arguments = ["--ntlm", "http://example.com/"];

        TransferContext context = CreateContext(arguments, "http://example.com/");
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Credentials", "null", context.Credentials is null ? "null" : "set");
        Assert.IsNull(context.Credentials);
    }

    [TestMethod]
    public void Create_SaslAuthzidWithNtlmAndUser_CarriesTheUser()
    {
        string[] arguments = ["--sasl-authzid", "x", "--ntlm", "-u", "bob:pw", "http://example.com/"];

        TransferContext context = CreateContext(arguments, "http://example.com/");
        Diagnostics.Act("context URL", context.Url.OriginalString);

        Diagnostics.Assert("context.Credentials!.UserName", "bob", context.Credentials!.UserName);
        Assert.AreEqual("bob", context.Credentials!.UserName);
    }

    /// <summary>Builds the context of <paramref name="url" /> under the command line <paramref name="arguments" />.</summary>
    private TransferContext CreateContext(string[] arguments, string url)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        return new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse(arguments)), HttpOf(arguments), BehaviourOf(arguments), TransferOf(arguments), ProtocolOf(arguments), ProxyOf(arguments), AuthenticationOf(arguments), NetworkOf(arguments), CurlUrl.Parse(url), output, null, null, null);
    }

    /// <summary>A progress sink that records nothing, to tell the context's sink from the one given.</summary>
    private sealed class SilentTransferProgress : ITransferProgress
    {
        public void ReportTransferStarted()
        {
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
        {
        }

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }

    private static EasyHttpOptions HttpOf(params string[] arguments) =>
        EasyOptionsFromCommandLine.HttpOf(CommandLineParser.Parse(arguments, _ => true).Options!);

    private static EasyBehaviourOptions BehaviourOf(params string[] arguments) =>
        EasyOptionsFromCommandLine.BehaviourOf(CommandLineParser.Parse(arguments, _ => true).Options!);

    private static EasyTransferOptions TransferOf(params string[] arguments) =>
        EasyOptionsFromCommandLine.TransferOf(CommandLineParser.Parse(arguments, _ => true).Options!);

    private TransferContext CreateFtpContext(Stream standardInput, Stream output, EasyProtocolOptions protocol) =>
        new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(Parse("ftp://example.com/f")), HttpOf("ftp://example.com/f"), new EasyBehaviourOptions(), new EasyTransferOptions(), protocol, new EasyProxyOptions(), new EasyAuthenticationOptions(), new EasyNetworkOptions(), CurlUrl.Parse("ftp://example.com/f"), output, null, null, null);

    private static EasyProtocolOptions ProtocolOf(params string[] arguments) =>
        EasyOptionsFromCommandLine.ProtocolOf(CommandLineParser.Parse(arguments, _ => true).Options!);

    private static EasyAuthenticationOptions AuthenticationOf(params string[] arguments) =>
        EasyOptionsFromCommandLine.AuthenticationOf(CommandLineParser.Parse(arguments, _ => true).Options!);

    private static EasyProxyOptions ProxyOf(params string[] arguments) =>
        EasyOptionsFromCommandLine.ProxyOf(CommandLineParser.Parse(arguments, _ => true).Options!);

    private static EasyNetworkOptions NetworkOf(params string[] arguments) =>
        EasyOptionsFromCommandLine.NetworkOf(CommandLineParser.Parse(arguments, _ => true).Options!);
}
