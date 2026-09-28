namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the eight connection and TLS switches curl 8.21.0 accepts
/// with no output of their own: <c>--[no-]tcp-nodelay</c>, <c>--[no-]alpn</c>,
/// <c>--[no-]sessionid</c>, <c>--[no-]keepalive</c>, <c>--[no-]styled-output</c>,
/// <c>--[no-]ssl-allow-beast</c>, <c>--[no-]ca-native</c> and <c>--[no-]ssl-revoke-best-effort</c>.
/// Measured against the local curl 8.21.0 on 2026-09-28: every spelling, <c>--no-</c> forms
/// included, exits 0 and adds nothing to standard error.
/// </summary>
[TestClass]
public sealed class CommandLineConnectionSwitchTests
{
    private const string Url = "https://example.com/";

    private static readonly Func<string, bool> NoPathExists = _ => false;

    [TestMethod]
    public void Parse_NoSwitches_KeepsCurlDefaults()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.TcpNoDelay);
        Assert.IsTrue(result.Options.UseAlpn);
        Assert.IsTrue(result.Options.ReuseSessionIds);
        Assert.IsTrue(result.Options.TcpKeepAlive);
        Assert.IsTrue(result.Options.StyledOutput);
        Assert.IsFalse(result.Options.AllowBeast);
        Assert.IsFalse(result.Options.UseNativeCaStore);
        Assert.IsFalse(result.Options.RevocationCheckBestEffort);
    }

    [TestMethod]
    [DataRow("--tcp-nodelay", nameof(CommandLineOptions.TcpNoDelay), true)]
    [DataRow("--no-tcp-nodelay", nameof(CommandLineOptions.TcpNoDelay), false)]
    [DataRow("--alpn", nameof(CommandLineOptions.UseAlpn), true)]
    [DataRow("--no-alpn", nameof(CommandLineOptions.UseAlpn), false)]
    [DataRow("--sessionid", nameof(CommandLineOptions.ReuseSessionIds), true)]
    [DataRow("--no-sessionid", nameof(CommandLineOptions.ReuseSessionIds), false)]
    [DataRow("--keepalive", nameof(CommandLineOptions.TcpKeepAlive), true)]
    [DataRow("--no-keepalive", nameof(CommandLineOptions.TcpKeepAlive), false)]
    [DataRow("--styled-output", nameof(CommandLineOptions.StyledOutput), true)]
    [DataRow("--no-styled-output", nameof(CommandLineOptions.StyledOutput), false)]
    [DataRow("--ssl-allow-beast", nameof(CommandLineOptions.AllowBeast), true)]
    [DataRow("--no-ssl-allow-beast", nameof(CommandLineOptions.AllowBeast), false)]
    [DataRow("--ca-native", nameof(CommandLineOptions.UseNativeCaStore), true)]
    [DataRow("--no-ca-native", nameof(CommandLineOptions.UseNativeCaStore), false)]
    [DataRow("--ssl-revoke-best-effort", nameof(CommandLineOptions.RevocationCheckBestEffort), true)]
    [DataRow("--no-ssl-revoke-best-effort", nameof(CommandLineOptions.RevocationCheckBestEffort), false)]
    public void Parse_Switch_SetsItsProperty(string argument, string property, bool expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse([argument, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, ReadSwitch(result.Options, property));
    }

    [TestMethod]
    [DataRow("--no-tcp-nodelay", "--tcp-nodelay", nameof(CommandLineOptions.TcpNoDelay), true)]
    [DataRow("--no-alpn", "--alpn", nameof(CommandLineOptions.UseAlpn), true)]
    [DataRow("--no-sessionid", "--sessionid", nameof(CommandLineOptions.ReuseSessionIds), true)]
    [DataRow("--no-keepalive", "--keepalive", nameof(CommandLineOptions.TcpKeepAlive), true)]
    [DataRow("--no-styled-output", "--styled-output", nameof(CommandLineOptions.StyledOutput), true)]
    [DataRow("--ssl-allow-beast", "--no-ssl-allow-beast", nameof(CommandLineOptions.AllowBeast), false)]
    [DataRow("--ca-native", "--no-ca-native", nameof(CommandLineOptions.UseNativeCaStore), false)]
    [DataRow("--ssl-revoke-best-effort", "--no-ssl-revoke-best-effort", nameof(CommandLineOptions.RevocationCheckBestEffort), false)]
    public void Parse_SwitchThenItsOpposite_LastOneWins(string first, string second, string property, bool expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse([first, second, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, ReadSwitch(result.Options, property));
    }

    private static bool ReadSwitch(CommandLineOptions options, string property) => property switch
    {
        nameof(CommandLineOptions.TcpNoDelay) => options.TcpNoDelay,
        nameof(CommandLineOptions.UseAlpn) => options.UseAlpn,
        nameof(CommandLineOptions.ReuseSessionIds) => options.ReuseSessionIds,
        nameof(CommandLineOptions.TcpKeepAlive) => options.TcpKeepAlive,
        nameof(CommandLineOptions.StyledOutput) => options.StyledOutput,
        nameof(CommandLineOptions.AllowBeast) => options.AllowBeast,
        nameof(CommandLineOptions.UseNativeCaStore) => options.UseNativeCaStore,
        _ => options.RevocationCheckBestEffort,
    };
}
