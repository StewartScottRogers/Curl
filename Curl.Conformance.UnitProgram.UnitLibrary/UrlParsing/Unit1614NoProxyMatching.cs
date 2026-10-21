using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1614.c</c>: <c>cidr4_match</c>, <c>cidr6_match</c> and
/// <c>proxy_check_noproxy</c>, here <see cref="NoProxyMatcher.Matches"/>. Curl has no separate CIDR
/// function, so the <c>list4</c> and <c>list6</c> rows ask whether the address is exempt under the one entry
/// <c>network/bits</c>, which is how <c>proxy_check_noproxy</c> reaches <c>cidr4_match</c> and
/// <c>cidr6_match</c> (<c>/0</c> compares the whole address, as <c>bits == 0</c> does there). Every row of
/// <c>list4</c>, <c>list6</c> and <c>list</c> (the <c>USE_IPV6</c> rows included) is checked in the C
/// program's order; a row that disagrees writes the C program's line, and any such row makes the one
/// <c>fail_if(err, "errors")</c> failure.
/// </summary>
/// <param name="matches">Tells whether a host is exempt under a no-proxy list; <see cref="NoProxyMatcher.Matches"/> unless a test injects another.</param>
public sealed class Unit1614NoProxyMatching(Func<string, string?, bool> matches) : IUnitProgram
{
    private static readonly (string Address, string Network, int Bits, bool Match)[] CidrRows =
    [
        // list4
        ("192.160.0.1", "192.160.0.1", 33, false),
        ("192.160.0.1", "192.160.0.1", 32, true),
        ("192.160.0.1", "192.160.0.1", 0, true),
        ("192.160.0.1", "192.160.0.1", 24, true),
        ("192.160.0.1", "192.160.0.1", 26, true),
        ("192.160.0.1", "192.160.0.1", 20, true),
        ("192.160.0.1", "192.160.0.1", 18, true),
        ("192.160.0.1", "192.160.0.1", 12, true),
        ("192.160.0.1", "192.160.0.1", 8, true),
        ("192.160.0.1", "10.0.0.1", 8, false),
        ("192.160.0.1", "10.0.0.1", 32, false),
        ("192.160.0.1", "10.0.0.1", 0, false),

        // list6
        ("::1", "::1", 0, true),
        ("::1", "::1", 128, true),
        ("::1", "0:0::1", 128, true),
        ("::1", "0:0::1", 129, false),
        ("fe80::ab47:4396:55c9:8474", "fe80::ab47:4396:55c9:8474", 64, true),
    ];

