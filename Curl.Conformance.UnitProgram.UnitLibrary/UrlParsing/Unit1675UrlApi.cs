using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1675.c</c>, six sections of the URL API checked in the C
/// program's order, each over its Curl counterpart: <c>ipv4_normalize</c>
/// (<see cref="CurlUrlIPv4Address.TryNormalize"/>), <c>urlencode_str</c> (<see cref="UrlPartText.UrlEncodeString"/>),
/// <c>ipv6_parse</c> (<see cref="CurlUrlHost.TryNormalize"/>), <c>parse_file</c> (<see cref="CurlUrl.TryParse(string, bool, out CurlUrl?)"/>
/// of a <c>file:</c> URL, its two <c>_WIN32</c> rows on Windows only), <c>Curl_url_same_origin</c>
/// (<see cref="RedirectFollower.IsSameOrigin"/>, every <c>USE_SSL</c> row included) and
/// <c>parse_hostname_login</c> (<see cref="CurlUrlAuthority.Parse"/>). A row that fails writes the C program's
/// line; a section with any failed row writes its <c>abort_if</c> line, counts one failure and ends the
/// program, as <c>abort_if</c>'s jump to <c>unit_test_abort</c> does.
/// </summary>
/// <param name="functions">The Curl functions each section checks; <see cref="Unit1675Functions.Curl"/> unless a test injects others.</param>
public sealed class Unit1675UrlApi(Unit1675Functions functions) : IUnitProgram
{
    private static readonly (string In, string? Out)[] IPv4Rows =
    [
        ("0x.0x.0x.0x", null), ("0x.0x.0x", null), ("0x.0x", null), ("0x", null),
        ("0", "0.0.0.0"), ("00", "0.0.0.0"), ("00000000000", "0.0.0.0"), ("127.0.0.1", "127.0.0.1"),
        ("0177.0.0.1", "127.0.0.1"), ("00177.0.0.1", "127.0.0.1"), ("0x7f.0.0.1", "127.0.0.1"), ("0x07f.0.0.1", "127.0.0.1"),
        ("1", "0.0.0.1"), ("010", "0.0.0.8"), ("001", "0.0.0.1"), ("127", "0.0.0.127"),
        ("127.1", "127.0.0.1"), ("127.0.1", "127.0.0.1"), ("1.16777215", "1.255.255.255"), ("1.16777216", null),
        ("1.1.65535", "1.1.255.255"), ("1.1.65536", null), ("0x7f000001", "127.0.0.1"), ("0x7F000001", "127.0.0.1"),
        ("0x7g000001", null), ("2130706433", "127.0.0.1"), ("017700000001", "127.0.0.1"), ("000000000017700000001", "127.0.0.1"),
        ("192.168.0.1", "192.168.0.1"), ("0300.0250.0000.0001", "192.168.0.1"), ("0xc0.0xa8.0.1", "192.168.0.1"), ("0xc0a80001", "192.168.0.1"),
        ("3232235521", "192.168.0.1"), ("4294967294", "255.255.255.254"), ("4294967295", "255.255.255.255"), ("037777777777", "255.255.255.255"),
        ("0xFFFFFFFF", "255.255.255.255"), ("0xFFFFFfff", "255.255.255.255"), ("1.2.3.4.5", null), ("256.0.0.1", null),
        ("1.256.0.1", null), ("1.1.256.1", null), ("1.0.0.256", null), ("0x100.0.0.1", null),
        ("1.0x100.0.1", null), ("1.1.0x100.1", null), ("1.1.1.0x100", null), ("0400.0.0.1", null),
        ("4.0400.0.1", null), ("4.4.0400.1", null), ("4.4.4.0400", null), ("4294967296", null),
        ("040000000000", null), ("0x100000000", null), ("1.2.3.-4", null), ("1.2.-3.4", null),
        ("1.-2.3.4", null), ("-1.2.3.4", null), ("-12", null), ("-12.1", null),
        ("-12.2.3", null), (" 1.2.3.4", null), ("1. 2.3.4", null), ("1.2. 3.4", null),
        ("1.2.3. 4", null),
    ];

