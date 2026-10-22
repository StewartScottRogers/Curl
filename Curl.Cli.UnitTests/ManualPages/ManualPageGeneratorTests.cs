using System.Runtime.CompilerServices;
using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualPageGeneratorTests
{
    private const string Folder = "/log";

    private const string ExpectedWarnings = """
        option1.md:19:1:WARN: see-also a non-existing option: include
        option1.md:19:1:WARN: see-also a non-existing option: silent
        option1.md:19:1:WARN: see-also a non-existing option: trace
        option1.md:19:1:WARN: see-also a non-existing option: trace-ascii
        WARN: option1.md mutexes a non-existing option: trace
        WARN: option1.md mutexes a non-existing option: trace-ascii
        option2.md:15:1:WARN: see-also a non-existing option: proto-default

        """;

    [TestMethod]
    public void Run_Mainpage_RendersUpstreamCase1705sManPage()
    {
        (int exitCode, string output, string error) = Run(1705, "mainpage");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Upstream("data1705-stdout.1").Replace("%DATE", "2026-10-21", StringComparison.Ordinal).Replace("%VERNUM", "8.22.0", StringComparison.Ordinal), output);
        Assert.AreEqual(ExpectedWarnings.ReplaceLineEndings("\n"), error);
    }

    [TestMethod]
    public void Run_Ascii_RendersUpstreamCase1706sAsciiPage()
    {
        (int exitCode, string output, string error) = Run(1706, "ascii");

        Assert.AreEqual(0, exitCode);
        // The case's <stdout> is the file followed by one empty line.
        Assert.AreEqual(Upstream("data1706-stdout.txt") + "\n", output);
        Assert.AreEqual(ExpectedWarnings.ReplaceLineEndings("\n"), error);
    }

    [TestMethod]
    public void Run_UnknownCommand_WritesTheUsageLine()
    {
        StringBuilder output = new();
        int exitCode = new ManualPageGenerator(_ => null, "2026-10-21").Run(["listhelp"], "8.22.0", output, new StringBuilder());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("Usage: managen [-d dir] <mainpage/ascii/listhelp/single FILE/protos/listcats> [files]\n", output.ToString());
    }

    [TestMethod]
    public void Run_MissingVersionHeader_DiesWithExitCode2()
    {
        StringBuilder error = new();
        int exitCode = new ManualPageGenerator(_ => null, "2026-10-21").Run(["-I", "/inc", "mainpage"], null, new StringBuilder(), error);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("no /inc/curl/curlver.h\n", error.ToString());
    }

    private static (int ExitCode, string Output, string Error) Run(int testNumber, string command)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            [$"{Folder}/mainpage.idx"] = "_header.md\n%options\n_footer.md\n",
            [$"{Folder}/_header.md"] = Upstream($"data{testNumber}-1.md"),
            [$"{Folder}/option1.md"] = Upstream($"data{testNumber}-2.md"),
            [$"{Folder}/option2.md"] = Upstream($"data{testNumber}-3.md"),
            [$"{Folder}/_footer.md"] = Upstream($"data{testNumber}-4.md"),
            ["/include/curl/curlver.h"] = "#define LIBCURL_VERSION \"8.22.0-DEV\"\n",
        };
        StringBuilder output = new();
        StringBuilder error = new();
        int exitCode = new ManualPageGenerator(path => files.GetValueOrDefault(path), "2026-10-21")
            .Run(["-d", Folder, "-I", "/include", command, "option1.md", "option2.md"], null, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    private static string Upstream(string name, [CallerFilePath] string sourceFile = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourceFile)!, "Upstream", name)).ReplaceLineEndings("\n");
}
