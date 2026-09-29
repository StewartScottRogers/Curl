using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// K-PKE, the public-key encryption scheme under ML-KEM (FIPS 203 section 5): key
/// generation, encryption and decryption of a 32-byte message. The matrix A-hat is never
/// stored: each entry is sampled from rho where it is used.
/// </summary>
/// <remarks>
/// Constant-time in the secrets (d, the message, the randomness r and the decryption key):
/// only the public rho drives rejection sampling. Every secret temporary is zeroed before
/// returning.
/// </remarks>
internal static class MlKemPublicKeyEncryption
{
    private const int Degree = MlKemPolynomial.Degree;
    private const int SeedSize = MlKemParameters.SeedSize;
    private const int EncodedPolynomialSize = MlKemParameters.EncodedPolynomialSize;

    /// <summary>
    /// K-PKE.KeyGen (FIPS 203 algorithm 13): derives from the 32-byte seed
    /// <paramref name="seed" /> the encryption key (t-hat and rho) and the decryption key
    /// (s-hat).
    /// </summary>
    public static void GenerateKeys(
        MlKemParameters parameters,
        ReadOnlySpan<byte> seed,
        Span<byte> encryptionKey,
        Span<byte> decryptionKey)
    {
        int rank = parameters.Rank;
        Span<byte> seedAndRank = stackalloc byte[SeedSize + 1];
        Span<byte> rhoAndSigma = stackalloc byte[2 * SeedSize];
        Span<int> secret = stackalloc int[rank * Degree];
        Span<int> error = stackalloc int[rank * Degree];
        Span<int> sum = stackalloc int[Degree];
        Span<int> entry = stackalloc int[Degree];
        try
        {
            seed.CopyTo(seedAndRank);
            seedAndRank[SeedSize] = (byte)rank;
            Sha3.HashData512(seedAndRank, rhoAndSigma);
            ReadOnlySpan<byte> rho = rhoAndSigma[..SeedSize];
            ReadOnlySpan<byte> sigma = rhoAndSigma[SeedSize..];
            SampleVector(sigma, 0, parameters.Eta1, secret);
            SampleVector(sigma, (byte)rank, parameters.Eta1, error);
            for (int index = 0; index < rank; index++)
            {
                MlKemPolynomial.Ntt(secret.Slice(index * Degree, Degree));
                MlKemPolynomial.Ntt(error.Slice(index * Degree, Degree));
            }

            for (int row = 0; row < rank; row++)
            {
                error.Slice(row * Degree, Degree).CopyTo(sum);
                for (int column = 0; column < rank; column++)
                {
                    SampleMatrixEntry(rho, row, column, entry);
                    MlKemPolynomial.MultiplyNttsAndAdd(sum, entry, secret.Slice(column * Degree, Degree));
                }

                MlKemPolynomial.Encode(sum, 12, encryptionKey.Slice(row * EncodedPolynomialSize, EncodedPolynomialSize));
                MlKemPolynomial.Encode(secret.Slice(row * Degree, Degree), 12, decryptionKey.Slice(row * EncodedPolynomialSize, EncodedPolynomialSize));
            }

            rho.CopyTo(encryptionKey[parameters.EncodedVectorSize..]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seedAndRank);
            CryptographicOperations.ZeroMemory(rhoAndSigma);
            secret.Clear();
            error.Clear();
            sum.Clear();
        }
    }

    /// <summary>
    /// K-PKE.Encrypt (FIPS 203 algorithm 14): encrypts the 32-byte
    /// <paramref name="message" /> to <paramref name="encryptionKey" /> with the 32 bytes of
    /// <paramref name="randomness" />, writing the ciphertext.
    /// </summary>
    public static void Encrypt(
        MlKemParameters parameters,
        ReadOnlySpan<byte> encryptionKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> randomness,
        Span<byte> ciphertext)
    {
        int rank = parameters.Rank;
        ReadOnlySpan<byte> rho = encryptionKey[parameters.EncodedVectorSize..];
        Span<int> vectorY = stackalloc int[rank * Degree];
        Span<int> errors = stackalloc int[(rank + 1) * Degree];
        Span<int> sum = stackalloc int[Degree];
        Span<int> entry = stackalloc int[Degree];
        try
        {
            SampleVector(randomness, 0, parameters.Eta1, vectorY);
            SampleVector(randomness, (byte)rank, MlKemParameters.Eta2, errors);
            for (int index = 0; index < rank; index++)
            {
                MlKemPolynomial.Ntt(vectorY.Slice(index * Degree, Degree));
            }

            for (int row = 0; row < rank; row++)
            {
                sum.Clear();
                for (int column = 0; column < rank; column++)
                {
                    SampleMatrixEntry(rho, column, row, entry);
                    MlKemPolynomial.MultiplyNttsAndAdd(sum, entry, vectorY.Slice(column * Degree, Degree));
                }

                FinishCompressedPart(sum, errors.Slice(row * Degree, Degree), parameters.Du, ciphertext.Slice(32 * parameters.Du * row, 32 * parameters.Du));
            }

            sum.Clear();
            for (int column = 0; column < rank; column++)
            {
                MlKemPolynomial.Decode(encryptionKey.Slice(column * EncodedPolynomialSize, EncodedPolynomialSize), 12, entry);
                MlKemPolynomial.MultiplyNttsAndAdd(sum, entry, vectorY.Slice(column * Degree, Degree));
            }

            Span<int> errorAndMessage = errors.Slice(rank * Degree, Degree);
            MlKemPolynomial.Decode(message, 1, entry);
            MlKemPolynomial.Decompress(entry, 1);
            MlKemPolynomial.AddTo(errorAndMessage, entry);
            FinishCompressedPart(sum, errorAndMessage, parameters.Dv, ciphertext[parameters.CompressedVectorSize..]);
        }
        finally
        {
            vectorY.Clear();
            errors.Clear();
            sum.Clear();
            entry.Clear();
        }
    }

