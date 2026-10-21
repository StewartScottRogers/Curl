namespace Curl.Conformance.UnitProgram.Asn1.Names;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit3205.c</c>: its cipher suite name checks are compiled only
/// with mbedTLS or Rustls, so on the Schannel and the OpenSSL build the program does nothing and returns 0,
/// and so does this port, on every platform (ADR-0723).
/// </summary>
public sealed class Unit3205CipherSuiteNames : IUnitProgram
{
    /// <inheritdoc />
    public string ProgramName => "unit3205";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(UnitProgramResult.Success);
    }
}
