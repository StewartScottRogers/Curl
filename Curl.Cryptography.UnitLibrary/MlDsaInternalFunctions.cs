using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// ML-DSA's internal functions (FIPS 204 section 6): key derivation from rho, K, s1 and s2
/// (the heart of ML-DSA.KeyGen_internal), ML-DSA.Sign_internal and ML-DSA.Verify_internal
/// from the message representative mu, and the hashing of mu itself. Polynomial vectors
/// are flat <see cref="int" /> arrays of 256 coefficients per polynomial, reduced mod q;
/// the matrix A is stored whole, row by row, in the NTT domain.
/// </summary>
/// <remarks>
/// Key derivation and signing are constant-time in the private key apart from what
/// <see cref="MlDsaSampling" /> documents and the rejection loop of signing, whose
/// iteration count FIPS 204 allows to leak: each attempt runs every step and every norm
/// check to completion, whatever it finds, and branches once, on the combined outcome.
/// Every secret temporary is zeroed before returning. Verification works on public data.
/// </remarks>
internal static class MlDsaInternalFunctions
{
    private const int Degree = MlDsaPolynomial.Degree;
    private const int SeedSize = MlDsaParameters.SeedSize;
    private const int HashSize = MlDsaParameters.HashSize;
    private const int T0Upper = 1 << (MlDsaParameters.DroppedBits - 1);

    /// <summary>
    /// Expands the 32-byte seed xi into rho, rho' and K and derives the key pair (FIPS 204
    /// algorithm 6).
    /// </summary>
    public static void GenerateKeys(MlDsaParameters parameters, ReadOnlySpan<byte> seed, Span<byte> publicKey, Span<byte> privateKey)
    {
        Span<byte> expanded = stackalloc byte[SeedSize + HashSize + SeedSize];
        int[] s1 = new int[parameters.Columns * Degree];
        int[] s2 = new int[parameters.Rows * Degree];
        try
        {
            using (Shake h = Shake.Create256())
            {
                h.AppendData(seed);
                h.AppendData([(byte)parameters.Rows, (byte)parameters.Columns]);
                h.Read(expanded);
            }

            ReadOnlySpan<byte> rhoPrime = expanded.Slice(SeedSize, HashSize);
            for (int column = 0; column < parameters.Columns; column++)
            {
                MlDsaSampling.SampleBounded(rhoPrime, column, parameters.Eta, s1.AsSpan(column * Degree, Degree));
            }

            for (int row = 0; row < parameters.Rows; row++)
            {
                MlDsaSampling.SampleBounded(rhoPrime, parameters.Columns + row, parameters.Eta, s2.AsSpan(row * Degree, Degree));
            }

            DeriveKeys(parameters, expanded[..SeedSize], expanded[(SeedSize + HashSize)..], s1, s2, publicKey, privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expanded);
            Array.Clear(s1);
            Array.Clear(s2);
        }
    }

    /// <summary>
    /// Derives the key pair from rho, K, s1 and s2: t = A s1 + s2 split by Power2Round into
    /// t1 and t0, pk = rho || t1, tr = H(pk), and sk = rho || K || tr || s1 || s2 || t0
    /// (FIPS 204 algorithm 6 from step 3, with algorithms 22 and 24).
    /// </summary>
    public static void DeriveKeys(
        MlDsaParameters parameters,
        ReadOnlySpan<byte> rho,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<int> s1,
        ReadOnlySpan<int> s2,
        Span<byte> publicKey,
        Span<byte> privateKey)
    {
        int[] matrix = ExpandMatrix(parameters, rho);
        int[] s1Ntt = s1.ToArray();
        int[] t = new int[parameters.Rows * Degree];
        try
        {
            MlDsaPolynomial.NttEach(s1Ntt);
            MultiplyMatrix(parameters, matrix, s1Ntt, t);
            MlDsaPolynomial.InverseNttEach(t);
            MlDsaPolynomial.AddTo(t, s2);
            rho.CopyTo(publicKey);
            rho.CopyTo(privateKey);
            key.CopyTo(privateKey[SeedSize..]);
            int t0PolynomialSize = 32 * MlDsaParameters.DroppedBits;
            Span<byte> t0Encoded = privateKey[^(parameters.Rows * t0PolynomialSize)..];
            SplitAndEncodeT(parameters, t, publicKey[SeedSize..], t0Encoded);
            Shake.HashData256(publicKey, privateKey.Slice(2 * SeedSize, HashSize));
            int secretSize = 32 * parameters.SecretBits;
            Span<byte> secrets = privateKey[((2 * SeedSize) + HashSize)..];
            EncodeCentered(s1, parameters.Eta, parameters.SecretBits, secrets[..(parameters.Columns * secretSize)]);
            EncodeCentered(s2, parameters.Eta, parameters.SecretBits, secrets.Slice(parameters.Columns * secretSize, parameters.Rows * secretSize));
        }
        finally
        {
            Array.Clear(s1Ntt);
            Array.Clear(t);
        }
    }

