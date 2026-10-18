using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;

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
