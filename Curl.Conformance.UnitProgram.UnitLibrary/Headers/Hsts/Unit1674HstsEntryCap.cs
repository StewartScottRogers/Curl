using System.Text;
using Curl.Core.Hsts;

namespace Curl.Conformance.UnitProgram.Headers.Hsts;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1674.c</c>: writes an HSTS file of
/// <c>MAX_HSTS_ENTRIES</c> plus five entries to the path named by the first argument, loads it into an
/// <see cref="HstsCache" /> and writes <c>OK</c> to standard output when exactly
/// <see cref="HstsCache.MaxEntries" /> are held.
/// </summary>
/// <param name="curlTime">The second the cache's clock stands at: the case's <c>CURL_TIME</c>.</param>
/// <param name="entriesWritten">How many entries the file holds; <see cref="UpstreamEntriesWritten" /> for the port itself.</param>
public sealed class Unit1674HstsEntryCap(long curlTime, int entriesWritten) : IUnitProgram
{
    /// <summary>The C program's <c>MAX_HSTS_ENTRIES + 5</c>.</summary>
    public const int UpstreamEntriesWritten = HstsCache.MaxEntries + 5;

    /// <inheritdoc />
    public string ProgramName => "unit1674";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        StringBuilder file = new();
        for (int index = 0; index < entriesWritten; index++)
        {
            file.Append("example").Append(index).Append(".com \"20991231 12:34:56\"\n");
        }

        string fileName = context.Arguments[0];
        await File.WriteAllTextAsync(fileName, file.ToString(), Encoding.Latin1, cancellationToken);
        HstsCache cache = new(new CurlTimeClock(curlTime));
        cache.ReadFile(await File.ReadAllTextAsync(fileName, Encoding.Latin1, cancellationToken));

        int count = cache.Entries.Count;
        bool capped = count == HstsCache.MaxEntries;
        string line = capped ? "OK\n" : $"Not OK: {count} entries\n";
        await context.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(line), cancellationToken);
        return new UnitProgramResult(capped ? 0 : 1);
    }
}
