using System.Security.Cryptography;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1612.c</c>: <c>Curl_hmacit(&amp;Curl_HMAC_MD5, ...)</c> keyed
/// with <c>Pa55worD</c>, here the BCL's <see cref="HMACMD5"/>, the HMAC-MD5 Curl's CRAM-MD5 computes with.
/// </summary>
public sealed class Unit1612HmacMd5 : DigestUnitProgram
{
    private static readonly byte[] Password = Bytes("Pa55worD");

    /// <inheritdoc />
    public override string ProgramName => "unit1612";

    /// <inheritdoc />
    protected override IReadOnlyList<(byte[] Input, byte[] Expected)> Rows { get; } =
    [
        (Bytes("1"), Convert.FromHexString("d129754358dcab78dfcd7f2b29311337")),
        (Bytes("hello-you-fool"), Convert.FromHexString("75f1a7b9f540e5a498839f645a276dd0")),
    ];

    /// <inheritdoc />
    protected override byte[] Digest(byte[] input) => HMACMD5.HashData(Password, input);
}
