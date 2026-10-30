using System.Buffers.Binary;
using System.Numerics;

namespace Curl.Extension.Checksum;

/// <summary>SHA-512 and SHA-512/t (FIPS 180-4 §6.4, §6.7), built by hand: the base class library has no SHA-512/t.</summary>
internal sealed class Sha512Hash : BlockChecksumHash
{
    private readonly ulong[] _initialHash;
    private readonly int _digestLength;
    private readonly ulong[] _state = new ulong[8];
    private readonly ulong[] _schedule = new ulong[80];

    /// <summary>Starts a hash.</summary>
    /// <param name="initialHash">The eight words of the initial hash value (§5.3.5, §5.3.6).</param>
    /// <param name="digestLength">The digest length in bytes: 28 for SHA-512/224, 32 for SHA-512/256, 64 for SHA-512.</param>
    public Sha512Hash(ulong[] initialHash, int digestLength)
        : base(128)
    {
        _initialHash = initialHash;
        _digestLength = digestLength;
        ResetState();
    }

    protected override void ProcessBlock(ReadOnlySpan<byte> block)
    {
        var k = Sha2Constants.RoundConstants512;
        for (var t = 0; t < 16; t++)
        {
            _schedule[t] = BinaryPrimitives.ReadUInt64BigEndian(block[(t * 8)..]);
        }

        for (var t = 16; t < 80; t++)
        {
            var w15 = _schedule[t - 15];
            var w2 = _schedule[t - 2];
            var sigma0 = BitOperations.RotateRight(w15, 1) ^ BitOperations.RotateRight(w15, 8) ^ (w15 >> 7);
            var sigma1 = BitOperations.RotateRight(w2, 19) ^ BitOperations.RotateRight(w2, 61) ^ (w2 >> 6);
            _schedule[t] = sigma1 + _schedule[t - 7] + sigma0 + _schedule[t - 16];
        }

        ulong a = _state[0], b = _state[1], c = _state[2], d = _state[3], e = _state[4], f = _state[5], g = _state[6], h = _state[7];
        for (var t = 0; t < 80; t++)
        {
            var sum1 = BitOperations.RotateRight(e, 14) ^ BitOperations.RotateRight(e, 18) ^ BitOperations.RotateRight(e, 41);
            var sum0 = BitOperations.RotateRight(a, 28) ^ BitOperations.RotateRight(a, 34) ^ BitOperations.RotateRight(a, 39);
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

        ulong[] working = [a, b, c, d, e, f, g, h];
        for (var i = 0; i < 8; i++)
        {
            _state[i] += working[i];
        }
    }

    protected override byte[] Digest()
    {
        var digest = new byte[64];
        for (var i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt64BigEndian(digest.AsSpan(i * 8), _state[i]);
        }

        return digest[.._digestLength];
    }

    protected override void ResetState() => _initialHash.CopyTo(_state, 0);
}
