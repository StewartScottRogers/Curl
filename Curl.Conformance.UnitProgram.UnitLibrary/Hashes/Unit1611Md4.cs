using Curl.Cryptography;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1611.c</c>: <c>Curl_md4it</c>, here Curl's hand-built
/// <see cref="Md4"/>.
/// </summary>
public sealed class Unit1611Md4 : DigestUnitProgram
{
    private const int DigestLength = 16;

    /// <inheritdoc />
    public override string ProgramName => "unit1611";

    /// <inheritdoc />
    protected override IReadOnlyList<(byte[] Input, byte[] Expected)> Rows { get; } =
    [
        (Bytes("1"), Convert.FromHexString("8be1ec697b14ad3a53b371436120641d")),
        (Bytes("hello-you-fool"), Convert.FromHexString("a7161cad7ebedbbcf8c723102d2ce20b")),
    ];

    /// <inheritdoc />
    protected override byte[] Digest(byte[] input)
    {
        byte[] output = new byte[DigestLength];
        Md4.HashData(input, output);
        return output;
    }
}