    /// <summary>
    /// Reads s1 and s2 out of <paramref name="privateKey" /> (FIPS 204 algorithm 25, in
    /// part) into <paramref name="s1" /> and <paramref name="s2" />.
    /// </summary>
    public static void DecodeSecrets(MlDsaParameters parameters, ReadOnlySpan<byte> privateKey, Span<int> s1, Span<int> s2)
    {
        int secretSize = 32 * parameters.SecretBits;
        ReadOnlySpan<byte> secrets = privateKey[((2 * SeedSize) + HashSize)..];
        DecodeCentered(secrets[..(parameters.Columns * secretSize)], parameters.Eta, parameters.SecretBits, s1);
        DecodeCentered(secrets.Slice(parameters.Columns * secretSize, parameters.Rows * secretSize), parameters.Eta, parameters.SecretBits, s2);
    }

    /// <summary>
    /// Writes mu = H(tr || 0 || |ctx| || ctx || M, 64), the message representative of pure
    /// ML-DSA (FIPS 204 algorithm 2 step 10 and algorithm 7 step 6), to
    /// <paramref name="mu" />.
    /// </summary>
    public static void ComputeMessageRepresentative(ReadOnlySpan<byte> tr, ReadOnlySpan<byte> context, ReadOnlySpan<byte> message, Span<byte> mu)
    {
        using Shake h = Shake.Create256();
        h.AppendData(tr);
        h.AppendData([0, (byte)context.Length]);
        h.AppendData(context);
        h.AppendData(message);
        h.Read(mu);
    }

