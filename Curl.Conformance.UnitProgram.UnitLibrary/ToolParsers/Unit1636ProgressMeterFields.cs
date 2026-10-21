using Curl.Output;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/unit/unit1636.c</c>: the 63 <c>time2str</c> rows and the 63 and five
/// <c>max6out</c> rows written to standard output through <see cref="ProgressMeterFields.Time(long)"/> and
/// <see cref="ProgressMeterFields.Size(long)"/>, each followed by <c>^^ was too long!</c> when its text is not
/// seven or six characters. Like the C program it counts no failure: the case's stdout is the check.
/// </summary>
public sealed class Unit1636ProgressMeterFields : IUnitProgram
{
    private const string TooLongLine = "^^ was too long!";

    /// <inheritdoc/>
    public string ProgramName => "unit1636";

    /// <inheritdoc/>
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        StandardOutputLines output = new(context);
        await output.WriteLineAsync("time2str", cancellationToken).ConfigureAwait(false);
        foreach (long seconds in ProgressFieldRows.Doubling())
        {
            await WriteRowAsync(output, seconds, ProgressMeterFields.Time(seconds), 7, cancellationToken).ConfigureAwait(false);
        }

        await output.WriteLineAsync("max6out", cancellationToken).ConfigureAwait(false);
        foreach (long bytes in ProgressFieldRows.Doubling().Concat(ProgressFieldRows.ByteCounts))
        {
            await WriteRowAsync(output, bytes, ProgressMeterFields.Size(bytes), 6, cancellationToken).ConfigureAwait(false);
        }

        return UnitProgramResult.Success;
    }

    /// <summary>Writes one row's line, and <c>^^ was too long!</c> after it when <paramref name="text"/> is not <paramref name="width"/> characters.</summary>
    /// <param name="output">The program's standard output.</param>
    /// <param name="value">The value formatted.</param>
    /// <param name="text">Its text.</param>
    /// <param name="width">The width the text must have.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once the lines are written.</returns>
    public static async Task WriteRowAsync(StandardOutputLines output, long value, string text, int width, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(text);
        await output.WriteLineAsync(ProgressFieldRows.Line(value, text), cancellationToken).ConfigureAwait(false);
        if (text.Length != width)
        {
            await output.WriteLineAsync(TooLongLine, cancellationToken).ConfigureAwait(false);
        }
    }
}
