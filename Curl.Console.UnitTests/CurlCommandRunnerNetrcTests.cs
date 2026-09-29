using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins how <c>-n</c>, <c>--netrc-file</c> and <c>--netrc-optional</c> give a transfer its
/// credentials, through fake file and environment seams (<see cref="TransferCredentialLookup" />).
/// Every expectation was measured on 2026-09-28 with curl 8.21.0 (mingw, Schannel) through
/// <c>Record-CurlExchange.ps1</c> against 127.0.0.1:18505, with <c>HOME</c> and
/// <c>USERPROFILE</c> pointed at prepared directories (BL-505 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerNetrcTests
{
    private const string Url = "http://127.0.0.1:18505/";

    private const string Home = "home";

    private const string Profile = "profile";

    private const string Entry = "machine 127.0.0.1 login nu password np\n";

    private const string TwoEntries = "machine 127.0.0.1 login a password pa\nmachine 127.0.0.1 login b password pb\n";

    private const string Malformed = "machine 127.0.0.1 login \"abc\n";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string NoSuchFileLine = "curl: (26) .netrc error: no such file" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly InMemoryDataFileReader dataFiles = new();
    private readonly Dictionary<string, string> environment = new() { ["HOME"] = Home };
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow(true, @"home\.netrc")]
    [DataRow(false, "home/.netrc")]
    public async Task RunAsync_NetrcInTheHomeDirectory_SendsItsLoginAndPassword(bool runsOnWindows, string path)
    {
        // curl -s -S -n <url> with HOME holding .netrc: Authorization: Basic bnU6bnA= (nu:np).
        dataFiles.Files[path] = Encoding.UTF8.GetBytes(Entry);

        int exitCode = await RunAsync(["-s", "-S", "-n", Url], runsOnWindows);

        Assert.AreEqual(0, exitCode);
        AssertSent("nu", "np");
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_BothNetrcNamesOnWindows_ReadsDotNetrcFirst()
    {
        // HOME holding .netrc (login dot) and _netrc (login under): Basic ZG90OmRw (dot:dp).
        dataFiles.Files[@"home\.netrc"] = Encoding.UTF8.GetBytes("machine 127.0.0.1 login dot password dp\n");
        dataFiles.Files[@"home\_netrc"] = Encoding.UTF8.GetBytes("machine 127.0.0.1 login under password up\n");

        await RunAsync(["-s", "-S", "-n", Url], runsOnWindows: true);

        AssertSent("dot", "dp");
        CollectionAssert.AreEqual(new[] { @"home\.netrc" }, dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_OnlyUnderscoreNetrcOnWindows_ReadsIt()
    {
        // HOME holding only _netrc: Basic dW5kZXI6dXA= (under:up).
        dataFiles.Files[@"home\_netrc"] = Encoding.UTF8.GetBytes("machine 127.0.0.1 login under password up\n");

        await RunAsync(["-s", "-S", "-n", Url], runsOnWindows: true);

        AssertSent("under", "up");
        CollectionAssert.AreEqual(new[] { @"home\.netrc", @"home\_netrc" }, dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_OnlyUnderscoreNetrcOffWindows_IsNotLookedFor()
    {
        dataFiles.Files["home/_netrc"] = Encoding.UTF8.GetBytes(Entry);

        int exitCode = await RunAsync(["-s", "-S", "-n", Url], runsOnWindows: false);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(NoSuchFileLine, StandardErrorText);
        CollectionAssert.AreEqual(new[] { "home/.netrc" }, dataFiles.PathsRead);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task RunAsync_NoHomeOnWindows_LooksInTheUserProfile(string? home)
    {
        // HOME unset, USERPROFILE holding .netrc: nu:np; and holding only _netrc: under:up.
        SetHome(home);
        environment["USERPROFILE"] = Profile;
        dataFiles.Files[@"profile\.netrc"] = Encoding.UTF8.GetBytes(Entry);

        await RunAsync(["-s", "-S", "-n", Url], runsOnWindows: true);

        AssertSent("nu", "np");
    }

    [TestMethod]
    public async Task RunAsync_HomeWithoutNetrcOnWindows_DoesNotFallBackToTheUserProfile()
    {
        // HOME an empty directory, USERPROFILE holding .netrc: exit 26, no such file.
        environment["USERPROFILE"] = Profile;
        dataFiles.Files[@"profile\.netrc"] = Encoding.UTF8.GetBytes(Entry);

        int exitCode = await RunAsync(["-s", "-S", "-n", Url], runsOnWindows: true);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(NoSuchFileLine, StandardErrorText);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_NoHomeAndNoProfile_FailsWithNoSuchFile(bool runsOnWindows)
    {
        environment.Remove("HOME");
        dataFiles.Files[@"profile\.netrc"] = Encoding.UTF8.GetBytes(Entry);
        dataFiles.Files["profile/.netrc"] = Encoding.UTF8.GetBytes(Entry);

        int exitCode = await RunAsync(["-s", "-S", "-n", Url], runsOnWindows);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(NoSuchFileLine, StandardErrorText);
        Assert.IsEmpty(dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_UserProfileOffWindows_IsNotLookedIn()
    {
        environment.Remove("HOME");
        environment["USERPROFILE"] = Profile;
        dataFiles.Files["profile/.netrc"] = Encoding.UTF8.GetBytes(Entry);

        int exitCode = await RunAsync(["-s", "-S", "-n", Url], runsOnWindows: false);

        Assert.AreEqual(26, exitCode);
    }

    [TestMethod]
    [DataRow("-s", "-S", "-n")]
    [DataRow("-n")]
    [DataRow("-o", "out.txt", "-n")]
    public async Task RunAsync_RequiredNetrcMissing_Exits26WithNoSuchFileAndSendsNothing(params string[] options)
    {
        // curl [-s -S] -n <url> with no netrc file: "curl: (26) .netrc error: no such file", no
        // request, no progress meter, and no -o file created.
        int exitCode = await RunAsync([.. options, Url]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(NoSuchFileLine, StandardErrorText);
        Assert.IsEmpty(http.Contexts);
        Assert.IsFalse(fileSystem.Written.ContainsKey("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_RequiredNetrcMissingForTwoUrls_FailsEachOne()
    {
        int exitCode = await RunAsync(["-s", "-S", "-n", Url, Url]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(NoSuchFileLine + NoSuchFileLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OptionalNetrcMissing_SendsNoCredentials()
    {
        int exitCode = await RunAsync(["-s", "-S", "--netrc-optional", Url]);

        Assert.AreEqual(0, exitCode);
        Assert.IsNull(Assert.ContainsSingle(http.Contexts).Credentials);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OptionalNetrcFound_SendsItsLoginAndPassword()
    {
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Entry);

        await RunAsync(["-s", "-S", "--netrc-optional", Url]);

        AssertSent("nu", "np");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(Malformed)]
    public async Task RunAsync_UserOptionWithAUserName_WinsAndNetrcIsNotRead(string netrc)
    {
        // curl -u a:b -n <url>: Basic YTpi (a:b), with no netrc file or a malformed one.
        if (netrc.Length > 0)
        {
            dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(netrc);
        }

        int exitCode = await RunAsync(["-s", "-S", "-u", "a:b", "-n", Url]);

        Assert.AreEqual(0, exitCode);
        AssertSent("a", "b");
        Assert.IsEmpty(dataFiles.PathsRead);
    }

    [TestMethod]
    [DataRow(":")]
    [DataRow(":pw")]
    public async Task RunAsync_UserOptionWithoutAUserName_LeavesItToNetrc(string user)
    {
        // curl -u : -n and curl -u :pw -n: Basic bnU6bnA= (nu:np).
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Entry);

        await RunAsync(["-s", "-S", "-u", user, "-n", Url]);

        AssertSent("nu", "np");
    }

    [TestMethod]
    [DataRow("http://b@127.0.0.1:18505/", "b", "pb")]
    [DataRow("http://%62@127.0.0.1:18505/", "b", "pb")]
    [DataRow("http://b:x@127.0.0.1:18505/", "b", "pb")]
    [DataRow("http://zz@127.0.0.1:18505/", "zz", "")]
    [DataRow("http://zz:x@127.0.0.1:18505/", "zz", "x")]
    [DataRow("http://127.0.0.1:18505/", "a", "pa")]
    public async Task RunAsync_UrlUserName_PicksTheEntryAndNetrcPasswordWins(string url, string user, string password)
    {
        // Measured: b@ -> b:pb, %62@ -> b:pb, b:x@ -> b:pb, zz@ -> zz:, zz:x@ -> zz:x, none -> a:pa.
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(TwoEntries);

        await RunAsync(["-s", "-S", "-n", url]);

        AssertSent(user, password);
    }

    [TestMethod]
    public async Task RunAsync_EntryWithOnlyALogin_SendsAnEmptyPassword()
    {
        // machine 127.0.0.1 login lo: Basic bG86 (lo:).
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes("machine 127.0.0.1 login lo\n");

        await RunAsync(["-s", "-S", "-n", Url]);

        AssertSent("lo", string.Empty);
    }

    [TestMethod]
    [DataRow(Url)]
    [DataRow("http://b:x@127.0.0.1:18505/")]
    public async Task RunAsync_RequiredNetrcMalformed_Exits26WithSyntaxError(string url)
    {
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Malformed);

        int exitCode = await RunAsync(["-s", "-S", "-n", url]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("curl: (26) .netrc error: syntax error" + NewLine, StandardErrorText);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_OptionalNetrcMalformed_IsIgnored()
    {
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Malformed);

        int exitCode = await RunAsync(["-s", "-S", "--netrc-optional", Url]);

        Assert.AreEqual(0, exitCode);
        Assert.IsNull(Assert.ContainsSingle(http.Contexts).Credentials);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_OptionalNetrcMalformedOrMissingWithUrlUser_SendsTheUrlCredentials(bool fileThere)
    {
        if (fileThere)
        {
            dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Malformed);
        }

        await RunAsync(["-s", "-S", "--netrc-optional", "http://zz:x@127.0.0.1:18505/"]);

        AssertSent("zz", "x");
    }

    [TestMethod]
    [DataRow("--netrc-file", ".")]
    [DataRow("--netrc-optional", "--netrc-file", ".")]
    public async Task RunAsync_NetrcFile_IsReadInsteadOfTheHomeDirectory(params string[] options)
    {
        // "." exists on every platform, so the parser's existence check passes; the fake reader
        // serves it as the file.
        dataFiles.Files["."] = Encoding.UTF8.GetBytes(Entry);
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes("machine 127.0.0.1 login other password other\n");

        await RunAsync(["-s", "-S", .. options, Url]);

        AssertSent("nu", "np");
        CollectionAssert.AreEqual(new[] { "." }, dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_NetrcFileThatCannotBeRead_Exits26WithNoSuchFile()
    {
        // curl --netrc-file <a directory> <url>: "curl: (26) .netrc error: no such file".
        int exitCode = await RunAsync(["-s", "-S", "--netrc-file", ".", Url]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(NoSuchFileLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithoutNetrcOptions_ReadsNoFileAndKeepsTheUserOption()
    {
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Entry);

        await RunAsync(["-s", "-S", "-u", "a:b", Url]);

        AssertSent("a", "b");
        Assert.IsEmpty(dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_FtpUrl_TakesTheNetrcCredentials()
    {
        // curl -n ftp://127.0.0.1:18505/f: USER nu, PASS np; with b@ and two entries: USER b, PASS pb.
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Entry);

        int exitCode = await RunAsync(["-s", "-S", "-n", "ftp://127.0.0.1:18505/f"], handler: ftp);

        Assert.AreEqual(0, exitCode);
        NetworkCredential credentials = Assert.ContainsSingle(ftp.Contexts).Credentials!;
        Assert.AreEqual("nu", credentials.UserName);
        Assert.AreEqual("np", credentials.Password);
    }

    [TestMethod]
    public async Task RunAsync_FtpUrlWithNetrcMissing_Exits26()
    {
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-s", "-S", "-n", "ftp://127.0.0.1:18505/f"], handler: ftp);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(NoSuchFileLine, StandardErrorText);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_OnWindows_LooksForDotNetrcThenUnderscoreNetrc()
    {
        int exitCode = await RunAsync(["-s", "-S", "-n", Url], OperatingSystem.IsWindows());

        Assert.AreEqual(26, exitCode);
        CollectionAssert.AreEqual(new[] { @"home\.netrc", @"home\_netrc" }, dataFiles.PathsRead);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_OffWindows_LooksForDotNetrcOnly()
    {
        int exitCode = await RunAsync(["-s", "-S", "-n", Url], OperatingSystem.IsWindows());

        Assert.AreEqual(26, exitCode);
        CollectionAssert.AreEqual(new[] { "home/.netrc" }, dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_NetrcCredentials_AreSentAsBasicAuthorization()
    {
        // Measured request: Authorization: Basic bnU6bnA= after Host.
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Entry);
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);

        int exitCode = await RunAsync(["-s", "-S", "-n", Url], handler: HttpOver(server));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "GET / HTTP/1.1\r\nHost: 127.0.0.1:18505\r\nAuthorization: Basic bnU6bnA=\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Encoding.Latin1.GetString(server.Written));
    }

    [TestMethod]
    [DataRow("/b", true)]
    [DataRow("http://localhost:18505/b", false)]
    public async Task RunAsync_RedirectUnderLocation_CarriesNetrcCredentialsOnlyToTheSameHost(string location, bool carried)
    {
        // curl -n -L: a redirect to /b sends Basic bnU6bnA= again; one to localhost (no entry) sends none.
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(Entry);
        ScriptedConnector server = new(
        [
            Encoding.Latin1.GetBytes($"HTTP/1.1 302 Found\r\nLocation: {location}\r\nContent-Length: 0\r\n\r\n"),
            Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"),
        ]);

        int exitCode = await RunAsync(["-s", "-S", "-n", "-L", Url], handler: HttpOver(server));

        Assert.AreEqual(0, exitCode);
        string[] requests = Encoding.Latin1.GetString(server.Written).Split("GET ", StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(2, requests);
        Assert.Contains("Authorization: Basic bnU6bnA=", requests[0]);
        Assert.AreEqual(carried, requests[1].Contains("Authorization:", StringComparison.Ordinal));
    }

    private static HttpProtocolHandler HttpOver(ScriptedConnector server) =>
        new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

    private void SetHome(string? home)
    {
        if (home is null)
        {
            environment.Remove("HOME");
            return;
        }

        environment["HOME"] = home;
    }

    private void AssertSent(string user, string password)
    {
        NetworkCredential? credentials = Assert.ContainsSingle(http.Contexts).Credentials;
        Assert.IsNotNull(credentials);
        Assert.AreEqual(user, credentials.UserName);
        Assert.AreEqual(password, credentials.Password);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, bool runsOnWindows = false, IProtocolHandler? handler = null) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler ?? http])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows,
                outputPaths: fileSystem,
                configFileReader: dataFiles,
                readEnvironmentVariable: name => environment.GetValueOrDefault(name))
            .RunAsync(arguments);
}
