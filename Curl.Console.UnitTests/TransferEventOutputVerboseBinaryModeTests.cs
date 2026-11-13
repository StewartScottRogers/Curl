using Curl.Cli;
using Curl.Core;
using Curl.Testing;
using System.Text;

namespace Curl.Console;

/// <summary>
/// Pins <c>-v --stderr -</c>: its lines follow standard output's text or binary mode, as curl's do (BL-5040).
/// </summary>
[TestClass]
public sealed class TransferEventOutputVerboseBinaryModeTests
{
    private static async Task<string> WriteInfoLineAsync(bool runsOnWindows, bool stderrIsStdout, bool binary)
    {
        CommandLineOptions options = CommandLineParser.Parse(["-v", "http://127.0.0.1/"], _ => true).Options!;
        MemoryStream standardOutput = new();
        await using TransferEventOutput output = await TransferEventOutput.OpenAsync(
            options, new InMemoryFileSystem(), standardOutput, standardOutput, runsOnWindows, standardOutputIsTerminal: false, TimeProvider.System, () => binary, stderrIsStdout);
        output.Events.ReportInfo("line");
        return Encoding.ASCII.GetString(standardOutput.ToArray());
    }

    [TestMethod]
    public async Task StderrToStdout_BodyAlsoOnStdout_WritesBareLineFeed()
    {
        Assert.AreEqual("* line\n", await WriteInfoLineAsync(runsOnWindows: true, stderrIsStdout: true, binary: true));
    }

    [TestMethod]
    public async Task StderrToStdout_BodyInFile_WritesCrLfWhereTheRuntimeIsWindows()
    {
        Assert.AreEqual("* line\r\n", await WriteInfoLineAsync(runsOnWindows: true, stderrIsStdout: true, binary: false));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task StderrToStdout_BodyInFile_TextModeEmitsCrLfOnWindows()
    {
        Assert.AreEqual("* line\r\n", await WriteInfoLineAsync(runsOnWindows: OperatingSystem.IsWindows(), stderrIsStdout: true, binary: false));
    }

    [TestMethod]
    public async Task StandardErrorNotRedirected_StaysTextMode()
    {
        Assert.AreEqual("* line\r\n", await WriteInfoLineAsync(runsOnWindows: true, stderrIsStdout: false, binary: true));
    }
}
