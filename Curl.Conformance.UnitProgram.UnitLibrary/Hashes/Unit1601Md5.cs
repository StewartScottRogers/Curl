using System.Security.Cryptography;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1601.c</c>: <c>Curl_md5it</c>, here the BCL's
/// <see cref="MD5"/>, the MD5 Curl's Digest and SASL authentication compute with.
/// </summary>
public sealed class Unit1601Md5 : DigestUnitProgram
{
    /// <inheritdoc />
    public override string ProgramName => "unit1601";

    /// <inheritdoc />
    protected override IReadOnlyList<(byte[] Input, byte[] Expected)> Rows { get; } =
    [
        (Bytes("1"), Convert.FromHexString("c4ca4238a0b923820dcc509a6f75849b")),
        (Bytes("hello-you-fool"), Convert.FromHexString("88670b6d5d742fada5cdf9b682875f22")),
    ];

    /// <inheritdoc />
    protected override byte[] Digest(byte[] input) => MD5.HashData(input);
}
