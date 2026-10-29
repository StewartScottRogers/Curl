using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Pins CGR-184 (BL-3713) in <see cref="EasyOptionsFromCommandLine" />: a <c>--keepalive-cnt</c> above
/// <c>INT_MAX</c>, which curl's 64-bit <c>long</c> accepts on Linux and macOS, maps to the most an
/// <see cref="int" /> holds instead of overflowing to a negative count that libcurl refuses with exit 43.
/// Windows curl's 32-bit <c>long</c> refuses the value while parsing, so the command-line tests run off Windows.
/// </summary>
[TestClass]
public sealed class Cgr184Tests
{
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void NetworkOf_KeepAliveCountPastIntMax_IsIntMax()
    {
        CommandLineOptions options = Parse("--keepalive-cnt", "2147483648");

        Assert.AreEqual(int.MaxValue, EasyOptionsFromCommandLine.NetworkOf(options).TcpKeepCount);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void NetworkOf_KeepAliveCountWithinInt_IsKept()
    {
        CommandLineOptions options = Parse("--keepalive-cnt", "5");

        Assert.AreEqual(5, EasyOptionsFromCommandLine.NetworkOf(options).TcpKeepCount);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult result = QuicBuildParser.Parse([.. arguments, "http://127.0.0.1/"], _ => true);
        Assert.IsTrue(result.IsAccepted, string.Join(' ', arguments));
        return result.Options;
    }
}
