using Curl.Conformance.UnitProgram.Hashes;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.Asn1.Names;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1397.c</c>: <c>Curl_cert_hostcheck</c>, here
/// <see cref="SchannelCommonNameCheck.PatternMatchesHost"/>, over the C program's table row for row. The
/// C program's <c>\xff</c> and <c>\xfe</c> bytes are the Latin-1 characters of the same value. Both the
/// Schannel and the OpenSSL build compile the checks (ADR-0723), so every row runs on every platform.
/// </summary>
/// <param name="patternMatchesHost">Whether a certificate name pattern matches a host; <see cref="SchannelCommonNameCheck.PatternMatchesHost"/> unless a test injects another.</param>
public sealed class Unit1397CertHostCheck(Func<string, string, bool> patternMatchesHost) : IUnitProgram
{
    private static readonly (string Host, string Pattern, bool Match)[] Rows =
    [
        (".hello.com", "*.hello.com", false),
        ("a.hello.com", "*.hello.com", true),
        (string.Empty, string.Empty, false),
        ("a", string.Empty, false),
        (string.Empty, "b", false),
        ("a", "b", false),
        ("aa", "bb", false),
        ("ÿ", "ÿ", true),
        ("aa.aa.aa", "aa.aa.bb", false),
        ("aa.aa.aa", "aa.aa.aa", true),
        ("aa.aa.aa", "*.aa.bb", false),
        ("aa.aa.aa", "*.aa.aa", true),
        ("192.168.0.1", "192.168.0.1", true),
        ("192.168.0.1", "*.168.0.1", false),
        ("192.168.0.1", "*.0.1", false),
        ("h.ello", "*.ello", false),
        ("h.ello.", "*.ello", false),
        ("h.ello", "*.ello.", false),
        ("h.e.llo", "*.e.llo", true),
        ("h.e.llo", " *.e.llo", false),
        (" h.e.llo", "*.e.llo", true),
        ("h.e.llo.", "*.e.llo", true),
        ("*.e.llo.", "*.e.llo", true),
        ("************.e.llo.", "*.e.llo", true),
        (new string('A', 60) + new string('B', 60) + new string('C', 60) + new string('D', 60) + new string('E', 60) + ".e.llo.", "*.e.llo", true),
        ("þþ.e.llo.", "*.e.llo", true),
        ("h.e.llo.", "*.e.llo.", true),
        ("h.e.llo", "*.e.llo.", true),
        (".h.e.llo", "*.e.llo.", false),
        ("h.e.llo", "*.*.llo.", false),
        ("h.e.llo", "h.*.llo", false),
        ("h.e.llo", "h.e.*", false),
        ("hello", "*.ello", false),
        ("hello", "**llo", false),
        ("bar.foo.example.com", "*.example.com", false),
        ("foo.example.com", "*.example.com", true),
        ("baz.example.net", "b*z.example.net", false),
        ("foobaz.example.net", "*baz.example.net", false),
        ("xn--l8j.example.local", "x*.example.local", false),
        ("xn--l8j.example.net", "*.example.net", true),
        ("xn--l8j.example.net", "*j.example.net", false),
        ("xn--l8j.example.net", "xn--l8j.example.net", true),
        ("xn--l8j.example.net", "xn--l8j.*.net", false),
        ("xl8j.example.net", "*.example.net", true),
        ("fe80::3285:a9ff:fe46:b619", "*::3285:a9ff:fe46:b619", false),
        ("fe80::3285:a9ff:fe46:b619", "fe80::3285:a9ff:fe46:b619", true),
    ];

    /// <summary>Creates the port over <see cref="SchannelCommonNameCheck.PatternMatchesHost"/>.</summary>
    public Unit1397CertHostCheck()
        : this(SchannelCommonNameCheck.PatternMatchesHost)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1397";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach (var (host, pattern, match) in Rows)
        {
            checks.FailUnless(patternMatchesHost(pattern, host) == match, $"HOST: {host} PTRN: {pattern} did {(match ? "NOT " : string.Empty)}MATCH");
        }

        return Task.FromResult(checks.Result);
    }
}