    /// <summary>
    /// K-PKE.Decrypt (FIPS 203 algorithm 15): recovers the 32-byte message of
    /// <paramref name="ciphertext" /> under <paramref name="decryptionKey" />.
    /// </summary>
    public static void Decrypt(
        MlKemParameters parameters,
        ReadOnlySpan<byte> decryptionKey,
        ReadOnlySpan<byte> ciphertext,
        Span<byte> message)
    {
        int rank = parameters.Rank;
        int partSize = 32 * parameters.Du;
        Span<int> product = stackalloc int[Degree];
        Span<int> secret = stackalloc int[Degree];
        Span<int> part = stackalloc int[Degree];
        try
        {
            product.Clear();
            for (int index = 0; index < rank; index++)
            {
                MlKemPolynomial.Decode(ciphertext.Slice(partSize * index, partSize), parameters.Du, part);
                MlKemPolynomial.Decompress(part, parameters.Du);
                MlKemPolynomial.Ntt(part);
                MlKemPolynomial.Decode(decryptionKey.Slice(index * EncodedPolynomialSize, EncodedPolynomialSize), 12, secret);
                MlKemPolynomial.MultiplyNttsAndAdd(product, secret, part);
            }

            MlKemPolynomial.InverseNtt(product);
            MlKemPolynomial.Decode(ciphertext[parameters.CompressedVectorSize..], parameters.Dv, part);
            MlKemPolynomial.Decompress(part, parameters.Dv);
            for (int index = 0; index < Degree; index++)
            {
                part[index] = MlKemPolynomial.Subtract(part[index], product[index]);
            }

            MlKemPolynomial.Compress(part, 1);
            MlKemPolynomial.Encode(part, 1, message);
        }
        finally
        {
            product.Clear();
            secret.Clear();
            part.Clear();
        }
    }

    // Fills consecutive polynomials of vector from PRF_eta(seed, N), N counting up from
    // firstCounter (FIPS 203 equation 4.3: SHAKE256(seed || N) of 64 eta bytes).
    private static void SampleVector(ReadOnlySpan<byte> seed, byte firstCounter, int eta, Span<int> vector)
    {
        Span<byte> input = stackalloc byte[SeedSize + 1];
        Span<byte> output = stackalloc byte[64 * eta];
        try
        {
            seed.CopyTo(input);
            for (int index = 0; index < vector.Length / Degree; index++)
            {
                input[SeedSize] = (byte)(firstCounter + index);
                Shake.HashData256(input, output);
                MlKemPolynomial.SampleCenteredBinomial(output, eta, vector.Slice(index * Degree, Degree));
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(output);
        }
    }

    // A-hat[row, column] = SampleNTT(rho || column || row) (FIPS 203 algorithm 13, line 6).
    private static void SampleMatrixEntry(ReadOnlySpan<byte> rho, int row, int column, Span<int> entry)
    {
        Span<byte> seed = stackalloc byte[SeedSize + 2];
        rho.CopyTo(seed);
        seed[SeedSize] = (byte)column;
        seed[SeedSize + 1] = (byte)row;
        MlKemPolynomial.SampleNtt(seed, entry);
    }

    // Takes the NTT-domain sum back, adds the error (and message), compresses and encodes.
    private static void FinishCompressedPart(Span<int> sum, ReadOnlySpan<int> error, int bits, Span<byte> destination)
    {
        MlKemPolynomial.InverseNtt(sum);
        MlKemPolynomial.AddTo(sum, error);
        MlKemPolynomial.Compress(sum, bits);
        MlKemPolynomial.Encode(sum, bits, destination);
    }
}
