using System.Text;
using Curl.Api.Codes;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.Asn1.Der;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1657.c</c>: <c>getASN1Element</c>, here
/// <see cref="DerReader.TryRead(byte[], int, int, out DerElement)"/>, over the C program's four rows in order,
/// each <c>n</c> constructed elements of indefinite length (<c>32 80</c>) nested in one another and then
/// <c>n</c> pairs of zero bytes: 3 and 16 deep are read, 17 and 1024 deep refused. It writes its line for
/// each row whose outcome differs. The Schannel build compiles the checks; the OpenSSL build only prints
/// that they are not tested and passes, and so does this port off Windows (ADR-0723).
/// </summary>
/// <param name="readsElement">Whether an element can be read from the start of a buffer; <see cref="DerReader.TryRead(byte[], int, int, out DerElement)"/> unless a test injects another.</param>
/// <param name="readsElements">Whether this is the Schannel build, whose curl compiles <c>getASN1Element</c>.</param>
public sealed class Unit1657IndefiniteNesting(Func<byte[], bool> readsElement, bool readsElements) : IUnitProgram
{
    /// <summary>The line the OpenSSL build's program prints with <c>puts</c> in place of its checks.</summary>
    public const string NotTestedLine = "not tested since getASN1Element() is not built in\n";

    private static readonly (int Depth, CurlCode Result)[] Rows =
    [
        (3, CurlCode.Ok),
        (16, CurlCode.Ok),
        (17, CurlCode.BadFunctionArgument),
        (1024, CurlCode.BadFunctionArgument),
    ];

    /// <summary>Creates the port over <see cref="DerReader.TryRead(byte[], int, int, out DerElement)"/>, running the checks on Windows only, as the platform's curl build does.</summary>
    public Unit1657IndefiniteNesting()
        : this(OperatingSystem.IsWindows())
    {
    }

    /// <summary>Creates the port over <see cref="DerReader.TryRead(byte[], int, int, out DerElement)"/>.</summary>
    /// <param name="readsElements">Whether to run the checks, as the Schannel build does, or print that <c>getASN1Element</c> is not built in.</param>
    public Unit1657IndefiniteNesting(bool readsElements)
        : this(der => DerReader.TryRead(der, 0, der.Length, out _), readsElements)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1657";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!readsElements)
        {
            await context.StandardOutput.WriteAsync(Encoding.ASCII.GetBytes(NotTestedLine), cancellationToken).ConfigureAwait(false);
            return UnitProgramResult.Success;
        }

        bool allOk = true;
        for (int index = 0; index < Rows.Length; index++)
        {
            CurlCode result = readsElement(Nested(Rows[index].Depth)) ? CurlCode.Ok : CurlCode.BadFunctionArgument;
            if (result != Rows[index].Result)
            {
                context.StandardError.WriteLine($"test {index}: expect result {(int)Rows[index].Result}, got {(int)result}");
                allOk = false;
            }
        }

        UnitChecks checks = new(context.StandardError, ProgramName);
        checks.FailUnless(allOk, "some tests of getASN1Element() fails");
        return checks.Result;
    }

    // make1657_nested: depth times 32 80, then depth times 00 00.
    private static byte[] Nested(int depth)
    {
        byte[] der = new byte[depth * 4];
        for (int index = 0; index < depth; index++)
        {
            der[index * 2] = 0x32;
            der[(index * 2) + 1] = 0x80;
        }

        return der;
    }
}
