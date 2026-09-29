using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--proxy1.0</c>, <c>--preproxy</c>, <c>--socks5-basic</c>, <c>--socks5-gssapi</c>,
/// <c>--socks5-gssapi-service</c>, <c>--socks5-gssapi-nec</c>, <c>--haproxy-protocol</c>,
/// <c>--haproxy-clientip</c> and <c>--suppress-connect-headers</c> as curl 8.21.0 parses them.
/// Measured with the reference curl 8.21.0 on 2026-09-28; the commands and what they produced are
/// in BL-612's Notes.
/// </summary>
[TestClass]
public sealed class CommandLineProxyVariantOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesEveryPropertyAtItsDefault()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.PreProxy);
        Assert.IsFalse(result.Options.Socks5BasicAuth);
        Assert.IsFalse(result.Options.Socks5GssapiAuth);
        Assert.IsNull(result.Options.Socks5GssapiServiceName);
        Assert.IsFalse(result.Options.Socks5GssapiNec);
        Assert.IsFalse(result.Options.HaproxyProtocol);
        Assert.IsNull(result.Options.HaproxyClientIp);
        Assert.IsFalse(result.Options.SuppressConnectHeaders);
    }

    [TestMethod]
    public void Parse_Proxy10_RecordsAnHttp10Proxy()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy1.0", "proxy.example:3128", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(new CommandLineProxy("proxy.example:3128", ProxyKind.Http10), result.Options.Proxy);
    }

    [TestMethod]
    [DataRow(new[] { "--proxy1.0", "a:1", "-x", "b:2" }, "b:2", ProxyKind.Http)]
    [DataRow(new[] { "-x", "a:1", "--proxy1.0", "b:2" }, "b:2", ProxyKind.Http10)]
    [DataRow(new[] { "--socks5", "a:1", "--proxy1.0", "b:2" }, "b:2", ProxyKind.Http10)]
    [DataRow(new[] { "--proxy1.0", "a:1", "--socks5", "b:2" }, "b:2", ProxyKind.Socks5)]
    public void Parse_Proxy10AndAnotherProxyOption_TheLastWinsWithItsKind(string[] options, string address, ProxyKind kind)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. options, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(new CommandLineProxy(address, kind), result.Options.Proxy);
    }

    [TestMethod]
    public void Parse_PreProxy_RecordsTheLastValueVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--preproxy", "socks5://a:1", "--preproxy", "socks4://b:2", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("socks4://b:2", result.Options.PreProxy);
        Assert.IsNull(result.Options.Proxy);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("HTTP/service")]
    public void Parse_Socks5GssapiService_RecordsTheLastValueEvenWhenEmpty(string service)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--socks5-gssapi-service", "other", "--socks5-gssapi-service", service, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(service, result.Options.Socks5GssapiServiceName);
    }

    [TestMethod]
    public void Parse_HaproxyClientIp_RecordsTheLastValueUnvalidated()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--haproxy-clientip", "10.0.0.1", "--haproxy-clientip", "x", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("x", result.Options.HaproxyClientIp);
    }

    [TestMethod]
    [DataRow("--socks5-basic")]
    [DataRow("--socks5-gssapi")]
    [DataRow("--socks5-gssapi-nec")]
    [DataRow("--haproxy-protocol")]
    [DataRow("--suppress-connect-headers")]
    public void Parse_Flag_TurnsOnOnlyItsOwnProperty(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { spelledOption }, FlagsTurnedOn(result.Options));
    }

    [TestMethod]
    [DataRow("--socks5-basic")]
    [DataRow("--socks5-gssapi")]
    [DataRow("--socks5-gssapi-nec")]
    [DataRow("--haproxy-protocol")]
    [DataRow("--suppress-connect-headers")]
    public void Parse_FlagThenItsNoSpelling_TurnsItOff(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "--no-" + spelledOption[2..], Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(FlagsTurnedOn(result.Options));
    }

    [TestMethod]
    [DataRow("--socks5-basic")]
    [DataRow("--socks5-gssapi")]
    [DataRow("--socks5-gssapi-nec")]
    [DataRow("--haproxy-protocol")]
    [DataRow("--suppress-connect-headers")]
    public void Parse_NoSpellingThenFlag_TurnsItOn(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-" + spelledOption[2..], spelledOption + "=x", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { spelledOption }, FlagsTurnedOn(result.Options));
    }

    [TestMethod]
    [DataRow("--proxy1.0")]
    [DataRow("--preproxy")]
    [DataRow("--haproxy-clientip")]
    public void Parse_EmptyValue_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "", Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--no-proxy1.0")]
    [DataRow("--no-proxy1.0=x")]
    [DataRow("--no-preproxy")]
    [DataRow("--no-preproxy=x")]
    [DataRow("--no-socks5-gssapi-service")]
    [DataRow("--no-socks5-gssapi-service=x")]
    [DataRow("--no-haproxy-clientip")]
    [DataRow("--no-haproxy-clientip=x")]
    public void Parse_NoSpellingOfAValueOption_IsRefusedAsNotReversible(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix");
    }

    private static string[] FlagsTurnedOn(CommandLineOptions options) =>
    [
        .. options.Socks5BasicAuth ? new[] { "--socks5-basic" } : [],
        .. options.Socks5GssapiAuth ? new[] { "--socks5-gssapi" } : [],
        .. options.Socks5GssapiNec ? new[] { "--socks5-gssapi-nec" } : [],
        .. options.HaproxyProtocol ? new[] { "--haproxy-protocol" } : [],
        .. options.SuppressConnectHeaders ? new[] { "--suppress-connect-headers" } : [],
    ];

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
