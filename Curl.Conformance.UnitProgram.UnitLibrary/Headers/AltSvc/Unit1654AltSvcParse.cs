using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core.AltSvc;

namespace Curl.Conformance.UnitProgram.Headers.AltSvc;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1654.c</c> over <see cref="AltSvcCache" />: loads the case's
/// alt-svc file and checks it holds 4 entries, checks the count after each of the C program's
/// <c>Curl_altsvc_parse</c> calls, looks the <c>params.example</c> entry up and checks its <c>ma</c>,
/// <c>persist</c> and destination, clears it, and saves the cache to <c>&lt;file&gt;-out</c>, which the case
/// verifies.
/// </summary>
/// <remarks>
/// <c>Curl_altsvc_parse</c> fails only when memory runs out, so the C program's <c>fail_if(result)</c> checks
/// have no counterpart: <see cref="AltSvcCache.ApplyHeader" /> cannot fail.
/// </remarks>
/// <param name="rows">The parse calls; <see cref="UpstreamRows" /> for the port itself.</param>
/// <param name="clock">The clock; a <see cref="CurlTimeClock" /> at the case's <c>CURL_TIME</c> for the port itself.</param>
public sealed class Unit1654AltSvcParse(IReadOnlyList<Unit1654AltSvcRow> rows, TimeProvider clock) : IUnitProgram
{
    /// <summary>The host of the origin the C program looks up after its unknown-parameter header.</summary>
    public const string LookupHost = "params.example";

    /// <summary>The port of the origin the C program looks up.</summary>
    public const int LookupPort = 443;

    private static readonly HashSet<AltSvcAlpn> LookupDestinations = [AltSvcAlpn.H2];

    /// <inheritdoc />
    public string ProgramName => "unit1654";

    /// <summary>The C program's parse calls, in order, with the counts it checks.</summary>
    public static IReadOnlyList<Unit1654AltSvcRow> UpstreamRows { get; } =
    [
        new("example.org", 8080, AltSvcAlpn.H1, "h2=\"example.com:8080\"\r\n", 5),
        new("2.example.org", 8080, AltSvcAlpn.H1, "h3=\":8080\"\r\n", 6),
        new("3.example.org", 8080, AltSvcAlpn.H1, "h2=\"example.com:8080\", h3=\"yesyes.com:8080\"\r\n", 8),
        new("example.org", 80, AltSvcAlpn.H2, "h2=\"example.com:443\"; ma = 120;\r\n", 9),
        new("example.net", 80, AltSvcAlpn.H2, "h2=\"example.net:443\"; ma=\"180\";\r\n", 10),
        new("curl.se", 80, AltSvcAlpn.H1, "h2=\":443\"; ma=180, h3=\":443\"; persist = \"1\"; ma = 120;\r\n", 12),
        new("curl.se", 80, AltSvcAlpn.H1, "clear;\r\n", 10),
        new("curl.se", 80, AltSvcAlpn.H1, "h2=\":443\", h3=\":443\"; persist = \"1\"; ma = 120;\r\n", 12),
        new("curl.se", 80, AltSvcAlpn.H1, "clear\r\n", 10),
        new("5.example.net", 80, AltSvcAlpn.H2, "h6=\"example.net:443\"; ma=\"180\";\r\n", null),
        new("6.example.net", 80, AltSvcAlpn.H2, "h2=\"example.net:443,; ma=\"180\";\r\n", null),
        new("7.example.net", 80, AltSvcAlpn.H2, "h2=\"example.net\"; ma=\"180\";\r\n", null),
        new("8.example.net", 80, AltSvcAlpn.H2, "h2=\"example.net:70000\"; ma=\"180\";\r\n", null),
        new("test.se", 443, AltSvcAlpn.H2, "h2=\"test2.se:443\"; ma=\"180 \" ; unknown=2, h2=\"test3.se:443\"; ma = 120;\r\n", null),
        new(LookupHost, LookupPort, AltSvcAlpn.H1, "h2=\":443\"; long-extension-parameter=token; ext=token; quoted=\"a;\\\"b\"; ma=120; persist=1\r\n", null),
    ];

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        string path = AltSvcCacheFile.PathOf(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        AltSvcCache cache = new(clock);
        await AltSvcCacheFile.LoadAsync(cache, path, cancellationToken).ConfigureAwait(false);
        checks.FailUnless(cache.Entries.Count == 4, "wrong number of entries");

        foreach (Unit1654AltSvcRow row in rows)
        {
            cache.ApplyHeader(row.Header, row.SourceAlpn, row.Host, row.Port);
            checks.FailUnless(row.ExpectedCount is not { } expected || cache.Entries.Count == expected, "wrong number of entries");
        }

        if (CheckLookup(cache, checks))
        {
            cache.ApplyHeader("clear\r\n", AltSvcAlpn.H1, LookupHost, LookupPort);
            await File.WriteAllTextAsync($"{path}-out", cache.FormatFile(Environment.NewLine), cancellationToken).ConfigureAwait(false);
        }

        return checks.Result;
    }

    /// <summary>The C program's <c>Curl_altsvc_lookup</c> of <see cref="LookupHost" /> and its three checks; false when no entry is found, which ends the program before it saves.</summary>
    private static bool CheckLookup(AltSvcCache cache, UnitChecks checks)
    {
        AltSvcMatch? match = cache.FindForOrigin([AltSvcAlpn.H1], LookupHost, LookupPort, LookupDestinations);
        checks.FailUnless(match is not null, "alt-svc entry not found");
        if (match is null)
        {
            return false;
        }

        checks.FailUnless(match.Entry.Expires.ToUnixTimeSeconds() == CurlTimeClock.UpstreamCaseEpochSeconds + 120, "unknown parameters hid ma");
        checks.FailUnless(match.Entry.Persist, "unknown parameters hid persist");
        checks.FailUnless(match.IsSameDestination, "wrong alt-svc destination");
        return true;
    }
}
