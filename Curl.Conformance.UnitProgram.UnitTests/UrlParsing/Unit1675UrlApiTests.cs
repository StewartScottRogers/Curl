using Curl.Conformance.UnitProgram.UrlParsing;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1675UrlApiTests
{
    [TestMethod]
    public async Task RunAsync_CurlFunctions_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1675", new Unit1675UrlApi().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_IPv4RowsWrong_WritesEachFailureAndAbortsBeforeTheNextSection()
    {
        using StringWriter standardError = new();
        bool encoded = false;
        Unit1675Functions functions = Unit1675Functions.Curl with
        {
            NormalizeIPv4 = host => host switch
            {
                "0" => null,
                "00" => "1.1.1.1",
                "0x" => "0.0.0.0",
                _ => Unit1675Functions.Curl.NormalizeIPv4(host),
            },
            UrlEncode = (text, relative, query) =>
            {
                encoded = true;
                return text;
            },
        };

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(functions), standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.IsFalse(encoded);
        Assert.AreEqual(
            Lines(
                "ipv4_normalize('0x') succeeded unexpectedly: got '0.0.0.0'",
                "ipv4_normalize('0') failed: expected '0.0.0.0', got '0'",
                "ipv4_normalize('00') failed: expected '0.0.0.0', got '1.1.1.1'",
                "unit1675.c Abort assertion 'ipv4_normalize tests failed' met"),
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_EncodedTextDiffers_WritesTheQueryValueAndAborts()
    {
        using StringWriter standardError = new();
        Unit1675Functions functions = Unit1675Functions.Curl with
        {
            UrlEncode = (text, relative, query) => text == "~-._" && query == Unit1675Functions.QueryYes ? "x" : Unit1675Functions.Curl.UrlEncode(text, relative, query),
        };

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(functions), standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(
            Lines("urlencode_str('~-._', query=2) failed: expected '~-._', got 'x'", "unit1675.c Abort assertion 'urlencode_str tests failed' met"),
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_IPv6RowsWrong_WritesHostZoneAndUnexpectedSuccessFailures()
    {
        using StringWriter standardError = new();
        Unit1675Functions functions = Unit1675Functions.Curl with
        {
            ParseIPv6 = host => host switch
            {
                "[::1]" => null,
                "[fe80::1%eth0]" => ("[fe80::2]", null),
                "[fe80::1%25eth0]" => ("[fe80::1]", "eth1"),
                _ => ("[::]", null),
            },
        };

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(functions), standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(
            Lines(
                "ipv6_parse('[::1]') host failed: expected '[::1]', got 'error'",
                "ipv6_parse('[fe80::1%eth0]') host failed: expected '[fe80::1]', got '[fe80::2]'",
                "ipv6_parse('[fe80::1%eth0]') zone failed: expected 'eth0', got '(null)'",
                "ipv6_parse('[fe80::1%25eth0]') zone failed: expected 'eth0', got 'eth1'",
                "ipv6_parse('[::1') succeeded unexpectedly",
                "ipv6_parse('[]') succeeded unexpectedly",
                "unit1675.c Abort assertion 'ipv6_parse tests failed' met"),
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_FileRowsWrongOffWindows_WritesBothFailuresAndSkipsTheWindowsRows()
    {
        using StringWriter standardError = new();
        Unit1675Functions functions = Unit1675Functions.Curl with
        {
            ParseFilePath = url => url switch
            {
                "file:///etc/hosts" => null,
                "file:foo" => "foo",
                _ => Unit1675Functions.Curl.ParseFilePath(url),
            },
            IsWindows = false,
        };

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(functions), standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(
            Lines(
                "parse_file('file:///etc/hosts') failed: expected path '/etc/hosts'; got path ''",
                "Unexpectedly fine for input 'file:foo'",
                "unit1675.c Abort assertion 'parse_file tests failed' met"),
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_WindowsRowsWrongOnWindows_WritesTheirFailures()
    {
        using StringWriter standardError = new();
        Unit1675Functions functions = Unit1675Functions.Curl with
        {
            ParseFilePath = url => url.Contains("c:", StringComparison.Ordinal) ? "/c:/windows/system32" : Unit1675Functions.Curl.ParseFilePath(url),
            IsWindows = true,
        };

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(functions), standardError);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(standardError.ToString(), "parse_file('file://localhost/c:/windows/system32') failed: expected path 'c:/windows/system32'; got path '/c:/windows/system32'");
    }

    [TestMethod]
    public async Task RunAsync_OriginNeverMatches_WritesEachRowThatShouldMatch()
    {
        using StringWriter standardError = new();
        Unit1675Functions functions = Unit1675Functions.Curl with { IsSameOrigin = (_, _) => false };

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(functions), standardError);

        Assert.AreEqual(1, result.ExitCode);
        string written = standardError.ToString();
        StringAssert.Contains(written, "ERROR: 2 base http://host:123/x and href ://:/y did not match");
        StringAssert.Contains(written, "ERROR: 8 base https://host/x and href https://host:443/y did not match");
        Assert.DoesNotContain("ERROR: 3 ", written);
        StringAssert.EndsWith(written, "unit1675.c Abort assertion 'same_origin tests failed' met" + Environment.NewLine);
    }

    [TestMethod]
    public async Task RunAsync_OriginAlwaysMatches_WritesEachRowThatShouldNot()
    {
        using StringWriter standardError = new();
        Unit1675Functions functions = Unit1675Functions.Curl with { IsSameOrigin = (_, _) => true };

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(functions), standardError);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(standardError.ToString(), "ERROR: 3 base http://host:80/x and href http://host:123/y matched");
    }

    [TestMethod]
    public async Task RunAsync_LoginDiffers_WritesExpectedAndGotAndAborts()
    {
        using StringWriter standardError = new();
        Unit1675Functions functions = Unit1675Functions.Curl with
        {
            ParseLogin = (text, scheme, disallowUser) => (CurlUrlRejection.None, new Unit1675Login("foo", null, null, 3)),
        };

        UnitProgramResult result = await RunAsync(new Unit1675UrlApi(functions), standardError);

        Assert.AreEqual(1, result.ExitCode);
        string written = standardError.ToString();
        StringAssert.Contains(written, "0: parse_hostname_login('foo:bar@host') host failed: expected 'None/foo/bar/o/8', got 'None/foo///3'");
        StringAssert.Contains(written, "3: parse_hostname_login('foo:bar@host') host failed: expected 'UserNotAllowed////0', got 'None/foo///3'");
        StringAssert.EndsWith(written, "unit1675.c Abort assertion 'parse_hostname_login tests failed' met" + Environment.NewLine);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1675UrlApi().RunAsync(null!, CancellationToken.None));
    }

    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + Environment.NewLine));

    private static Task<UnitProgramResult> RunAsync(Unit1675UrlApi program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1675, []) { StandardError = standardError }, CancellationToken.None);
}
