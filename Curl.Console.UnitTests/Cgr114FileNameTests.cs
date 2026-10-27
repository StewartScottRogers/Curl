using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// CGR-114 (BL-3900, ADR-0846): curl 8.22.0's mingw build on a Windows-1252 host writes <c>-o 日本語.bin</c>
/// as <c>___.bin</c>, refuses <c>-T 日本.txt</c> with <c>curl: cannot open '??.txt'</c> and exit 26, and
/// creates <c>cafÃ©.bin</c> for a UTF-8 config file's <c>output = "café.bin"</c>. The arguments are read
/// through an injected Windows-1252 as <c>Program</c> reads them through the host's code page, and the command
/// line is parsed as the Windows build parses it, so the tests pass on every host.
/// </summary>
[TestClass]
public sealed class Cgr114FileNameTests
{
    private const string Url = "http://127.0.0.1/bin";

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly InMemoryDataFileReader configFiles = new();

    [TestMethod]
    public async Task RunAsync_OutputNameOutsideTheAnsiCodePageOnWindows_WritesTheSanitizedBestFitName()
    {
        int exitCode = await RunAsync(AnsiCodePageArguments.ThroughCodePage(["-s", "-o", "日本語.bin", Url], Windows1252));

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(new[] { "___.bin" }, fileSystem.Written.Keys.ToArray());
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_UploadNameOutsideTheAnsiCodePageOnWindows_CannotOpenTheBestFitName()
    {
        fileSystem.UnreadablePaths.Add("??.txt");

        int exitCode = await RunAsync(AnsiCodePageArguments.ThroughCodePage(["-s", "-T", "日本.txt", Url], Windows1252));

        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        StringAssert.StartsWith(Encoding.ASCII.GetString(standardError.ToArray()), "curl: cannot open '??.txt'");
    }

    [TestMethod]
    public async Task RunAsync_OutputInUtf8ConfigFileOnWindows_CreatesTheFileBytesReadInTheAnsiCodePage()
    {
        configFiles.Files["c.cfg"] = Encoding.UTF8.GetBytes("output = \"café.bin\"\n");
        Encoding hostAnsiCodePage = ConfigFileWireText.WindowsAnsiCodePage(() => CodePagesEncodingProvider.Instance.GetEncoding(0));

        int exitCode = await RunAsync(["-s", "-K", "c.cfg", Url]);

        Assert.AreEqual(0, exitCode);
        string written = fileSystem.Written.Keys.Single();
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("café.bin"), hostAnsiCodePage.GetBytes(written));
        Assert.AreEqual(ConfigFileWireText.Respell("café.bin", hostAnsiCodePage), written);
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([RecordingProtocolHandler.WritingPath("http")])),
                fileSystem,
                fileSystem,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: true,
                configFileReader: configFiles,
                defaultConfigFileSearch: new DefaultConfigFileSearch(_ => null, isWindows: true, executableDirectory: null, accountHomeDirectory: null),
                parsesAsWindowsBuild: true)
            .RunAsync(arguments);
}
