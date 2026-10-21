using System.Text;
using Curl.Conformance.UnitProgram.Hashes;

namespace Curl.Conformance.UnitProgram.Headers.HeaderValues;

/// <summary>
/// The shape of upstream's <c>unit1625.c</c> and <c>unit1626.c</c>: each row is checked in turn until one
/// fails, whose description is written to standard output; then <c>&lt;n&gt; invokes</c> is written, n being
/// the rows that passed, which the case verifies. A failed row ends the program with
/// <see cref="FailedInitExitCode" />, <c>CURLE_FAILED_INIT</c>; otherwise <see cref="CheckAfterRows" /> runs.
/// </summary>
/// <typeparam name="TRow">One row of the C program's <c>list[]</c>.</typeparam>
/// <param name="rows">The rows to check, in order: the C program's <c>list[]</c> for the port itself.</param>
public abstract class InvokeCountingUnitProgram<TRow>(IReadOnlyList<TRow> rows) : IUnitProgram
{
    /// <summary><c>CURLE_FAILED_INIT</c>, what the C program returns when a row fails.</summary>
    public const int FailedInitExitCode = 2;

    /// <inheritdoc />
    public abstract string ProgramName { get; }

    /// <summary>The rows checked, in order.</summary>
    protected IReadOnlyList<TRow> Rows { get; } = rows ?? throw new ArgumentNullException(nameof(rows));

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int invokes = 0;
        string? mismatch = null;
        while (invokes < Rows.Count && (mismatch = Mismatch(Rows[invokes])) is null)
        {
            invokes++;
        }

        await using (StreamWriter output = new(context.StandardOutput, new UTF8Encoding(false), leaveOpen: true))
        {
            await output.WriteAsync($"{mismatch}{invokes} invokes\n".AsMemory(), cancellationToken);
        }

        if (mismatch is not null)
        {
            return new UnitProgramResult(FailedInitExitCode);
        }

        UnitChecks checks = new(context.StandardError, ProgramName);
        CheckAfterRows(checks);
        return checks.Result;
    }

    /// <summary>Checks one row against the Curl code.</summary>
    /// <param name="row">The row.</param>
    /// <returns><see langword="null" /> when the row passes; otherwise the lines the C program prints for it.</returns>
    protected abstract string? Mismatch(TRow row);

    /// <summary>The checks the C program makes after every row passed; none unless overridden.</summary>
    /// <param name="checks">Where a failed check is counted and written.</param>
    protected virtual void CheckAfterRows(UnitChecks checks)
    {
    }
}
