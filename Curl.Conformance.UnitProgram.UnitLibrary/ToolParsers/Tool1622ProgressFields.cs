using Curl.Conformance.UnitProgram.Hashes;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/tunit/tool1622.c</c>: the 63 <c>timebuf</c> rows and the 63 and five
/// <c>max5data</c> rows written to standard output with their width checks, then the exact
/// <c>timecases</c> and <c>datacases</c> rows, through <see cref="ParallelProgressMeterText.Time(long)"/> and
/// <see cref="ParallelProgressMeterText.Size(long)"/>.
/// </summary>
public sealed class Tool1622ProgressFields : IUnitProgram
{
    private static readonly (long Value, string Output)[] TimeCases =
    [
        (0, "        "),
        (1, "00:00:01"),
        (524287, "  6d 01h"),
        (134217727, " 51m 23d"),
        (4294967295, "    136y"),
        (4398046511103, " >99999y"),
    ];

    private static readonly (long Value, string Output)[] DataCases =
    [
        (0, "    0"),
        (99999, "99999"),
        (100000, "97.6k"),
        (131072, " 128k"),
        (12645826, "12.0M"),
        (1099445657078333, " 999T"),
    ];

    /// <inheritdoc/>
    public string ProgramName => "tool1622";

    /// <inheritdoc/>
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        StandardOutputLines output = new(context);
        await output.WriteLineAsync("timebuf", cancellationToken).ConfigureAwait(false);
        foreach (long seconds in ProgressFieldRows.Doubling())
        {
            string text = ParallelProgressMeterText.Time(seconds);
            await output.WriteLineAsync(ProgressFieldRows.Line(seconds, text), cancellationToken).ConfigureAwait(false);
            checks.FailUnless(text.Length == 8, "timebuf output width");
        }

        await output.WriteLineAsync("max5data", cancellationToken).ConfigureAwait(false);
        foreach (long bytes in ProgressFieldRows.Doubling())
        {
            string text = ParallelProgressMeterText.Size(bytes);
            await output.WriteLineAsync(ProgressFieldRows.Line(bytes, text), cancellationToken).ConfigureAwait(false);
            checks.FailUnless(text.Length == 5, "max5data output width");
        }

        foreach (long bytes in ProgressFieldRows.ByteCounts)
        {
            string text = ParallelProgressMeterText.Size(bytes);
            await output.WriteLineAsync(ProgressFieldRows.Line(bytes, text), cancellationToken).ConfigureAwait(false);
            checks.FailUnless(text.Length == 5, "max5data check output width");
        }

        foreach ((long value, string expected) in TimeCases)
        {
            checks.FailUnless(ParallelProgressMeterText.Time(value) == expected, expected);
        }

        foreach ((long value, string expected) in DataCases)
        {
            checks.FailUnless(ParallelProgressMeterText.Size(value) == expected, expected);
        }

        return checks.Result;
    }
}