    // A C byte from 0x80 up is the lone surrogate U+DC00 plus the byte, as UrlByteText carries it (ADR-0697).
    private static readonly (string In, bool Relative, int Query, string Out)[] EncodeRows =
    [
        ("http://leave\x01/hello\x01world", false, Unit1675Functions.QueryNo, "http://leave\x01/hello%01world"),
        ("http://leave/hello\x01world", false, Unit1675Functions.QueryNo, "http://leave/hello%01world"),
        ("http://le ave/hello\x01world", false, Unit1675Functions.QueryNo, "http://le ave/hello%01world"),
        ("hello\x01world", true, Unit1675Functions.QueryNo, "hello%01world"),
        ("hello\uDCF0world", true, Unit1675Functions.QueryNo, "hello%F0world"),
        ("hello world", true, Unit1675Functions.QueryNo, "hello%20world"),
        ("hello%20world", true, Unit1675Functions.QueryNo, "hello%20world"),
        ("hello world", true, Unit1675Functions.QueryYes, "hello+world"),
        ("a+b c", true, Unit1675Functions.QueryNo, "a+b%20c"),
        ("a%20b%20c", true, Unit1675Functions.QueryNo, "a%20b%20c"),
        ("a%aab%aac", true, Unit1675Functions.QueryNo, "a%AAb%AAc"),
        ("a%aab%AAc", true, Unit1675Functions.QueryNo, "a%AAb%AAc"),
        ("w%w%x", true, Unit1675Functions.QueryNo, "w%w%x"),
        ("w%wf%xf", true, Unit1675Functions.QueryNo, "w%wf%xf"),
        ("w%fw%fw", true, Unit1675Functions.QueryNo, "w%fw%fw"),
        ("a+b c", true, Unit1675Functions.QueryYes, "a+b+c"),
        ("/foo/bar", true, Unit1675Functions.QueryNo, "/foo/bar"),
        ("/foo/bar", true, Unit1675Functions.QueryYes, "/foo/bar"),
        ("/foo/ bar", true, Unit1675Functions.QueryNo, "/foo/%20bar"),
        ("/foo/ bar", true, Unit1675Functions.QueryYes, "/foo/+bar"),
        ("~-._", true, Unit1675Functions.QueryNo, "~-._"),
        ("~-._", true, Unit1675Functions.QueryYes, "~-._"),
        ("foo bar?foo bar", true, Unit1675Functions.QueryNo, "foo%20bar?foo%20bar"),
        ("foo bar?foo bar", true, Unit1675Functions.QueryNotYet, "foo%20bar?foo+bar"),
    ];

    private static readonly (string In, string? Host, string? Zone)[] IPv6Rows =
    [
        ("[::1]", "[::1]", null),
        ("[fe80::1%eth0]", "[fe80::1]", "eth0"),
        ("[fe80::1%25eth0]", "[fe80::1]", "eth0"),
        ("[::1", null, null),
        ("[]", null, null),
    ];

    private static readonly (string In, string? Path, bool Fine)[] FileRows =
    [
        ("file:///etc/hosts", "/etc/hosts", true),
        ("file://localhost/etc/hosts", "/etc/hosts", true),
        ("file://apple/etc/hosts", "/etc/hosts", false),
        ("file:foo", null, false),
        ("file:./", null, false),
        ("file:?q", null, false),
        ("file:#f", null, false),
    ];

    private static readonly (string In, string? Path, bool Fine)[] WindowsFileRows =
    [
        ("file:///c:/windows/system32", "c:/windows/system32", true),
        ("file://localhost/c:/windows/system32", "c:/windows/system32", true),
    ];

    private static readonly (string Base, string? Scheme, string? Host, string? Port, string Path, bool Match)[] OriginRows =
    [
        ("http://host:123/x", "http", "host", "123", "/y", true),
        ("http://host:123/x", null, "host", "123", "/y", true),
        ("http://host:123/x", null, null, null, "/y", true),
        ("http://host:80/x", "http", "host", "123", "/y", false),
        ("http://host:80/x", "http", "host", null, "/y", true),
        ("http://host/x", "http", "host", "80", "/y", true),
        ("http://host:123/x", "https", "host", "123", "/y", false),
        ("https://host/x", "http", "host", "443", "/y", false),
        ("https://host/x", "https", "host", "443", "/y", true),
    ];

    private static readonly (CurlUrlRejection Uc, string In, string? Scheme, bool DisallowUser, Unit1675Login Login)[] LoginRows =
    [
        (CurlUrlRejection.None, "foo:bar@host", null, false, new("foo", "bar", "o", 8)),
        (CurlUrlRejection.None, "foo:bar;abc@host", "imap", false, new("foo", "bar", "abc", 12)),
        (CurlUrlRejection.None, "foo:bar;abc@host", null, false, new("foo", "bar;abc", "o", 12)),
        (CurlUrlRejection.UserNotAllowed, "foo:bar@host", null, true, new(null, null, null, 0)),
        (CurlUrlRejection.None, "host", null, false, new(null, null, null, 0)),
    ];

    /// <summary>Creates the port over Curl's own functions.</summary>
    public Unit1675UrlApi()
        : this(Unit1675Functions.Curl)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1675";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        TextWriter error = context.StandardError;
        (Func<TextWriter, int> Section, string AbortMessage)[] sections =
        [
            (CheckIPv4, "ipv4_normalize tests failed"),
            (CheckUrlEncode, "urlencode_str tests failed"),
            (CheckIPv6, "ipv6_parse tests failed"),
            (CheckFile, "parse_file tests failed"),
            (CheckSameOrigin, "same_origin tests failed"),
            (CheckLogin, "parse_hostname_login tests failed"),
        ];
        foreach ((Func<TextWriter, int> section, string abortMessage) in sections)
        {
            if (section(error) > 0)
            {
                error.WriteLine($"unit1675.c Abort assertion '{abortMessage}' met");
                return Task.FromResult(new UnitProgramResult(1));
            }
        }

