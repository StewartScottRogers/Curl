using Curl.Ntlm;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1600.c</c>: <c>Curl_ntlm_core_mk_nt_hash</c> into a
/// 21-byte buffer, here <see cref="NtlmOneWayFunctions.ComputeNtOwfV1"/> zero-padded the same way.
/// </summary>
public sealed class Unit1600NtHash : DigestUnitProgram
{
    private const int OutputLength = 21;

    /// <inheritdoc />
    public override string ProgramName => "unit1600";

    /// <inheritdoc />
    protected override IReadOnlyList<(byte[] Input, byte[] Expected)> Rows { get; } =
    [
        (Bytes("1"), Convert.FromHexString("69943c5e63b4d2c104dbbcc15138b72b0000000000")),
        (Bytes("hello-you-fool"), Convert.FromHexString("39af87a6750a7a00baa0d34f049ec1d00000000000")),
        (Bytes(new string('A', 300)), Convert.FromHexString("369dae06847ee1c14a9439ea6f448c650000000000")),
    ];

    /// <inheritdoc />
    protected override byte[] Digest(byte[] input)
    {
        byte[] output = new byte[OutputLength];
        NtlmOneWayFunctions.ComputeNtOwfV1(System.Text.Encoding.Latin1.GetString(input)).CopyTo(output, 0);
        return output;
    }
}