    /// <summary>
    /// Signs the message representative <paramref name="mu" /> with
    /// <paramref name="privateKey" /> and the 32 bytes <paramref name="randomness" />
    /// (all zero for the deterministic variant) into <paramref name="signature" /> (FIPS
    /// 204 algorithm 7).
    /// </summary>
    public static void Sign(
        MlDsaParameters parameters,
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> mu,
        ReadOnlySpan<byte> randomness,
        Span<byte> signature)
    {
        using MlDsaSigningWorkspace workspace = new(parameters, privateKey);
        Span<byte> maskSeed = stackalloc byte[HashSize];
        try
        {
            using (Shake h = Shake.Create256())
            {
                h.AppendData(privateKey.Slice(SeedSize, SeedSize));
                h.AppendData(randomness);
                h.AppendData(mu);
                h.Read(maskSeed);
            }

            int counter = 0;
            while (!workspace.TryAttempt(mu, maskSeed, counter, signature))
            {
                counter += parameters.Columns;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(maskSeed);
        }
    }

    /// <summary>
    /// Verifies <paramref name="signature" /> over the message representative
    /// <paramref name="mu" /> against <paramref name="publicKey" /> (FIPS 204 algorithm 8).
    /// </summary>
    /// <returns>
    /// <c>false</c> when the hint is malformed, z is out of range, or the recomputed
    /// commitment hash differs; otherwise <c>true</c>.
    /// </returns>
    public static bool Verify(MlDsaParameters parameters, ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> mu, ReadOnlySpan<byte> signature)
    {
        int rowsLength = parameters.Rows * Degree;
        ReadOnlySpan<byte> commitmentHash = signature[..parameters.CommitmentHashSize];
        int[] z = new int[parameters.Columns * Degree];
        int[] hint = new int[rowsLength];
        if (!TryDecodeSignature(parameters, signature, z, hint))
        {
            return false;
        }

        int[] challenge = new int[Degree];
        MlDsaSampling.SampleInBall(commitmentHash, parameters.Tau, challenge);
        MlDsaPolynomial.Ntt(challenge);
        int[] t1 = new int[rowsLength];
        DecodeScaledT1(parameters, publicKey, t1);
        MlDsaPolynomial.NttEach(z);
        int[] approximation = new int[rowsLength];
        MultiplyMatrix(parameters, ExpandMatrix(parameters, publicKey[..SeedSize]), z, approximation);
        int[] product = new int[rowsLength];
        MultiplyByChallenge(challenge, t1, product);
        MlDsaPolynomial.SubtractFrom(approximation, product);
        MlDsaPolynomial.InverseNttEach(approximation);
        for (int index = 0; index < approximation.Length; index++)
        {
            approximation[index] = MlDsaPolynomial.UseHint(hint[index], approximation[index], parameters.Gamma2);
        }

        Span<byte> expected = stackalloc byte[parameters.CommitmentHashSize];
        HashCommitment(parameters, mu, approximation, expected);
        return CryptographicOperations.FixedTimeEquals(expected, commitmentHash);
    }

    /// <summary>
    /// Writes c-tilde = H(mu || w1Encode(w1), lambda / 4) (FIPS 204 algorithm 7 step 15,
    /// algorithm 8 step 12, with algorithm 28).
    /// </summary>
    public static void HashCommitment(MlDsaParameters parameters, ReadOnlySpan<byte> mu, ReadOnlySpan<int> commitment, Span<byte> commitmentHash)
    {
        int polynomialSize = 32 * parameters.CommitmentBits;
        Span<byte> encoded = stackalloc byte[parameters.EncodedCommitmentSize];
        for (int row = 0; row < parameters.Rows; row++)
        {
            MlDsaEncoding.Pack(commitment.Slice(row * Degree, Degree), parameters.CommitmentBits, encoded.Slice(row * polynomialSize, polynomialSize));
        }

        using Shake h = Shake.Create256();
        h.AppendData(mu);
        h.AppendData(encoded);
        h.Read(commitmentHash);
        CryptographicOperations.ZeroMemory(encoded);
    }

    /// <summary>Samples the whole matrix A from rho (FIPS 204 algorithm 32), row by row.</summary>
    public static int[] ExpandMatrix(MlDsaParameters parameters, ReadOnlySpan<byte> rho)
    {
        int[] matrix = new int[parameters.Rows * parameters.Columns * Degree];
        for (int row = 0; row < parameters.Rows; row++)
        {
            for (int column = 0; column < parameters.Columns; column++)
            {
                int offset = ((row * parameters.Columns) + column) * Degree;
                MlDsaSampling.SampleNtt(rho, row, column, matrix.AsSpan(offset, Degree));
            }
        }

        return matrix;
    }

    /// <summary>
    /// Writes A times <paramref name="vectorNtt" />, an NTT-domain vector of l polynomials,
    /// to <paramref name="result" />, k polynomials left in the NTT domain.
    /// </summary>
    public static void MultiplyMatrix(MlDsaParameters parameters, ReadOnlySpan<int> matrix, ReadOnlySpan<int> vectorNtt, Span<int> result)
    {
        result.Clear();
        for (int row = 0; row < parameters.Rows; row++)
        {
            Span<int> accumulator = result.Slice(row * Degree, Degree);
            for (int column = 0; column < parameters.Columns; column++)
            {
                int offset = ((row * parameters.Columns) + column) * Degree;
                MlDsaPolynomial.MultiplyNttsAndAdd(accumulator, matrix.Slice(offset, Degree), vectorNtt.Slice(column * Degree, Degree));
            }
        }
    }

    /// <summary>
    /// Writes the NTT-domain product of the challenge <paramref name="challengeNtt" /> with
    /// each polynomial of <paramref name="vectorNtt" /> to <paramref name="result" />.
    /// </summary>
    public static void MultiplyByChallenge(ReadOnlySpan<int> challengeNtt, ReadOnlySpan<int> vectorNtt, Span<int> result)
    {
        result.Clear();
        for (int offset = 0; offset < result.Length; offset += Degree)
        {
            MlDsaPolynomial.MultiplyNttsAndAdd(result.Slice(offset, Degree), challengeNtt, vectorNtt.Slice(offset, Degree));
        }
    }

    /// <summary>Writes each polynomial of <paramref name="vector" />, centred, as <paramref name="upper" /> - w in <paramref name="bits" /> bits.</summary>
    public static void EncodeCentered(ReadOnlySpan<int> vector, int upper, int bits, Span<byte> destination)
    {
        int polynomialSize = 32 * bits;
        for (int index = 0; index * Degree < vector.Length; index++)
        {
            MlDsaEncoding.PackCentered(vector.Slice(index * Degree, Degree), upper, bits, destination.Slice(index * polynomialSize, polynomialSize));
        }
    }

    private static void DecodeCentered(ReadOnlySpan<byte> source, int upper, int bits, Span<int> vector)
    {
        int polynomialSize = 32 * bits;
        for (int index = 0; index * Degree < vector.Length; index++)
        {
            MlDsaEncoding.UnpackCentered(source.Slice(index * polynomialSize, polynomialSize), upper, bits, vector.Slice(index * Degree, Degree));
        }
    }

    // Power2Round of every coefficient of t: t1 packed into the public key at 10 bits,
    // t0 packed into the private key as 2^12 - t0 at 13 bits.
    private static void SplitAndEncodeT(MlDsaParameters parameters, Span<int> t, Span<byte> t1Encoded, Span<byte> t0Encoded)
    {
        int[] t1 = new int[t.Length];
        for (int index = 0; index < t.Length; index++)
        {
            t1[index] = MlDsaPolynomial.Power2Round(t[index], out int low);
            t[index] = low;
        }

        int t1Size = 32 * MlDsaParameters.HighBitsOfTBits;
        for (int row = 0; row < parameters.Rows; row++)
        {
            MlDsaEncoding.Pack(t1.AsSpan(row * Degree, Degree), MlDsaParameters.HighBitsOfTBits, t1Encoded.Slice(row * t1Size, t1Size));
        }

        EncodeCentered(t, T0Upper, MlDsaParameters.DroppedBits, t0Encoded);
    }

    // sigDecode (FIPS 204 algorithm 27) and the norm check of z (algorithm 8 step 13):
    // false for a malformed hint or a z coefficient of gamma1 - beta or more.
    private static bool TryDecodeSignature(MlDsaParameters parameters, ReadOnlySpan<byte> signature, Span<int> z, Span<int> hint)
    {
        int zSize = parameters.Columns * 32 * parameters.MaskBits;
        DecodeCentered(signature.Slice(parameters.CommitmentHashSize, zSize), parameters.Gamma1, parameters.MaskBits, z);
        return MlDsaEncoding.TryUnpackHint(signature[(parameters.CommitmentHashSize + zSize)..], parameters.Omega, hint)
            && !MlDsaPolynomial.ExceedsBound(z, parameters.Gamma1 - parameters.Beta);
    }

    // t1 from the public key (FIPS 204 algorithm 23), times 2^d, in the NTT domain: t1 is
    // below 2^10, so t1 2^d is at most q - 1.
    private static void DecodeScaledT1(MlDsaParameters parameters, ReadOnlySpan<byte> publicKey, Span<int> t1)
    {
        int t1Size = 32 * MlDsaParameters.HighBitsOfTBits;
        for (int row = 0; row < parameters.Rows; row++)
        {
            MlDsaEncoding.Unpack(publicKey.Slice(SeedSize + (row * t1Size), t1Size), MlDsaParameters.HighBitsOfTBits, t1.Slice(row * Degree, Degree));
        }

        for (int index = 0; index < t1.Length; index++)
        {
            t1[index] <<= MlDsaParameters.DroppedBits;
        }

        MlDsaPolynomial.NttEach(t1);
    }

    /// <summary>
    /// The private key decoded for signing (s1, s2 and t0 in the NTT domain, and A), with
    /// the buffers one signing attempt needs; <see cref="Dispose" /> zeroes all of them.
    /// </summary>
    private sealed class MlDsaSigningWorkspace : IDisposable
    {
        private readonly MlDsaParameters parameters;
        private readonly int[] matrix;
        private readonly int[] s1Ntt;
        private readonly int[] s2Ntt;
        private readonly int[] t0Ntt;
        private readonly int[] mask;
        private readonly int[] maskNtt;
        private readonly int[] commitment;
        private readonly int[] highBits;
        private readonly int[] challenge;
        private readonly int[] z;
        private readonly int[] r;
        private readonly int[] ct0;
        private readonly int[] hint;
        private readonly byte[] commitmentHash;

        public MlDsaSigningWorkspace(MlDsaParameters parameters, ReadOnlySpan<byte> privateKey)
        {
            this.parameters = parameters;
            int rowsLength = parameters.Rows * Degree;
            int columnsLength = parameters.Columns * Degree;
            matrix = ExpandMatrix(parameters, privateKey[..SeedSize]);
            s1Ntt = new int[columnsLength];
            s2Ntt = new int[rowsLength];
            t0Ntt = new int[rowsLength];
            mask = new int[columnsLength];
            maskNtt = new int[columnsLength];
            commitment = new int[rowsLength];
            highBits = new int[rowsLength];
            challenge = new int[Degree];
            z = new int[columnsLength];
            r = new int[rowsLength];
            ct0 = new int[rowsLength];
            hint = new int[rowsLength];
            commitmentHash = new byte[parameters.CommitmentHashSize];
            DecodeSecrets(parameters, privateKey, s1Ntt, s2Ntt);
            DecodeCentered(privateKey[^(parameters.Rows * 32 * MlDsaParameters.DroppedBits)..], T0Upper, MlDsaParameters.DroppedBits, t0Ntt);
            MlDsaPolynomial.NttEach(s1Ntt);
            MlDsaPolynomial.NttEach(s2Ntt);
            MlDsaPolynomial.NttEach(t0Ntt);
        }

        // One pass of FIPS 204 algorithm 7's loop, steps 11 to 31 with kappa = counter:
        // every step and check runs whatever the earlier checks found, and the signature
        // is written only when all of them pass.
        public bool TryAttempt(ReadOnlySpan<byte> mu, ReadOnlySpan<byte> maskSeed, int counter, Span<byte> signature)
        {
            for (int column = 0; column < parameters.Columns; column++)
            {
                MlDsaSampling.SampleMask(maskSeed, counter + column, parameters.Gamma1Bits, mask.AsSpan(column * Degree, Degree));
            }

            mask.CopyTo(maskNtt, 0);
            MlDsaPolynomial.NttEach(maskNtt);
            MultiplyMatrix(parameters, matrix, maskNtt, commitment);
            MlDsaPolynomial.InverseNttEach(commitment);
            for (int index = 0; index < commitment.Length; index++)
            {
                highBits[index] = MlDsaPolynomial.HighBits(commitment[index], parameters.Gamma2);
            }

            HashCommitment(parameters, mu, highBits, commitmentHash);
            MlDsaSampling.SampleInBall(commitmentHash, parameters.Tau, challenge);
            MlDsaPolynomial.Ntt(challenge);
            Transformed(s1Ntt, z);
            MlDsaPolynomial.AddTo(z, mask);
            Transformed(s2Ntt, r);
            MlDsaPolynomial.SubtractFrom(r, commitment);
            Negate(r);
            Transformed(t0Ntt, ct0);
            bool rejected = MlDsaPolynomial.ExceedsBound(z, parameters.Gamma1 - parameters.Beta)
                | MlDsaPolynomial.LowBitsExceedBound(r, parameters.Gamma2, parameters.Gamma2 - parameters.Beta)
                | MlDsaPolynomial.ExceedsBound(ct0, parameters.Gamma2)
                | (ComputeHint() > parameters.Omega);
            if (rejected)
            {
                return false;
            }

            WriteSignature(signature);
            return true;
        }

        public void Dispose()
        {
            Array.Clear(s1Ntt);
            Array.Clear(s2Ntt);
            Array.Clear(t0Ntt);
            Array.Clear(mask);
            Array.Clear(maskNtt);
            Array.Clear(commitment);
            Array.Clear(highBits);
            Array.Clear(challenge);
            Array.Clear(z);
            Array.Clear(r);
            Array.Clear(ct0);
            Array.Clear(hint);
            CryptographicOperations.ZeroMemory(commitmentHash);
        }

        // Writes the inverse NTT of c times vectorNtt to result.
        private void Transformed(ReadOnlySpan<int> vectorNtt, int[] result)
        {
            MultiplyByChallenge(challenge, vectorNtt, result);
            MlDsaPolynomial.InverseNttEach(result);
        }

        // h = MakeHint(-c t0, w - c s2 + c t0): 1 where adding c t0 to r = w - c s2 moves
        // its high bits. Returns the number of ones.
        private int ComputeHint()
        {
            int ones = 0;
            for (int index = 0; index < hint.Length; index++)
            {
                hint[index] = MlDsaPolynomial.MakeHint(ct0[index], r[index], parameters.Gamma2);
                ones += hint[index];
            }

            return ones;
        }

        private void WriteSignature(Span<byte> signature)
        {
            commitmentHash.CopyTo(signature);
            int zSize = parameters.Columns * 32 * parameters.MaskBits;
            EncodeCentered(z, parameters.Gamma1, parameters.MaskBits, signature.Slice(parameters.CommitmentHashSize, zSize));
            MlDsaEncoding.PackHint(hint, parameters.Omega, signature[(parameters.CommitmentHashSize + zSize)..]);
        }

        // r held c s2 - w; negating gives w - c s2.
        private static void Negate(Span<int> vector)
        {
            for (int index = 0; index < vector.Length; index++)
            {
                vector[index] = MlDsaPolynomial.Subtract(0, vector[index]);
            }
        }
    }
}
