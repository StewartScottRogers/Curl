using System.Text;
using Curl.Api.Options.Http;
using Curl.Api.Options.Protocols;
using Curl.Cli;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the per-transfer setup split into <see cref="TransferPerformer" /> (ADR-0506, BL-2283): a
/// rejected URL, the proxy <c>-x</c> chooses, a missing known-hosts file, the HSTS upgrade of an
/// <c>http://</c> URL and the <c>-b</c> file loaded before the request, on fakes.
/// </summary>
public sealed partial class TransferPerformerTests
{
    private readonly TransferPerformer sshPerformer = new(TimeProvider.System, EasyErrorText);

    [TestMethod]
    public void KnownHostsFileMissingFailure_GivenEasyErrorText_Exits2WithItsTextForExit2()
    {
        TransferResult failure = sshPerformer.KnownHostsFileMissingFailure;

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(EasyErrorText(CurlExitCode.FailedInit), failure.ErrorMessage);
    }

    [TestMethod]
    public void ReportUrlRejected_UnparsableHost_Exits3AndReportsTheRejection()
    {
        CommandLineOptions options = Parse("http://exa mple.com/");
        InfoRecordingEvents events = new();

        bool parsed = TransferPerformer.TryParseTransferUrl("http://exa mple.com/", options, out _, out CurlUrlRejection rejection);
        TransferResult result = TransferPerformer.ReportUrlRejected(rejection, events);

        Assert.IsFalse(parsed);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(TransferPerformer.UrlRejectedPrefix + rejection.ToCurlMessage(), result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { result.ErrorMessage }, events.Info);
    }

    [TestMethod]
    public void ReportUrlRejected_UserUnderDisallowUsernameInUrl_Exits67()
    {
        CommandLineOptions options = Parse("--disallow-username-in-url", "http://user@example.com/");

        bool parsed = TransferPerformer.TryParseTransferUrl("http://user@example.com/", options, out _, out CurlUrlRejection rejection);
        TransferResult result = TransferPerformer.ReportUrlRejected(rejection, new InfoRecordingEvents());

        Assert.IsFalse(parsed);
        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
    }

    [TestMethod]
    public void ParsedUrlRefusal_HostLongerThanTheMaximum_Exits3()
    {
        CurlUrl url = CurlUrl.Parse("http://" + new string('a', TransferPerformer.MaximumHostLength + 1) + "/");

        TransferResult? refusal = TransferPerformer.ParsedUrlRefusal(url);

        Assert.AreEqual(CurlExitCode.UrlMalformat, refusal!.ExitCode);
        Assert.AreEqual(TransferPerformer.TooLongHostnameMessage, refusal.ErrorMessage);
        Assert.IsNull(TransferPerformer.ParsedUrlRefusal(CurlUrl.Parse(First)));
    }

    [TestMethod]
    public void TrySelectProxyAndCredentials_ProxyOption_ChoosesThatProxy()
    {
        CommandLineOptions options = Parse("-x", "http://proxy.example:3128", First);
        EngineTransferState state = NewState();

        bool selected = TransferPerformer.TrySelectProxyAndCredentials(
            HttpDispatch(), options, CurlUrl.Parse(First), state, new InfoRecordingEvents(), NoDiagnosticLog.Instance, CredentialLookup(),
            out ProxyEndpoint? proxy, out TransferResult? failure);

        Assert.IsTrue(selected);
        Assert.IsNull(failure);
        Assert.AreEqual("proxy.example", proxy!.Host);
        Assert.AreEqual(3128, proxy.Port);
    }

