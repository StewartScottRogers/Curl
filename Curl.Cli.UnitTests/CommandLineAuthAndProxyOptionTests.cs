using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--basic</c>, <c>--digest</c>, <c>--anyauth</c>, the <c>--proxy-</c> auth switches, <c>--oauth2-bearer</c>, <c>-x</c> / <c>--proxy</c>,
/// <c>-U</c> / <c>--proxy-user</c>, <c>--noproxy</c>, <c>-p</c> / <c>--proxytunnel</c> and the <c>--socks</c>
/// options as curl 8.21.0 parses them. Measured with the reference curl 8.21.0 on 2026-09-26; the
/// commands and what they produced are in BL-192's Notes and ADR-0026.
/// </summary>
[TestClass]
public sealed class CommandLineAuthAndProxyOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string CannotBeReversed = "the given option cannot be reversed with a --no- prefix";

    [TestMethod]
    public void Parse_NoAuthOption_AllowsBasic()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(HttpAuthSchemes.Basic, result.Options.AuthSchemes);
        Assert.IsNull(result.Options.BearerToken);
    }

    [TestMethod]
    [DataRow(new[] { "--basic" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--digest" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--basic", "--digest" }, HttpAuthSchemes.Basic | HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--no-basic" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--digest", "--no-digest" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--basic", "--no-basic", "--digest" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--digest", "--basic", "--no-basic" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--ntlm" }, HttpAuthSchemes.Ntlm)]
    [DataRow(new[] { "--negotiate" }, HttpAuthSchemes.Negotiate)]
    [DataRow(new[] { "--no-ntlm" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--no-negotiate" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--ntlm", "--no-ntlm" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--negotiate", "--no-negotiate" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--basic", "--ntlm" }, HttpAuthSchemes.Basic | HttpAuthSchemes.Ntlm)]
    [DataRow(new[] { "--ntlm", "--negotiate" }, HttpAuthSchemes.Ntlm | HttpAuthSchemes.Negotiate)]
    [DataRow(new[] { "--anyauth", "--no-ntlm", "--no-negotiate" }, HttpAuthSchemes.Basic | HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--anyauth" }, HttpAuthSchemes.Any)]
    [DataRow(new[] { "--anyauth", "--basic" }, HttpAuthSchemes.Any)]
    [DataRow(new[] { "--anyauth", "--no-digest" }, HttpAuthSchemes.Basic | HttpAuthSchemes.Ntlm | HttpAuthSchemes.Negotiate)]
    [DataRow(new[] { "--digest", "--anyauth" }, HttpAuthSchemes.Any)]
    [DataRow(new[] { "--oauth2-bearer", "tok" }, HttpAuthSchemes.Bearer)]
    [DataRow(new[] { "--oauth2-bearer", "tok", "--basic" }, HttpAuthSchemes.Bearer | HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--oauth2-bearer", "tok", "--no-basic" }, HttpAuthSchemes.Bearer)]
    [DataRow(new[] { "--oauth2-bearer", "tok", "--anyauth" }, HttpAuthSchemes.Any | HttpAuthSchemes.Bearer)]
    [DataRow(new[] { "--anyauth", "--oauth2-bearer", "tok" }, HttpAuthSchemes.Any | HttpAuthSchemes.Bearer)]
    public void Parse_AuthSchemeOptions_AllowTheSchemesCurlAsksFor(string[] options, HttpAuthSchemes expected)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([.. options, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.AuthSchemes);
    }

    [TestMethod]
    [DataRow(new string[0], HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--proxy-basic" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--proxy-digest" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--proxy-ntlm" }, HttpAuthSchemes.Ntlm)]
    [DataRow(new[] { "--proxy-negotiate" }, HttpAuthSchemes.Negotiate)]
    [DataRow(new[] { "--proxy-anyauth" }, HttpAuthSchemes.Any)]
    [DataRow(new[] { "--proxy-digest", "--proxy-basic" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--proxy-basic", "--proxy-digest" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--proxy-digest", "--proxy-ntlm" }, HttpAuthSchemes.Ntlm)]
    [DataRow(new[] { "--proxy-ntlm", "--proxy-negotiate" }, HttpAuthSchemes.Negotiate)]
    [DataRow(new[] { "--proxy-anyauth", "--proxy-basic" }, HttpAuthSchemes.Any)]
    [DataRow(new[] { "--proxy-negotiate", "--proxy-anyauth" }, HttpAuthSchemes.Any)]
    [DataRow(new[] { "--proxy-basic", "--no-proxy-basic" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--proxy-digest", "--no-proxy-digest" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--proxy-ntlm", "--proxy-digest", "--no-proxy-ntlm" }, HttpAuthSchemes.Digest)]
    [DataRow(new[] { "--proxy-negotiate", "--no-proxy-negotiate" }, HttpAuthSchemes.Basic)]
    [DataRow(new[] { "--proxy-anyauth", "--no-proxy-anyauth", "--proxy-digest" }, HttpAuthSchemes.Digest)]
    public void Parse_ProxyAuthSchemeOptions_AllowTheOneSchemeCurlPicks(string[] options, HttpAuthSchemes expected)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([.. options, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.ProxyAuthSchemes);
    }

    [TestMethod]
    public void Parse_ProxyAndServerAuthSchemeOptions_SetTheirOwnSets()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["--proxy-digest", "--ntlm", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(HttpAuthSchemes.Digest, result.Options.ProxyAuthSchemes);
        Assert.AreEqual(HttpAuthSchemes.Ntlm, result.Options.AuthSchemes);
    }

    [TestMethod]
    public void Parse_OAuth2Bearer_RecordsTheLastToken()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["--oauth2-bearer", "one", "--oauth2-bearer", "two", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("two", result.Options.BearerToken);
    }

    [TestMethod]
    public void Parse_NoAwsSigV4_LeavesItNull()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.AwsSigV4);
    }

    /// <summary>
    /// <c>--aws-sigv4</c> keeps its last value verbatim and leaves <see cref="CommandLineOptions.AuthSchemes" />
    /// alone: the HTTP handler signs in place of every scheme whenever it is set (BL-629).
    /// </summary>
    [TestMethod]
    [DataRow(new[] { "--aws-sigv4", "aws:amz:us-east-1:s3" }, "aws:amz:us-east-1:s3")]
    [DataRow(new[] { "--aws-sigv4", "osc", "--aws-sigv4", "aws:amz" }, "aws:amz")]
    [DataRow(new[] { "--aws-sigv4", "" }, "")]
    public void Parse_AwsSigV4_RecordsTheLastValueVerbatim(string[] arguments, string expected)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.AwsSigV4);
        Assert.AreEqual(HttpAuthSchemes.Basic, result.Options.AuthSchemes);
    }

    [TestMethod]
    [DataRow("--oauth2-bearer")]
    [DataRow("--socks4")]
    [DataRow("--socks4a")]
    [DataRow("--socks5")]
    [DataRow("--socks5-hostname")]
    public void Parse_EmptyValue_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([spelledOption, "", Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("-x", "proxy.example:3128", ProxyKind.Http)]
    [DataRow("--proxy", "proxy.example:3128", ProxyKind.Http)]
    [DataRow("--socks4", "proxy.example:1080", ProxyKind.Socks4)]
    [DataRow("--socks4a", "proxy.example:1080", ProxyKind.Socks4a)]
    [DataRow("--socks5", "proxy.example:1080", ProxyKind.Socks5)]
    [DataRow("--socks5-hostname", "proxy.example:1080", ProxyKind.Socks5Hostname)]
    public void Parse_ProxyOption_RecordsTheValueAndTheKindItNames(string spelledOption, string address, ProxyKind kind)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([spelledOption, address, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(new CommandLineProxy(address, kind), result.Options.Proxy);
    }

    [TestMethod]
    public void Parse_NoProxyOption_HasNoProxy()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.Proxy);
        Assert.IsNull(result.Options.ProxyCredentials);
        Assert.IsNull(result.Options.NoProxy);
        Assert.IsFalse(result.Options.ProxyTunnel);
    }

    [TestMethod]
    public void Parse_EmptyProxy_IsKeptAsNoProxyAtAll()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["-x", "a:1", "-x", "", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(new CommandLineProxy(string.Empty, ProxyKind.Http), result.Options.Proxy);
    }

    [TestMethod]
    public void Parse_Socks5ThenProxy_TheLastWinsWithItsKind()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["--socks5", "a:1", "-x", "b:2", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(new CommandLineProxy("b:2", ProxyKind.Http), result.Options.Proxy);
    }

    [TestMethod]
    public void Parse_ProxyThenSocks5_TheLastWinsWithItsKind()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["-x", "a:1", "--socks5", "b:2", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(new CommandLineProxy("b:2", ProxyKind.Socks5), result.Options.Proxy);
    }

    [TestMethod]
    public void Parse_ProxyValueThatLooksLikeAFlag_IsTakenWithoutAWarning()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["-x", "-s", "--noproxy", "-s", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-s", result.Options.Proxy!.Address);
        Assert.AreEqual("-s", result.Options.NoProxy);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-U")]
    [DataRow("--proxy-user")]
    public void Parse_ProxyUser_SplitsAtTheFirstColon(string spelledOption)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([spelledOption, "bob:se:cret", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("bob", result.Options.ProxyCredentials!.UserName);
        Assert.AreEqual("se:cret", result.Options.ProxyCredentials.Password);
        Assert.IsNull(result.Options.Credentials);
    }

    [TestMethod]
    [DataRow("*")]
    [DataRow("")]
    [DataRow("example.com,.local,10.0.0.0/8")]
    public void Parse_NoProxy_RecordsTheValueVerbatim(string hosts)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["--noproxy", "other", "--noproxy", hosts, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(hosts, result.Options.NoProxy);
    }

    [TestMethod]
    [DataRow(new[] { "-p" }, true)]
    [DataRow(new[] { "--proxytunnel" }, true)]
    [DataRow(new[] { "-p", "--no-proxytunnel" }, false)]
    [DataRow(new[] { "--no-proxytunnel", "-p" }, true)]
    public void Parse_ProxyTunnel_LastSpellingWins(string[] options, bool expected)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([.. options, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.ProxyTunnel);
    }

    [TestMethod]
    [DataRow("--basic=x")]
    [DataRow("--digest=x")]
    [DataRow("--anyauth=x")]
    [DataRow("--proxytunnel=x")]
    public void Parse_FlagWithAttachedValue_IsAccepted(string spelledOption)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    [DataRow("--no-anyauth")]
    [DataRow("--no-anyauth=x")]
    [DataRow("--no-oauth2-bearer")]
    [DataRow("--no-oauth2-bearer=x")]
    [DataRow("--no-proxy")]
    [DataRow("--no-proxy=x")]
    [DataRow("--no-proxy-user")]
    [DataRow("--no-proxy-user=x")]
    [DataRow("--no-noproxy")]
    [DataRow("--no-noproxy=x")]
    [DataRow("--no-socks4")]
    [DataRow("--no-socks4=x")]
    [DataRow("--no-socks4a")]
    [DataRow("--no-socks4a=x")]
    [DataRow("--no-socks5")]
    [DataRow("--no-socks5=x")]
    [DataRow("--no-socks5-hostname")]
    [DataRow("--no-socks5-hostname=x")]
    public void Parse_NoSpellingCurlRefuses_IsRefusedAsNotReversible(string spelledOption)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([spelledOption, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: {CannotBeReversed}");
    }

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
