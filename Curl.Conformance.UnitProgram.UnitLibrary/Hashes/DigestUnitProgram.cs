namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The shape of upstream's digest unit programs (<c>unit1600.c</c>, <c>unit1601.c</c>, <c>unit1610.c</c>,
/// <c>unit1611.c</c>, <c>unit1612.c</c>, <c>unit1615.c</c>): each row digests one input with the Curl
/// code under test and checks the digest with <c>verify_memory</c>, row by row in the C program's order.
/// </summary>
public abstract class DigestUnitProgram : IUnitProgram
{
    /// <inheritdoc />
    public abstract string ProgramName { get; }

    /// <summary>The C program's rows: each input and the digest it must give.</summary>
    protected abstract IReadOnlyList<(byte[] Input, byte[] Expected)> Rows { get; }

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        for (int row = 0; row < Rows.Count; row++)
        {
            (byte[] input, byte[] expected) = Rows[row];
            checks.VerifyMemory(Digest(input), expected, $"row {row}");
        }

        return Task.FromResult(checks.Result);
    }

    /// <summary>Digests <paramref name="input"/> with the Curl code under test.</summary>
    /// <param name="input">One row's input.</param>
    /// <returns>Its digest, as many bytes as the C program's output buffer.</returns>
    protected abstract byte[] Digest(byte[] input);

    /// <summary>The bytes of <paramref name="text"/>, one per character, as a C string literal holds them.</summary>
    /// <param name="text">Text whose every character is below U+0100.</param>
    /// <returns>Its Latin-1 bytes.</returns>
    protected static byte[] Bytes(string text) => System.Text.Encoding.Latin1.GetBytes(text);
}
