using System.Text;
using Curl.Core.Hsts;

namespace Curl.Conformance.UnitProgram.Headers.Hsts;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1660.c</c>: loads the HSTS file named by the first argument
/// into an <see cref="HstsCache" />, runs the C program's 23 <c>headers[]</c> rows through
/// <c>Curl_hsts_parse</c> (<see cref="HstsCache.ApplyHeader(string, string)" />) and <c>Curl_hsts</c>
/// (<see cref="HstsCache.Find" />), writes each result and the number of entries held to standard output,
/// looks <c>expire.example</c> up ten times one second apart, and saves the cache to the argument plus
/// <c>.save</c>, which the case verifies.
/// </summary>
/// <param name="curlTime">The second each run's clock starts at: the case's <c>CURL_TIME</c>.</param>
/// <param name="rows">The rows to run; <see cref="UpstreamRows" /> for the port itself.</param>
public sealed class Unit1660Hsts(long curlTime, IReadOnlyList<(string Host, string? LookupHost, string? Header, bool Accepted)> rows) : IUnitProgram
{
    /// <summary>The cases' <c>CURL_TIME</c>, exactly <c>20190124 22:34:21</c> UTC.</summary>
    public const long UpstreamCurlTime = 1_548_369_261;

    /// <summary><c>CURLE_BAD_FUNCTION_ARGUMENT</c>, what <c>Curl_hsts_parse</c> returns for a header it refuses.</summary>
    public const int BadFunctionArgument = 43;

    private const string ExpiringHost = "expire.example";

    private readonly IReadOnlyList<(string Host, string? LookupHost, string? Header, bool Accepted)> rows = rows ?? throw new ArgumentNullException(nameof(rows));

    /// <inheritdoc />
    public string ProgramName => "unit1660";

    /// <summary>
    /// The C program's <c>headers[]</c>: the host a header came from, the host looked up instead when not
    /// <see langword="null" />, the header (<see langword="null" /> to only look up), and whether
    /// <c>Curl_hsts_parse</c> returns <c>CURLE_OK</c>.
    /// </summary>
    public static IReadOnlyList<(string Host, string? LookupHost, string? Header, bool Accepted)> UpstreamRows { get; } =
    [
        ("-", "readfrom.example", null, true),
        ("-", "old.example", null, true),
        ("readfrom.example", null, "max-age=\"0\"", true),
        ("example.com", null, "max-age=\"31536000\"\r\n", true),
        ("example.com", null, "max-age=\"21536000\"\r\n", true),
        ("example.com", null, "max-age=\"21536000\"; \r\n", true),
        ("example.com", null, "max-age=\"21536000\"; includeSubDomains\r\n", true),
        ("example.org", null, "max-age=\"31536000\"\r\n", true),
        ("this.example", null, "max=\"31536\";", false),
        ("this.example", null, "max-age=\"31536", false),
        ("this.example", null, "max-age=31536\"", true),
        ("this.example", null, "max-age=0", true),
        ("another.example", null, "includeSubDomains; ", false),
        ("example.com", null, "max-age=\"21536000\"; includeSubDomains; max-age=\"3\";", false),
        ("2.example.com", null, "max-age=\"21536000\"; includeSubDomains; includeSubDomains;", false),
        ("3.example.com", null, "max-age=\"21536000\"; include; includeSubDomains;", true),
        ("3.example.com", null, "max-age=\"0\"; includeSubDomains;", true),
        ("-", "foo.example.com", null, true),
        ("-", "foo.xample.com", null, true),
        ("forexample.net", null, "max-age=0", true),
        ("-", "forexample.net", null, true),
        ("-", "example.net", null, true),
        (ExpiringHost, null, "max-age=\"7\"\r\n", true),
    ];

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string fileName = context.Arguments[0];
        CurlTimeClock clock = new(curlTime);
        HstsCache cache = new(clock);
        cache.ReadFile(await File.ReadAllTextAsync(fileName, Encoding.Latin1, cancellationToken));

        StringBuilder output = new();
        int failures = 0;
        for (int index = 0; index < rows.Count; index++)
        {
            failures += RunRow(cache, index, output, context.StandardError);
        }

        output.Append("Number of entries: ").Append(cache.Entries.Count).Append('\n');
        for (int second = 0; second < 10; second++)
        {
            AppendEntry(output, cache.Find(ExpiringHost), ExpiringHost);
            clock.Advance();
        }

        await context.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(output.ToString()), cancellationToken);
        await File.WriteAllTextAsync(fileName + ".save", cache.FormatFile(Environment.NewLine, long.MaxValue), Encoding.Latin1, cancellationToken);
        return new UnitProgramResult(failures);
    }

    /// <summary>The C program's <c>showsts</c>.</summary>
    private static void AppendEntry(StringBuilder output, HstsEntry? entry, string host)
    {
        if (entry is null)
        {
            output.Append('\'').Append(host).Append("' is not HSTS\n");
            return;
        }

        output.Append(host).Append(" [").Append(entry.Host).Append("]: ").Append(entry.ExpiresUnixSeconds)
            .Append(entry.IncludeSubDomains ? " includeSubDomains" : string.Empty).Append('\n');
    }

    /// <summary>One row of the C program's loop: the parse, if any, then the lookup; 1 when the parse result differs.</summary>
    private int RunRow(HstsCache cache, int index, StringBuilder output, TextWriter standardError)
    {
        (string host, string? lookupHost, string? header, bool accepted) = rows[index];
        if (header is not null)
        {
            bool parsed = cache.ApplyHeader(header, host);
            if (parsed != accepted)
            {
                standardError.Write($"Curl_hsts_parse({header}) failed: {(parsed ? 0 : BadFunctionArgument)}\n");
                return 1;
            }

            if (!parsed)
            {
                output.Append("Input ").Append(index).Append(": error ").Append(BadFunctionArgument).Append('\n');
                return 0;
            }
        }

        string checkedHost = lookupHost ?? host;
        AppendEntry(output, cache.Find(checkedHost), checkedHost);
        return 0;
    }
}