    private static readonly (string Host, string NoProxy, bool Match)[] NoProxyRows =
    [
        ("www.example.com", "localhost .example.com .example.de", false),
        ("www.example.com", "localhost,.example.com,.example.de", true),
        ("www.example.com.", "localhost,.example.com,.example.de", true),
        ("example.com", "localhost,.example.com,.example.de", true),
        ("example.com.", "localhost,.example.com,.example.de", true),
        ("www.example.com", "localhost,.example.com.,.example.de", true),
        ("www.example.com", "localhost,www.example.com.,.example.de", true),
        ("example.com", "localhost,example.com,.example.de", true),
        ("example.com.", "localhost,example.com,.example.de", true),
        ("nexample.com", "localhost,example.com,.example.de", false),
        ("www.example.com", "localhost,example.com,.example.de", true),
        ("127.0.0.1", "127.0.0.1,localhost", true),
        ("127.0.0.1", "127.0.0.1,localhost,", true),
        ("127.0.0.1", "127.0.0.1/8,localhost,", true),
        ("127.0.0.1", "127.0.0.1/28,localhost,", true),
        ("127.0.0.1", "127.0.0.1/31,localhost,", true),
        ("127.0.0.1", "localhost,127.0.0.1", true),
        ("127.0.0.1", "localhost,127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.", false),
        ("127.0.0.1", "localhost,127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0.1.127.0.0", false),
        ("localhost", "localhost,127.0.0.1", true),
        ("localhost", "127.0.0.1,localhost", true),
        ("foobar", "barfoo", false),
        ("foobar", "foobar", true),
        ("192.168.0.1", "foobar", false),
        ("192.168.0.1", "192.168.0.0/16", true),
        ("192.168.0.1", "192.168.0.0/16a", false),
        ("192.168.0.1", "192.168.0.0/16 ", true),
        ("192.168.0.1", "192.168.0.0/a16", false),
        ("192.168.0.1", "192.168.0.0/ 16", false),
        ("192.168.0.1", "192.168.0.0/24", true),
        ("192.168.0.1", "192.168.0.0/32", false),
        ("192.168.0.1", "192.168.0.1/32", true),
        ("192.168.0.1", "192.168.0.1/33", false),
        ("192.168.0.1", "192.168.0.0", false),
        ("192.168.1.1", "192.168.0.0/24", false),
        ("192.168.1.1", "192.168.0.0/33", false),
        ("192.168.1.1", "foo, bar, 192.168.0.0/24", false),
        ("192.168.1.1", "foo, bar, 192.168.0.0/16", true),
        ("::1", "foo, bar, 192.168.0.0/16", false),
        ("::1", "foo, bar, ::1/64", true),
        ("::1", "::1/64", true),
        ("::1", "::1/96", true),
        ("::1", "::1/129", false),
        ("::1", "::1/128", true),
        ("::1", "::1/127", true),
        ("::1", "::1/a127", false),
        ("::1", "::1/127a", false),
        ("::1", "::1/ 127", false),
        ("::1", "::1/127 ", true),
        ("::1", "::1/126", true),
        ("::1", "::1/125", true),
        ("::1", "::1/124", true),
        ("::1", "::1/123", true),
        ("::1", "::1/122", true),
        ("2001:db8:8000::1", "2001:db8::/65", false),
        ("2001:db8:8000::1", "2001:db8::/66", false),
        ("2001:db8:8000::1", "2001:db8::/67", false),
        ("2001:db8:8000::1", "2001:db8::/68", false),
        ("2001:db8:8000::1", "2001:db8::/69", false),
        ("2001:db8:8000::1", "2001:db8::/70", false),
        ("2001:db8:8000::1", "2001:db8::/71", false),
        ("2001:db8:8000::1", "2001:db8::/72", false),
        ("2001:db8::1", "2001:db8::/65", true),
        ("2001:db8::1", "2001:db8::/66", true),
        ("2001:db8::1", "2001:db8::/67", true),
        ("2001:db8::1", "2001:db8::/68", true),
        ("2001:db8::1", "2001:db8::/69", true),
        ("2001:db8::1", "2001:db8::/70", true),
        ("2001:db8::1", "2001:db8::/71", true),
        ("2001:db8::1", "2001:db8::/72", true),
        ("::1", "::1/129", false),
        ("bar", "foo, bar, ::1/64", true),
        ("BAr", "foo, bar, ::1/64", true),
        ("BAr", "foo,,,,,              bar, ::1/64", true),
        ("www.example.com", "foo, .example.com", true),
        ("www.example.com", "www2.example.com, .example.net", false),
        ("example.com", ".example.com, .example.net", true),
        ("nonexample.com", ".example.com, .example.net", false),
    ];

    /// <summary>Creates the port over <see cref="NoProxyMatcher.Matches"/>.</summary>
    public Unit1614NoProxyMatching()
        : this(NoProxyMatcher.Matches)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1614";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        TextWriter standardError = context.StandardError;
        int errors = 0;
        foreach ((string address, string network, int bits, bool match) in CidrRows)
        {
            errors += CountMismatch(matches(address, $"{network}/{bits}"), match, $"{address} in {network}/{bits}", standardError);
        }

        foreach ((string host, string noProxy, bool match) in NoProxyRows)
        {
            errors += CountMismatch(matches(host, noProxy), match, $"{host} in {noProxy}", standardError);
        }

        UnitChecks checks = new(standardError, ProgramName);
        checks.FailUnless(errors == 0, "errors");
        return Task.FromResult(checks.Result);
    }

    private static int CountMismatch(bool actual, bool expected, string row, TextWriter standardError)
    {
        if (actual == expected)
        {
            return 0;
        }

        standardError.WriteLine($"{row} should {(expected ? string.Empty : "not ")}match");
        return 1;
    }
}