    [TestMethod]
    public void TrySelectProxyAndCredentials_SchemeNoHandlerServes_Exits1AndReportsIt()
    {
        CommandLineOptions options = Parse("gopher://example.com/");
        InfoRecordingEvents events = new();

        bool selected = TransferPerformer.TrySelectProxyAndCredentials(
            HttpDispatch(), options, CurlUrl.Parse("gopher://example.com/"), NewState(), events, NoDiagnosticLog.Instance, CredentialLookup(),
            out _, out TransferResult? failure);

        Assert.IsFalse(selected);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "Protocol \"gopher\" not supported" }, events.Info);
    }

    [TestMethod]
    public async Task ResolveSshOptionsAsync_NoKnownHostsFile_WritesCurlsLineAndFailsBeforeConnecting()
    {
        List<string> lines = [];
        List<(string? File, bool Missing)> recorded = [];

        TransferResult? failure = await ResolveSshAsync(Parse(SftpUrl), lines, recorded);

        Assert.AreSame(sshPerformer.KnownHostsFileMissingFailure, failure);
        CollectionAssert.AreEqual(new[] { TransferPerformer.KnownHostsFileMissingLine }, lines);
        CollectionAssert.AreEqual(new (string?, bool)[] { (null, true) }, recorded);
    }

    [TestMethod]
    public async Task ResolveSshOptionsAsync_NoKnownHostsFileUnderSilent_WritesNothingAndFails()
    {
        List<string> lines = [];

        TransferResult? failure = await ResolveSshAsync(Parse("-s", SftpUrl), lines, []);

        Assert.AreSame(sshPerformer.KnownHostsFileMissingFailure, failure);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public async Task ResolveSshOptionsAsync_NoKnownHostsFileUnderSilentShowError_WritesCurlsLine()
    {
        List<string> lines = [];

        await ResolveSshAsync(Parse("-sS", SftpUrl), lines, []);

        CollectionAssert.AreEqual(new[] { TransferPerformer.KnownHostsFileMissingLine }, lines);
    }

    [TestMethod]
    public async Task ResolveSshOptionsAsync_NoKnownHostsFileWithFingerprint_WarnsAndGoesOn()
    {
        List<string> lines = [];
        EngineTransferState state = NewState();

        TransferResult? failure = await sshPerformer.ResolveSshOptionsAsync(
            Parse(SftpUrl), new EasySshOptions { HostPublicKeyMd5 = Md5Fingerprint }, CurlUrl.Parse(SftpUrl), state, () => null, (_, _) => { }, Recorder(lines));

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { TransferPerformer.KnownHostsFileMissingWarning }, lines);
        Assert.IsNotNull(state.Ssh);
    }

    [TestMethod]
    public async Task ResolveSshOptionsAsync_NoKnownHostsFileWithFingerprintUnderSilent_WritesNothing()
    {
        List<string> lines = [];

        TransferResult? failure = await ResolveSshAsync(Parse("-s", "--hostpubmd5", Md5Fingerprint, SftpUrl), lines, []);

        Assert.IsNull(failure);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public async Task ResolveSshOptionsAsync_HttpUrl_LeavesTheTransferWithoutSshOptions()
    {
        EngineTransferState state = NewState();

        TransferResult? failure = await sshPerformer.ResolveSshOptionsAsync(
            Parse(First), new EasySshOptions(), CurlUrl.Parse(First), state, () => throw new AssertFailedException("searched"), (_, _) => { }, Recorder([]));

        Assert.IsNull(failure);
        Assert.IsNull(state.Ssh);
    }

    [TestMethod]
    public async Task SwitchToHttpsForHstsAsync_HttpUrlOfAKnownHost_SwitchesToHttpsAndReportsIt()
    {
        HstsTransferPolicy hsts = new(clock);
        hsts.StoreFromResponse(CurlUrl.Parse("https://example.com/"), "max-age=31536000", clock.GetUtcNow());
        InfoRecordingEvents events = new();

        string url = await TransferPerformer.SwitchToHttpsForHstsAsync(hsts, Parse("http://example.com/a"), new EasyHttpOptions(), "http://example.com/a", new ReadRecordingFileSystem(), events);

        Assert.AreEqual("https://example.com/a", url);
        CollectionAssert.AreEqual(new[] { HstsTransferPolicy.SwitchedMessagePrefix + "https://example.com/a" }, events.Info);
    }

    [TestMethod]
    public async Task SwitchToHttpsForHstsAsync_UnknownHost_KeepsTheUrl()
    {
        InfoRecordingEvents events = new();

        string url = await TransferPerformer.SwitchToHttpsForHstsAsync(new HstsTransferPolicy(clock), Parse(First), new EasyHttpOptions(), First, new ReadRecordingFileSystem(), events);

        Assert.AreEqual(First, url);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task SwitchToHttpsForHstsAsync_HstsFileNamingTheHost_ReadsItAndSwitches()
    {
        ReadRecordingFileSystem fileSystem = new();
        fileSystem.ExistingContent["hsts.txt"] = Encoding.ASCII.GetBytes("example.com \"20991231 00:00:00\"\n");
        InfoRecordingEvents events = new();

        string url = await TransferPerformer.SwitchToHttpsForHstsAsync(
            new HstsTransferPolicy(clock), Parse("http://example.com/a"), new EasyHttpOptions { Hsts = "hsts.txt" }, "http://example.com/a", fileSystem, events);

        CollectionAssert.AreEqual(new[] { "hsts.txt" }, fileSystem.ReadPaths);
        Assert.AreEqual("https://example.com/a", url);
    }

    [TestMethod]
    public async Task LoadCookieFilesAsync_CookieFileBeforeAnHttpRequest_LoadsItsCookies()
    {
        CommandLineOptions options = Parse("-b", "cookies.txt", "http://example.com/");
        CookieEngine cookies = CookieEngine.FromHttpOptions(EasyOptionsFromCommandLine.HttpOf(options))!;
        ReadRecordingFileSystem fileSystem = new();
        fileSystem.ExistingContent["cookies.txt"] = Encoding.ASCII.GetBytes("example.com\tFALSE\t/\tFALSE\t0\tname\tvalue\n");
        InfoRecordingEvents events = new();

        await new TransferPerformer(clock, EasyErrorText).LoadCookieFilesAsync(cookies, options, "http://example.com/", fileSystem, Stream.Null, events);

        Assert.AreEqual("name=value", cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse("http://example.com/"), false, clock.GetUtcNow(), events));
    }

    [TestMethod]
    public async Task LoadCookieFilesAsync_FileUrl_ReadsNoCookieFile()
    {
        CommandLineOptions options = Parse("-b", "cookies.txt", "file:///dir/x");
        ReadRecordingFileSystem fileSystem = new();

        await new TransferPerformer(clock, EasyErrorText).LoadCookieFilesAsync(
            CookieEngine.FromHttpOptions(EasyOptionsFromCommandLine.HttpOf(options)), options, "file:///dir/x", fileSystem, Stream.Null, new InfoRecordingEvents());

        Assert.IsEmpty(fileSystem.ReadPaths);
    }

    private const string SftpUrl = "sftp://example.com/file";

    private const string Md5Fingerprint = "00112233445566778899aabbccddeeff";

    private static CommandLineOptions Parse(params string[] arguments) => CommandLineParser.Parse(arguments, _ => true).Options!;

    private static TransferDispatch HttpDispatch() => new(new ProtocolDispatcher([new ScriptedHandler()]));

    private static TransferCredentialLookup CredentialLookup() => new(new InMemoryDataFileReader(), _ => null, runsOnWindows: false);

    private static Func<string, Task> Recorder(List<string> lines) => line =>
    {
        lines.Add(line);
        return Task.CompletedTask;
    };

    private Task<TransferResult?> ResolveSshAsync(CommandLineOptions options, List<string> lines, List<(string? File, bool Missing)> recorded) =>
        sshPerformer.ResolveSshOptionsAsync(
            options, EasyOptionsFromCommandLine.ProtocolOf(options).Ssh, CurlUrl.Parse(SftpUrl), NewState(), () => null, (file, missing) => recorded.Add((file, missing)), Recorder(lines));

    /// <summary>Records the info lines; drops the rest.</summary>
    private sealed class InfoRecordingEvents : ITransferEvents
    {
        public List<string> Info { get; } = [];

        public void ReportInfo(string text) => Info.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportTlsMessage(TlsMessageEvent message)
        {
        }

        public void ReportTlsTrust(TlsTrustEvent trust)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }

    /// <summary>Opens the files it was given for reading, records every path it was asked to read, and writes nothing.</summary>
    private sealed class ReadRecordingFileSystem : IFileSystem
    {
        public Dictionary<string, byte[]> ExistingContent { get; } = [];

        public List<string> ReadPaths { get; } = [];

        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
        {
            ReadPaths.Add(path);
            return ValueTask.FromResult(ExistingContent.TryGetValue(path, out byte[]? content)
                ? FileOpenResult.Opened(new MemoryStream(content, writable: false), content.Length, null)
                : FileOpenResult.Failed(FileAccessStatus.NotFound));
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
