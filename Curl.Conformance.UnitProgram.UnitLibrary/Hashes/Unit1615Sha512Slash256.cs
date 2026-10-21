using Curl.Authentication;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1615.c</c>: <c>Curl_sha512_256it</c>, here Curl's
/// hand-built <see cref="Sha512Slash256"/>, over the C program's eight inputs.
/// </summary>
public sealed class Unit1615Sha512Slash256 : DigestUnitProgram
{
    /// <inheritdoc />
    public override string ProgramName => "unit1615";

    /// <inheritdoc />
    protected override IReadOnlyList<(byte[] Input, byte[] Expected)> Rows { get; } =
    [
        (Bytes("1"), Convert.FromHexString("18d27566bd1ac66b2332d8c54ad43f7bb22079c906d05f491f3f07a28d5c6990")),
        (Bytes("hello-you-fool"), Convert.FromHexString("af6fb4b0139bee13d1953cb8c7cd5b19f9cdcd21efdfa7425c0713eacc1a3976")),
        (Bytes("abc"), Convert.FromHexString("53048E2681941EF99B2E29B76B4C7DABE4C2D0C634FC6D46E0E2F13107E7AF23")),
        (Bytes(string.Empty), Convert.FromHexString("c672b8d1ef56ed28ab87c3622c5114069bdd3ad7b8f9737498d0c01ecef0967a")),
        (Bytes(string.Concat(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyzzyxwvutsrqponMLKJIHGFEDCBA", 2))), Convert.FromHexString("ade95d553b9e4569db53a40492e78794ffc9985f93038645e19717727cbc3115")),
        (Bytes(string.Concat(Enumerable.Repeat("/long", 70)) + "/path?with%20some=parameters"), Convert.FromHexString("bcabc62c0a22d5cbacace925cfceaaaf0ea1ed42468ae201ee2fdb39754773f1")),
        (Bytes("Simple string."), Convert.FromHexString("decb3c81654ba0f5f0456b7e61f50df538a4fcb18a95ff59bc0482cf23b23256")),
        ([.. Enumerable.Range(1, 255).Reverse().Select(value => (byte)value)], Convert.FromHexString("2231f2a1b489b244f766a0b831edb7738a34dc11c82cf2b58860396b5c067037")),
    ];

    /// <inheritdoc />
    protected override byte[] Digest(byte[] input) => Sha512Slash256.HashData(input);
}
