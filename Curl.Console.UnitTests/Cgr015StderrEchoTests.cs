using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// CGR-015 part 1b (BL-3893, ADR-0840): on Windows the reference curl echoes a refused argument to standard
/// error in the system ANSI code page it read it in (<c>--sélent</c> as E9), while a refused config file line
/// keeps the file's UTF-8 bytes. The tests compare against <see cref="CredentialEncoding.ForPlatform" />,
/// Windows-1252 on a host that names no ANSI code page, so they pass on every host.
/// </summary>
[TestClass]
public sealed class Cgr015StderrEchoTests
{
    private const string UnknownOptionLine = "curl: option --sélent: is unknown";

    [TestMethod]
    public async Task RunAsync_UnknownOptionOnWindows_EchoesItInTheAnsiCodePage()
    {
        byte[] standardError = await RunUnknownOptionAsync(runsOnWindows: true);

        Assert.IsTrue(
            Contains(standardError, CredentialEncoding.ForPlatform(isWindows: true).GetBytes(UnknownOptionLine)),
            Convert.ToHexString(standardError));
        Assert.IsFalse(Contains(standardError, Encoding.UTF8.GetBytes("é")), Convert.ToHexString(standardError));
    }

    [TestMethod]
    public async Task RunAsync_UnknownOptionOffWindows_EchoesItAsUtf8()
    {
        byte[] standardError = await RunUnknownOptionAsync(runsOnWindows: false);

        Assert.IsTrue(Contains(standardError, Encoding.UTF8.GetBytes(UnknownOptionLine)), Convert.ToHexString(standardError));
    }

    [TestMethod]
    public async Task RunAsync_UnknownOptionInAConfigFileOnWindows_EchoesTheFilesUtf8Bytes()
    {
        InMemoryDataFileReader configFiles = new();
        configFiles.Files["k.txt"] = Encoding.UTF8.GetBytes("--sélent\n");

        byte[] standardError = await RunUnknownOptionAsync(runsOnWindows: true, ["-K", "k.txt", "http://127.0.0.1/"], configFiles);

        Assert.IsTrue(
            Contains(standardError, Encoding.UTF8.GetBytes("config file option '--sélent'")),
            Convert.ToHexString(standardError));
    }

    private static Task<byte[]> RunUnknownOptionAsync(bool runsOnWindows) =>
        RunUnknownOptionAsync(runsOnWindows, ["--sélent", "http://127.0.0.1/"], new InMemoryDataFileReader());

    private static async Task<byte[]> RunUnknownOptionAsync(bool runsOnWindows, string[] arguments, InMemoryDataFileReader configFiles)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        InMemoryFileSystem fileSystem = new();
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http]), []),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows,
                TerminalColumns.Default,
                configFileReader: configFiles)
            .RunAsync(arguments);

        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        return standardError.ToArray();
    }

    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;
}
