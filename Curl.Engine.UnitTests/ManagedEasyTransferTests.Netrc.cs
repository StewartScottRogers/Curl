using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the credentials a perform from the options of a handle with no <see cref="ManagedEasyTransfer.ToolInputs" />
/// looks up in the <c>.netrc</c> file under <c>CURLOPT_NETRC</c> (BL-2696, upstream test2309): the entry for the URL's
/// host goes on the context, through a proxy and on a duplicated handle too, and a required file that cannot be read
/// ends the perform with exit 26 before anything is sent.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    private const string NetrcPath = "netrc2309";

    private static EasyOptions NetrcOptions(NetrcUsage netrc, string? proxy = null) => new()
    {
        Network = new EasyNetworkOptions { Url = "http://github.com" },
        Proxy = new EasyProxyOptions { Proxy = proxy },
        Authentication = new EasyAuthenticationOptions { Netrc = netrc, NetrcFile = NetrcPath },
    };

    private static ManagedEasyTransfer EasyReadingNetrc(RecordingProtocolHandler handler, string? netrcText)
    {
        ManagedEasyTransfer easy = EasyOver(handler, []);
        easy.Host = EasyTransferHost.Library with
        {
            ReadFile = path => path == NetrcPath && netrcText is not null ? Encoding.UTF8.GetBytes(netrcText) : null,
        };
        return easy;
    }

    [TestMethod]
    public async Task PerformAsync_NetrcRequiredThroughAProxyOnADuplicatedHandle_PutsTheHostsEntryOnTheContext()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyReadingNetrc(handler, "\nmachine github.com\n\nlogin daniel\npassword $y$j9T$x\n");
        easy.Options = NetrcOptions(NetrcUsage.Required, "http://127.0.0.1:8990/");
        await using ManagedEasyTransfer dupe = (ManagedEasyTransfer)easy.Duplicate();

        CurlCode code = await dupe.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("daniel", handler.Contexts.Single().Credentials?.UserName);
        Assert.AreEqual("$y$j9T$x", handler.Contexts.Single().Credentials?.Password);
    }

    [TestMethod]
    public async Task PerformAsync_NetrcRequiredAndTheFileUnreadable_AnswersReadErrorAndRunsNoTransfer()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyReadingNetrc(handler, null);
        easy.Options = NetrcOptions(NetrcUsage.Required);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.ReadError, code);
        Assert.AreEqual(".netrc error: no such file", easy.ErrorText);
        Assert.IsEmpty(handler.Contexts);
    }

    [TestMethod]
    public async Task PerformAsync_NetrcIgnored_ReadsNoFileAndPutsNoCredentialsOnTheContext()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyReadingNetrc(handler, "machine github.com login daniel password pw\n");
        easy.Options = NetrcOptions(NetrcUsage.Ignored);

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsNull(handler.Contexts.Single().Credentials);
    }

    [TestMethod]
    public async Task PerformAsync_NetrcRequiredWithToolInputs_ReadsNoFileAndPutsNoCredentialsOnTheContext()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler, []);
        List<string> pathsRead = [];
        easy.Host = EasyTransferHost.Library with
        {
            ReadFile = path =>
            {
                pathsRead.Add(path);
                return Encoding.UTF8.GetBytes("machine github.com login daniel password pw\n");
            },
        };
        easy.Options = NetrcOptions(NetrcUsage.Required);
        easy.ToolInputs = new EngineToolTransferInputs(Url: CurlUrl.Parse("http://github.com"));

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.DoesNotContain(NetrcPath, pathsRead);
        Assert.IsNull(handler.Contexts.Single().Credentials);
    }

    [TestMethod]
    public async Task PerformAsync_NetrcRequiredWithToolInputsAndTheFileUnreadable_RunsTheTransfer()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyReadingNetrc(handler, null);
        easy.Options = NetrcOptions(NetrcUsage.Required);
        easy.ToolInputs = new EngineToolTransferInputs(Url: CurlUrl.Parse("http://github.com"));

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(1, handler.Contexts);
    }

    [TestMethod]
    public async Task PerformAsync_NetrcIgnoredAndCredentialsInTheUrl_PutsTheUrlsCredentialsOnTheContext()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyReadingNetrc(handler, null);
        easy.Options = NetrcOptions(NetrcUsage.Ignored) with
        {
            Network = new EasyNetworkOptions { Url = "http://xxx:yyy@127.0.0.1:9000/1964/testapi/test" },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("xxx", handler.Contexts.Single().Credentials?.UserName);
        Assert.AreEqual("yyy", handler.Contexts.Single().Credentials?.Password);
    }

    [TestMethod]
    public async Task PerformAsync_UserPasswordWithAUserNameAndCredentialsInTheUrl_KeepsTheUserPassword()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyReadingNetrc(handler, null);
        easy.Options = NetrcOptions(NetrcUsage.Ignored) with
        {
            Network = new EasyNetworkOptions { Url = "http://xxx:yyy@127.0.0.1:9000/" },
            Authentication = new EasyAuthenticationOptions { UserPassword = "user:pw" },
        };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual("user", handler.Contexts.Single().Credentials?.UserName);
        Assert.AreEqual("pw", handler.Contexts.Single().Credentials?.Password);
    }

    [TestMethod]
    public async Task PerformAsync_AwsSigV4AndCredentialsOnlyInTheUrl_SignsWithTheUrlsUserName()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);

        string request = await PerformOverScriptedServerAsync(server, new EasyAuthenticationOptions { AwsSigV4 = "aws:amz:us-east-1:s3", HttpAuthentication = HttpAuthenticationMethods.AwsSigV4 });

        Assert.Contains("Authorization: AWS4-HMAC-SHA256 Credential=xxx/", request);
        Assert.Contains("/us-east-1/s3/aws4_request", request);
    }

    [TestMethod]
    public async Task PerformAsync_AwsSigV4ProviderOnlyAndCredentialsOnlyInTheUrl_SignsWithTheUrlsUserNameAndTheHostsScope()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);

        string request = await PerformOverScriptedServerAsync(server, new EasyAuthenticationOptions { AwsSigV4 = "xxx", HttpAuthentication = HttpAuthenticationMethods.AwsSigV4 });

        Assert.Contains("Authorization: XXX4-HMAC-SHA256 Credential=xxx/", request);
        Assert.Contains("/0/127/xxx4_request, SignedHeaders=host;x-xxx-date, Signature=", request);
        Assert.Contains("X-Xxx-Date: ", request);
    }

    [TestMethod]
    public async Task PerformAsync_AwsSigV4SetBackToNullAndCredentialsInTheUrl_SendsNoAuthorization()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);

        string request = await PerformOverScriptedServerAsync(server, new EasyAuthenticationOptions { AwsSigV4 = null, HttpAuthentication = HttpAuthenticationMethods.AwsSigV4 });

        Assert.DoesNotContain("Authorization", request);
    }

    /// <summary>Performs <c>http://xxx:yyy@127.0.0.1:9000/1978</c> with <paramref name="authentication" /> on an engine dialing <paramref name="server" />, and gives the request it wrote.</summary>
    private static async Task<string> PerformOverScriptedServerAsync(ScriptedConnector server, EasyAuthenticationOptions authentication)
    {
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"));
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using Api.EngineContracts.IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://xxx:yyy@127.0.0.1:9000/1978" },
            Authentication = authentication,
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        return Encoding.ASCII.GetString(server.Written);
    }

    [TestMethod]
    public void ReadDiskFile_FileExists_ReturnsItsBytes()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(directory, "netrc");
            File.WriteAllBytes(path, [1, 2, 3]);

            byte[]? bytes = EasyTransferHost.ReadDiskFile(path);

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, bytes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadDiskFile_FileMissingOrADirectory_ReturnsNull()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            byte[]? missing = EasyTransferHost.ReadDiskFile(Path.Combine(directory, "absent"));
            byte[]? folder = EasyTransferHost.ReadDiskFile(directory);

            Assert.IsNull(missing);
            Assert.IsNull(folder);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
