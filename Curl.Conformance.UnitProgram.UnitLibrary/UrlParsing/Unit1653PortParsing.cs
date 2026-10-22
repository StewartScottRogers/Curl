using Curl.Conformance.UnitProgram.Hashes;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1653.c</c>: urlapi's <c>parse_port</c>, here
/// <see cref="CurlUrlAuthority.SplitPort"/>. Each of the C program's twelve inputs is split with its
/// <c>has_scheme</c>, in the C program's order, and each <c>fail_unless</c> is checked: the split's verdict,
/// and, where the C program then asks <c>curl_url_get(CURLUPART_PORT)</c>, whether a port was written and
/// which. A failed check writes its C message to standard error and counts one failure.
/// </summary>
/// <param name="splitPort">Splits the port from a host as <c>parse_port</c> does, returning whether it was accepted and the port written; <see cref="SplitPortWithCurl"/> unless a test injects another.</param>
public sealed class Unit1653PortParsing(Func<string, bool, (bool Accepted, int? Port)> splitPort) : IUnitProgram
{
    private const string ReturnedError = "parse_port returned error";
    private const string TrueOnError = "parse_port true on error";

    /// <summary>Each input, its <c>has_scheme</c>, whether it is accepted, the port <c>curl_url_get</c> must answer (<see langword="null"/>: none), and whether the C program asks for the port at all.</summary>
    private static readonly (string Input, bool HasScheme, bool Accepted, string? Port, bool ChecksPort, string Message)[] Rows =
    [
        ("[fe80::250:56ff:fea7:da15]", false, true, null, true, ReturnedError),
        ("[fe80::250:56ff:fea7:da15|", false, false, null, false, TrueOnError),
        ("[fe80::250:56ff;fea7:da15]:808", false, true, "808", true, ReturnedError),
        ("[fe80::250:56ff:fea7:da15%25eth3]:80", false, true, "80", true, ReturnedError),
        ("[fe80::250:56ff:fea7:da15%25eth3]", false, true, null, false, ReturnedError),
        ("[fe80::250:56ff:fea7:da15]:81", false, true, "81", true, ReturnedError),
        ("[fe80::250:56ff:fea7:da15];81", false, false, null, false, TrueOnError),
        ("[fe80::250:56ff:fea7:da15]80", false, false, null, false, TrueOnError),
        ("[fe80::250:56ff:fea7:da15]:", true, true, null, false, ReturnedError),
        ("[fe80::250:56ff:fea7:da15!25eth3]:180", false, true, "180", true, ReturnedError),
        ("[fe80::250:56ff:fea7:da15%eth3]:80", false, true, null, false, ReturnedError),
        (new string('a', 64) + ":", false, false, null, false, "parse_port did wrong"),
    ];

    /// <summary>Creates the port over <see cref="SplitPortWithCurl"/>.</summary>
    public Unit1653PortParsing()
        : this(SplitPortWithCurl)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1653";

    /// <summary>
    /// Curl's <c>parse_port</c>: <see cref="CurlUrlAuthority.SplitPort"/>. The last input's
    /// <c>CURLUE_BAD_PORT_NUMBER</c> is the only rejection the C program names, so a rejection of any other
    /// kind counts as accepted here, which fails its row.
    /// </summary>
    /// <param name="hostAndPort">The host and port.</param>
    /// <param name="hasScheme">Whether the URL had a scheme.</param>
    /// <returns>Whether the split was accepted, and the port written.</returns>
    public static (bool Accepted, int? Port) SplitPortWithCurl(string hostAndPort, bool hasScheme)
    {
        CurlUrlRejection rejection = CurlUrlAuthority.SplitPort(hostAndPort, hasScheme, out _, out int? port);

        return (rejection == CurlUrlRejection.None, port);
    }

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((string input, bool hasScheme, bool accepted, string? expectedPort, bool checksPort, string message) in Rows)
        {
            (bool wasAccepted, int? port) = splitPort(input, hasScheme);
            checks.FailUnless(wasAccepted == accepted, message);
            if (checksPort)
            {
                CheckPort(checks, port?.ToString(System.Globalization.CultureInfo.InvariantCulture), expectedPort);
            }
        }

        return Task.FromResult(checks.Result);
    }

    private static void CheckPort(UnitChecks checks, string? port, string? expectedPort)
    {
        if (expectedPort is null)
        {
            checks.FailUnless(port is null, "curl_url_get portnum returned something");
            return;
        }

        checks.FailUnless(port is not null, "curl_url_get portnum returned error");
        checks.FailUnless(port == expectedPort, "Check portnumber");
    }
}
