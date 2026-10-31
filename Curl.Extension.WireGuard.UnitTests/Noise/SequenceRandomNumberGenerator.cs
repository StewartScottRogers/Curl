using System.Security.Cryptography;

namespace Curl.Extension.WireGuard.UnitTests.Noise;

/// <summary>A repeatable random source: each request is filled with the next bytes of a counter seeded by the test.</summary>
internal sealed class SequenceRandomNumberGenerator(byte seed) : RandomNumberGenerator
{
    private byte next = seed;

    public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());

    public override void GetBytes(Span<byte> data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = next++;
        }
    }
}
