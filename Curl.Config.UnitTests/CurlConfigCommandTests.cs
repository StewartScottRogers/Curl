namespace Curl.Config;

/// <summary>
/// Pins <see cref="CurlConfigCommand"/>: each option's answer as ADR-0712 lists it, the usage, an
/// unknown option as curl 8.22.0's <c>curl-config.in</c> answers it, and <c>--checkfor</c>'s comparison.
/// </summary>
[TestClass]
public sealed class CurlConfigCommandTests
{
    private static readonly CurlConfigBuild Build = new("8.22.0", 0x081600, ["FILE", "HTTP"], ["IPv6", "libz"], "Schannel", "/opt/curl", "");

    private static (int ExitCode, string Output) Run(params string[] arguments)
    {
        using StringWriter output = new();
        int exitCode = CurlConfigCommand.Run(arguments, Build, output);
        return (exitCode, output.ToString());
    }

    [TestMethod]
    [DataRow("--built-shared", "no\n")]
    [DataRow("--ca", "\n")]
    [DataRow("--cc", "dotnet\n")]
    [DataRow("--cflags", "\n")]
    [DataRow("--configure", "\n")]
    [DataRow("--feature", "IPv6\nlibz\n")]
    [DataRow("--features", "IPv6\nlibz\n")]
    [DataRow("--libs", "\n")]
    [DataRow("--prefix", "/opt/curl\n")]
    [DataRow("--protocols", "FILE\nHTTP\n")]
    [DataRow("--ssl-backends", "Schannel\n")]
    [DataRow("--static-libs", "\n")]
    [DataRow("--version", "libcurl 8.22.0\n")]
    [DataRow("--vernum", "081600\n")]
    public void Run_Option_PrintsItsAnswerAndExitsZero(string option, string expected)
    {
        Assert.AreEqual((0, expected), Run(option));
    }

    [TestMethod]
    public void Run_SeveralOptions_AnswersEachInTurn()
    {
        Assert.AreEqual((0, "dotnet\nno\n"), Run("--cc", "--built-shared"));
    }

    [TestMethod]
    public void Run_Version_EndsTheRunBeforeTheNextOption()
    {
        Assert.AreEqual((0, "libcurl 8.22.0\n"), Run("--version", "--cc"));
    }

    [TestMethod]
    public void Run_Help_PrintsTheUsageAndExitsZero()
    {
        Assert.AreEqual((0, CurlConfigCommand.UsageText), Run("--help", "--cc"));
    }

    [TestMethod]
    public void Run_NoOption_PrintsTheUsageAndExitsOne()
    {
        Assert.AreEqual((1, CurlConfigCommand.UsageText), Run());
    }

    [TestMethod]
    public void Run_UnknownOption_PrintsUnknownOptionAndTheUsageAndExitsOne()
    {
        Assert.AreEqual((1, "dotnet\nunknown option: --bogus\n" + CurlConfigCommand.UsageText), Run("--cc", "--bogus", "--cc"));
    }

    [TestMethod]
    public void UsageText_StartsAsCurlConfigInWritesIt()
    {
        StringAssert.StartsWith(CurlConfigCommand.UsageText, "Usage: curl-config [OPTION]\n\nAvailable values for OPTION include:\n\n  --built-shared        says 'yes' if libcurl was built shared\n");
    }

    [TestMethod]
    [DataRow("7")]
    [DataRow("8")]
    [DataRow("7.99.99")]
    [DataRow("8.21.9")]
    [DataRow("8.22.0")]
    [DataRow("8.22.0-pre1")]
    [DataRow("7.x")]
    public void Run_CheckforAVersionNoNewer_ExitsZeroSilently(string requested)
    {
        Assert.AreEqual((0, ""), Run("--checkfor", requested, "--cc"));
    }

    [TestMethod]
    [DataRow("9")]
    [DataRow("8.23.0")]
    [DataRow("8.22.1")]
    [DataRow("8.22")]
    [DataRow("8.x.0")]
    [DataRow("8.22.x")]
    [DataRow("x")]
    public void Run_CheckforANewerOrUnreadableVersion_SaysSoAndExitsOne(string requested)
    {
        Assert.AreEqual((1, $"requested version {requested} is newer than existing 8.22.0\n"), Run("--checkfor", requested));
    }

    [TestMethod]
    public void Run_CheckforWithNoVersion_SaysAnEmptyVersionIsNewerAndExitsOne()
    {
        Assert.AreEqual((1, "requested version  is newer than existing 8.22.0\n"), Run("--checkfor"));
    }

    [TestMethod]
    public void Run_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlConfigCommand.Run(null!, Build, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlConfigCommand.Run([], null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlConfigCommand.Run([], Build, null!));
    }
}
