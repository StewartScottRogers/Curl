using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// CGR-015 (BL-3544): on Windows the reference curl reads <c>-T ü.txt</c> in the system ANSI code page and
/// echoes the name it cannot open in those bytes (<c>curl: cannot open '</c> FC <c>.txt'</c>), not as UTF-8.
/// The tests compare against <see cref="CredentialEncoding.ForPlatform" />, so they pass on every host.
/// </summary>
[TestClass]
public sealed class Cgr015Tests
{
    private const string CannotOpenLine = "curl: cannot open 'ü.txt'";

    [TestMethod]
    public async Task RunAsync_UnopenableUploadFileOnWindows_EchoesItsNameInTheAnsiCodePage()
    {
        byte[] standardError = await RunUploadOfMissingFileAsync(runsOnWindows: true);

        Assert.IsTrue(
            Contains(standardError, CredentialEncoding.ForPlatform(isWindows: true).GetBytes(CannotOpenLine)),
            Convert.ToHexString(standardError));
        Assert.IsFalse(Contains(standardError, Encoding.UTF8.GetBytes("ü")), Convert.ToHexString(standardError));
    }

    [TestMethod]
    public async Task RunAsync_UnopenableUploadFileOffWindows_EchoesItsNameAsUtf8()
    {
        byte[] standardError = await RunUploadOfMissingFileAsync(runsOnWindows: false);

        Assert.IsTrue(Contains(standardError, Encoding.UTF8.GetBytes(CannotOpenLine)), Convert.ToHexString(standardError));
    }

    private static async Task<byte[]> RunUploadOfMissingFileAsync(bool runsOnWindows)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        InMemoryFileSystem fileSystem = new();
        fileSystem.UnreadablePaths.Add("ü.txt");
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http]), []),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows,
                TerminalColumns.Default)
            .RunAsync(["-T", "ü.txt", "http://127.0.0.1/"]);

        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        return standardError.ToArray();
    }

    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;
}