        return Task.FromResult(UnitProgramResult.Success);
    }

    private int CheckIPv4(TextWriter error)
    {
        int fails = 0;
        foreach ((string input, string? expected) in IPv4Rows)
        {
            string? actual = functions.NormalizeIPv4(input);
            fails += expected is null
                ? WriteUnless(error, actual is null, $"ipv4_normalize('{input}') succeeded unexpectedly: got '{actual}'")
                : WriteUnless(error, actual == expected, $"ipv4_normalize('{input}') failed: expected '{expected}', got '{actual ?? input}'");
        }

        return fails;
    }

    private int CheckUrlEncode(TextWriter error)
    {
        int fails = 0;
        foreach ((string input, bool relative, int query, string expected) in EncodeRows)
        {
            string actual = functions.UrlEncode(input, relative, query);
            if (actual != expected)
            {
                error.WriteLine($"urlencode_str('{input}', query={query}) failed: expected '{expected}', got '{actual}'");
                fails++;
            }
        }

        return fails;
    }

    private int CheckIPv6(TextWriter error)
    {
        int fails = 0;
        foreach ((string input, string? expectedHost, string? expectedZone) in IPv6Rows)
        {
            (string Host, string? Zone)? parsed = functions.ParseIPv6(input);
            fails += expectedHost is null
                ? WriteUnless(error, parsed is null, $"ipv6_parse('{input}') succeeded unexpectedly")
                : CheckIPv6Parsed(error, input, parsed, expectedHost, expectedZone);
        }

        return fails;
    }

    private static int CheckIPv6Parsed(TextWriter error, string input, (string Host, string? Zone)? parsed, string expectedHost, string? expectedZone)
    {
        if (parsed is not { } host)
        {
            return WriteUnless(error, false, $"ipv6_parse('{input}') host failed: expected '{expectedHost}', got 'error'");
        }

        int fails = WriteUnless(error, host.Host == expectedHost, $"ipv6_parse('{input}') host failed: expected '{expectedHost}', got '{host.Host}'");
        return expectedZone is null
            ? fails
            : fails + WriteUnless(error, host.Zone == expectedZone, $"ipv6_parse('{input}') zone failed: expected '{expectedZone}', got '{host.Zone ?? "(null)"}'");
    }

    private int CheckFile(TextWriter error)
    {
        int fails = 0;
        IEnumerable<(string In, string? Path, bool Fine)> rows = functions.IsWindows ? FileRows.Concat(WindowsFileRows) : FileRows;
        foreach ((string input, string? expectedPath, bool fine) in rows)
        {
            string? path = functions.ParseFilePath(input);
            fails += WriteUnless(error, fine || path is null, $"Unexpectedly fine for input '{input}'");
            fails += WriteUnless(error, !fine || path == expectedPath, $"parse_file('{input}') failed: expected path '{expectedPath}'; got path '{path}'");
        }

        return fails;
    }

    private int CheckSameOrigin(TextWriter error)
    {
        int fails = 0;
        for (int index = 0; index < OriginRows.Length; index++)
        {
            (string baseUrl, string? scheme, string? host, string? port, string path, bool expectMatch) = OriginRows[index];
            CurlUrl first = CurlUrl.Parse(baseUrl);
            string authority = host is null ? baseUrl[..baseUrl.IndexOf('/', baseUrl.IndexOf("//", StringComparison.Ordinal) + 2)] : $"{scheme ?? first.Scheme}://{host}{(port is null ? string.Empty : ":" + port)}";
            bool match = functions.IsSameOrigin(first, CurlUrl.Parse(authority + path));
            fails += WriteUnless(error, match == expectMatch, $"ERROR: {index} base {baseUrl} and href {scheme}://{host}:{port}{path} {(match ? "matched" : "did not match")}");
        }

        return fails;
    }

    private int CheckLogin(TextWriter error)
    {
        int fails = 0;
        for (int index = 0; index < LoginRows.Length; index++)
        {
            (CurlUrlRejection expectedUc, string input, string? scheme, bool disallowUser, Unit1675Login expected) = LoginRows[index];
            (CurlUrlRejection uc, Unit1675Login login) = functions.ParseLogin(input, scheme, disallowUser);
            fails += WriteUnless(
                error,
                uc == expectedUc && login == expected,
                $"{index}: parse_hostname_login('{input}') host failed: expected '{expectedUc}/{expected.User}/{expected.Password}/{expected.Options}/{expected.Offset}', got '{uc}/{login.User}/{login.Password}/{login.Options}/{login.Offset}'");
        }

        return fails;
    }

    private static int WriteUnless(TextWriter error, bool condition, string message)
    {
        if (condition)
        {
            return 0;
        }

        error.WriteLine(message);
        return 1;
    }
}
