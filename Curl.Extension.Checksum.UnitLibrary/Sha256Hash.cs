using System.Buffers.Binary;
using System.Numerics;

namespace Curl.Extension.Checksum;

/// <summary>SHA-256 and SHA-224 (FIPS 180-4 §6.2, §6.3), built by hand: the base class library has no SHA-224.</summary>
internal sealed class Sha256Hash : BlockChecksumHash
{
    private readonly uint[] _initialHash;
    private readonly int _digestLength;
    private readonly uint[] _state = new uint[8];
    private readonly uint[] _schedule = new uint[64];

    /// <summary>Starts a hash.</summary>
    /// <param name="initialHash">The eight words of the initial hash value (§5.3.2, §5.3.3).</param>
    /// <param name="digestLength">The digest length in bytes: 28 for SHA-224, 32 for SHA-256.</param>
    public Sha256Hash(uint[] initialHash, int digestLength)
        : base(64)
    {
        _initialHash = initialHash;
        _digestLength = digestLength;
        ResetState();
    }

    protected override void ProcessBlock(ReadOnlySpan<byte> block)
    {
        var k = Sha2Constants.RoundConstants256;
        for (var t = 0; t < 16; t++)
        {
            _schedule[t] = BinaryPrimitives.ReadUInt32BigEndian(block[(t * 4)..]);
        }

        for (var t = 16; t < 64; t++)
        {
            var w15 = _schedule[t - 15];
            var w2 = _schedule[t - 2];
            var sigma0 = BitOperations.RotateRight(w15, 7) ^ BitOperations.RotateRight(w15, 18) ^ (w15 >> 3);
            var sigma1 = BitOperations.RotateRight(w2, 17) ^ BitOperations.RotateRight(w2, 19) ^ (w2 >> 10);
            _schedule[t] = sigma1 + _schedule[t - 7] + sigma0 + _schedule[t - 16];
        }

        uint a = _state[0], b = _state[1], c = _state[2], d = _state[3], e = _state[4], f = _state[5], g = _state[6], h = _state[7];
        for (var t = 0; t < 64; t++)
        {
            var sum1 = BitOperations.RotateRight(e, 6) ^ BitOperations.RotateRight(e, 11) ^ BitOperations.RotateRight(e, 25);
            var sum0 = BitOperations.RotateRight(a, 2) ^ BitOperations.RotateRight(a, 13) ^ BitOperations.RotateRight(a, 22);
            var t1 = h + sum1 + ((e & f) ^ (~e & g)) + k[t] + _schedule[t];
            var t2 = sum0 + ((a & b) ^ (a & c) ^ (b & c));
            h = g;
            g = f;
            f = e;
            e = d + t1;
            d = c;
            c = b;
            b = a;
            a = t1 + t2;
        }

        uint[] working = [a, b, c, d, e, f, g, h];
        for (var i = 0; i < 8; i++)
        {
            _state[i] += working[i];
        }
    }

    protected override byte[] Digest()
    {
        var digest = new byte[32];
        for (var i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(digest.AsSpan(i * 4), _state[i]);
        }

        return digest[.._digestLength];
    }

    protected override void ResetState() => _initialHash.CopyTo(_state, 0);
}
