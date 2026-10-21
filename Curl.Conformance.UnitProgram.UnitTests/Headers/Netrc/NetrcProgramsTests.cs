using System.Text;
using Curl.Conformance.UnitProgram.Headers.Netrc;

namespace Curl.Conformance.UnitProgram.UnitTests.Headers.Netrc;

[TestClass]
public sealed class NetrcProgramsTests
{
    /// <summary>The netrc file case 1304 writes to <c>%LOGDIR/netrc1304</c>.</summary>
    private const string Case1304Netrc = "machine example.com login admin password passwd\nmachine curl.example.com login none password none\n";

    [TestMethod]
    public async Task RunAsync_Unit1304OverTheCaseNetrcFile_EveryCheckPasses()
    {
        (UnitProgramResult result, string standardError) = await RunUnit1304Async(UnitPrograms.Ported.Find("unit1304")!, Case1304Netrc);

        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    [DataRow("", 8)]
    [DataRow("default password y\n", 18)]
    [DataRow("default login x\n", 6)]
    public async Task RunAsync_Unit1304OverAnotherNetrcFile_CountsEveryFailedCheck(string netrc, int expectedFailures)
    {
        (UnitProgramResult result, _) = await RunUnit1304Async(new Unit1304NetrcParsing(), netrc);

        Assert.AreEqual(expectedFailures, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_Unit1304OverAnEmptyNetrcFile_WritesEachFailedCheckOfTheFoundHosts()
    {
        (_, string standardError) = await RunUnit1304Async(new Unit1304NetrcParsing(), string.Empty);

        string[] lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        CollectionAssert.AreEqual(
            new[]
            {
                "unit1304: Host should have been found",
                "unit1304: password should be 'passwd'",
                "unit1304: returned NULL!",
                "unit1304: login should be 'admin'",
                "unit1304: Host should have been found",
                "unit1304: password should be 'none'",
                "unit1304: returned NULL!",
                "unit1304: login should be 'none'",
            },
            lines);
    }

    [TestMethod]
    public async Task RunAsync_Unit1304NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1304NetrcParsing().RunAsync(null!, CancellationToken.None));
    }

    private static async Task<(UnitProgramResult Result, string StandardError)> RunUnit1304Async(IUnitProgram program, string netrc)
    {
        string directory = Path.Combine(Path.GetTempPath(), "unit1304-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "netrc1304");
            await File.WriteAllTextAsync(path, netrc, new UTF8Encoding(false));
            using StringWriter standardError = new();
            UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1304, [path]) { StandardError = standardError }, CancellationToken.None);
            return (result, standardError.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
