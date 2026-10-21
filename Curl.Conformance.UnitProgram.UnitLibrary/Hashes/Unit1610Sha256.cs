using System.Security.Cryptography;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1610.c</c>: <c>Curl_sha256it</c>, here the BCL's
/// <see cref="SHA256"/>, the SHA-256 Curl's AWS SigV4 and Digest authentication compute with.
/// </summary>
public sealed class Unit1610Sha256 : DigestUnitProgram
{
    /// <inheritdoc />
    public override string ProgramName => "unit1610";

    /// <inheritdoc />
    protected override IReadOnlyList<(byte[] Input, byte[] Expected)> Rows { get; } =
    [
        (Bytes("1"), Convert.FromHexString("6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b")),
        (Bytes("hello-you-fool"), Convert.FromHexString("cbb16a8ab9cbb935a8cba02e28c02630d1199c1f0217f47c9620f3efe82715ae")),
    ];

    /// <inheritdoc />
    protected override byte[] Digest(byte[] input) => SHA256.HashData(input);
}
