using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-069 part 1 (BL-3894) for the parser: a <c>-d @dir</c> or <c>-w @dir</c> naming a directory is
/// refused with exit 26 everywhere, but curl's Linux and macOS builds open the directory and only the
/// read fails, so they print no <c>curl: Failed to open dir</c> line (measured with curl 8.18.0 on
/// Linux on 2026-10-28); curl's Windows build cannot open it and prints the line.
/// </summary>
[TestClass]
public sealed class Cgr069DataFileTests
{
    private const string Url = "http://127.0.0.1/a";
    private const string TryHelpLine = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-d")]
    [DataRow("-w")]
    public void Parse_AtDirectoryOffWindows_IsRefusedWithoutTheFailedToOpenLine(string option)
    {
        string directory = CreateDirectory();
        try
        {
            Diagnostics.Arrange("arguments", $"{option} @<directory> {Url}, Linux build");
            CommandLineParseResult result = Parse([option, "@" + directory, Url], isWindows: false);

            Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Refusal?.ExitCode);
            Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
            CollectionAssert.AreEqual(
                new[] { $"curl: option {option}: error encountered when reading a file", TryHelpLine },
                result.Refusal.StandardErrorLines.ToArray());
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public void Parse_DataAtDirectoryOnWindows_IsRefusedWithTheFailedToOpenLine()
    {
        string directory = CreateDirectory();
        try
        {
            Diagnostics.Arrange("arguments", $"-d @<directory> {Url}, Windows build");
            CommandLineParseResult result = Parse(["-d", "@" + directory, Url], isWindows: true);

            Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Refusal?.ExitCode);
            Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
            string[] lines = [.. result.Refusal.StandardErrorLines];
            Assert.StartsWith("curl: Failed to open ", lines[0]);
            CollectionAssert.AreEqual(
                new[] { "curl: option -d: error encountered when reading a file", TryHelpLine },
                lines[^2..]);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public void OpensAsUnreadableDirectory_RegularFileOffWindows_IsFalse()
    {
        string file = Path.GetTempFileName();
        try
        {
            bool opens = DiskDataFileReader.ForPlatform(false).OpensAsUnreadableDirectory(file);

            Diagnostics.Assert("opens as unreadable directory", false, opens);
            Assert.IsFalse(opens);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void OpensAsUnreadableDirectory_DefaultMember_IsFalse()
    {
        IDataFileReader reader = new RecordingDataFileReader();

        bool opens = reader.OpensAsUnreadableDirectory(Path.GetTempPath());

        Diagnostics.Assert("opens as unreadable directory", false, opens);
        Assert.IsFalse(opens);
    }

    [TestMethod]
    public void DataFileOpenedButUnreadable_NullOption_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineRefusal.DataFileOpenedButUnreadable(null!));

        Diagnostics.Assert("parameter", "spelledOption", exception.ParamName);
        Assert.AreEqual("spelledOption", exception.ParamName);
    }

    private static string CreateDirectory() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "cgr069-" + Guid.NewGuid().ToString("N"))).FullName;

    private static CommandLineParseResult Parse(string[] arguments, bool isWindows) =>
        CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForPlatform(isWindows), isWindows);
}
