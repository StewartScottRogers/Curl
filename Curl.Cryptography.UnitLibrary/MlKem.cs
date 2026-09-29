using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// ML-KEM, the module-lattice key-encapsulation mechanism of FIPS 203, in its three
/// parameter sets: key generation, encapsulation and decapsulation with implicit rejection.
/// An instance holds one decapsulation key; encapsulation is static, needing only the
/// peer's encapsulation key. The TLS 1.3 groups <c>X25519MLKEM768</c>,
/// <c>SecP256r1MLKEM768</c>, <c>SecP384r1MLKEM1024</c> and <c>MLKEM512/768/1024</c> use
/// it (ADR-0118: the BCL's <c>MLKem</c> exists only on Linux with OpenSSL 3.5+ and on
/// Windows Insider builds).
/// </summary>
/// <remarks>
/// Constant-time in every secret. <see cref="Decapsulate" /> always re-encrypts, compares
/// the re-encrypted ciphertext with the one received by accumulating their XOR, with no
/// branch, and picks the shared secret or the implicit-rejection key J(z || c) with a
/// masked select, so a tampered ciphertext yields a pseudorandom key rather than an error
/// and the time taken does not tell the two apart. Every secret temporary is zeroed before
/// returning; <see cref="Dispose" /> zeroes the decapsulation key.
/// </remarks>
public sealed class MlKem : IDisposable
{
    /// <summary>The length in bytes of a shared secret.</summary>
    public const int SharedSecretSize = MlKemParameters.SeedSize;

    /// <summary>The length in bytes of each of key generation's seeds d and z.</summary>
    public const int SeedSize = MlKemParameters.SeedSize;

    /// <summary>The length in bytes of encapsulation's random message m.</summary>
    public const int MessageSize = MlKemParameters.SeedSize;

    private readonly MlKemParameters parameters;
    private readonly byte[] decapsulationKey;
    private bool disposed;

    private MlKem(MlKemParameterSet parameterSet, MlKemParameters parameters, byte[] decapsulationKey)
    {
        ParameterSet = parameterSet;
        this.parameters = parameters;
        this.decapsulationKey = decapsulationKey;
    }

    /// <summary>The parameter set of the key this instance holds.</summary>
    public MlKemParameterSet ParameterSet { get; }

    /// <summary>Returns the length in bytes of an encapsulation key: 800, 1184 or 1568.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static int GetEncapsulationKeySize(MlKemParameterSet parameterSet) =>
        MlKemParameters.For(parameterSet).EncapsulationKeySize;

    /// <summary>Returns the length in bytes of a decapsulation key: 1632, 2400 or 3168.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static int GetDecapsulationKeySize(MlKemParameterSet parameterSet) =>
        MlKemParameters.For(parameterSet).DecapsulationKeySize;

    /// <summary>Returns the length in bytes of a ciphertext: 768, 1088 or 1568.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static int GetCiphertextSize(MlKemParameterSet parameterSet) =>
        MlKemParameters.For(parameterSet).CiphertextSize;

    /// <summary>
    /// Generates a key pair of <paramref name="parameterSet" /> from seeds drawn from
    /// <see cref="RandomNumberGenerator" /> (FIPS 203 algorithm 19).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static MlKem GenerateKey(MlKemParameterSet parameterSet)
    {
        Span<byte> seeds = stackalloc byte[2 * SeedSize];
        try
        {
            RandomNumberGenerator.Fill(seeds);
            return GenerateKey(parameterSet, seeds[..SeedSize], seeds[SeedSize..]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seeds);
        }
    }

    /// <summary>
    /// Generates the key pair of <paramref name="parameterSet" /> that the 32-byte seeds
    /// <paramref name="d" /> and <paramref name="z" /> determine (FIPS 203 algorithm 16,
    /// ML-KEM.KeyGen_internal), so published known answers reproduce.
    /// </summary>
    /// <exception cref="ArgumentException">A seed is not 32 bytes.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static MlKem GenerateKey(MlKemParameterSet parameterSet, ReadOnlySpan<byte> d, ReadOnlySpan<byte> z)
    {
        MlKemParameters parameters = MlKemParameters.For(parameterSet);
        RequireSize(d.Length, SeedSize, nameof(d));
        RequireSize(z.Length, SeedSize, nameof(z));
        byte[] key = new byte[parameters.DecapsulationKeySize];
        Span<byte> encapsulationKey = key.AsSpan(parameters.EncodedVectorSize, parameters.EncapsulationKeySize);
        MlKemPublicKeyEncryption.GenerateKeys(parameters, d, encapsulationKey, key.AsSpan(0, parameters.EncodedVectorSize));
        Sha3.HashData256(encapsulationKey, key.AsSpan(HashOffset(parameters), SeedSize));
        z.CopyTo(key.AsSpan(HashOffset(parameters) + SeedSize));
        return new MlKem(parameterSet, parameters, key);
    }

    /// <summary>
    /// Takes a copy of the decapsulation key <paramref name="decapsulationKey" /> of
    /// <paramref name="parameterSet" /> after FIPS 203 section 7.3's check that the hash it
    /// holds is H of the encapsulation key it holds.
    /// </summary>
    /// <exception cref="ArgumentException">The key has the wrong length or fails the hash check.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static MlKem ImportDecapsulationKey(MlKemParameterSet parameterSet, ReadOnlySpan<byte> decapsulationKey)
    {
        MlKemParameters parameters = MlKemParameters.For(parameterSet);
        RequireSize(decapsulationKey.Length, parameters.DecapsulationKeySize, nameof(decapsulationKey));
        Span<byte> hash = stackalloc byte[SeedSize];
        Sha3.HashData256(decapsulationKey.Slice(parameters.EncodedVectorSize, parameters.EncapsulationKeySize), hash);
        if (!CryptographicOperations.FixedTimeEquals(hash, decapsulationKey.Slice(HashOffset(parameters), SeedSize)))
        {
            throw new ArgumentException("The decapsulation key's hash does not match its encapsulation key.", nameof(decapsulationKey));
        }

        return new MlKem(parameterSet, parameters, decapsulationKey.ToArray());
    }

    /// <summary>
    /// Encapsulates a shared secret to <paramref name="encapsulationKey" /> with a message
    /// drawn from <see cref="RandomNumberGenerator" /> (FIPS 203 algorithm 20).
    /// </summary>
    /// <returns>
    /// <c>false</c>, with <paramref name="ciphertext" /> and <paramref name="sharedSecret" />
    /// zeroed, when the key fails FIPS 203 section 7.2's modulus check; otherwise <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static bool TryEncapsulate(
        MlKemParameterSet parameterSet,
        ReadOnlySpan<byte> encapsulationKey,
        Span<byte> ciphertext,
        Span<byte> sharedSecret)
    {
        Span<byte> message = stackalloc byte[MessageSize];
        try
        {
            RandomNumberGenerator.Fill(message);
            return TryEncapsulate(parameterSet, encapsulationKey, message, ciphertext, sharedSecret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
        }
    }

    /// <summary>
    /// Encapsulates the 32-byte <paramref name="message" /> m to
    /// <paramref name="encapsulationKey" /> (FIPS 203 algorithm 17,
    /// ML-KEM.Encaps_internal), so published known answers reproduce.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with <paramref name="ciphertext" /> and <paramref name="sharedSecret" />
    /// zeroed, when the key fails FIPS 203 section 7.2's modulus check; otherwise <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static bool TryEncapsulate(
        MlKemParameterSet parameterSet,
        ReadOnlySpan<byte> encapsulationKey,
        ReadOnlySpan<byte> message,
        Span<byte> ciphertext,
        Span<byte> sharedSecret)
    {
        MlKemParameters parameters = MlKemParameters.For(parameterSet);
        RequireSize(encapsulationKey.Length, parameters.EncapsulationKeySize, nameof(encapsulationKey));
        RequireSize(message.Length, MessageSize, nameof(message));
        RequireSize(ciphertext.Length, parameters.CiphertextSize, nameof(ciphertext));
        RequireSize(sharedSecret.Length, SharedSecretSize, nameof(sharedSecret));
        if (!MlKemPolynomial.AreAllBelowModulus(encapsulationKey[..parameters.EncodedVectorSize]))
        {
            ciphertext.Clear();
            sharedSecret.Clear();
            return false;
        }

        Span<byte> messageAndHash = stackalloc byte[2 * SeedSize];
        Span<byte> keyAndRandomness = stackalloc byte[2 * SeedSize];
        try
        {
            message.CopyTo(messageAndHash);
            Sha3.HashData256(encapsulationKey, messageAndHash[SeedSize..]);
            Sha3.HashData512(messageAndHash, keyAndRandomness);
            MlKemPublicKeyEncryption.Encrypt(parameters, encapsulationKey, message, keyAndRandomness[SeedSize..], ciphertext);
            keyAndRandomness[..SeedSize].CopyTo(sharedSecret);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(messageAndHash);
            CryptographicOperations.ZeroMemory(keyAndRandomness);
        }
    }

    /// <summary>Writes this key pair's encapsulation key to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void ExportEncapsulationKey(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireSize(destination.Length, parameters.EncapsulationKeySize, nameof(destination));
        EncapsulationKey.CopyTo(destination);
    }

    /// <summary>Writes the decapsulation key to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void ExportDecapsulationKey(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireSize(destination.Length, parameters.DecapsulationKeySize, nameof(destination));
        decapsulationKey.CopyTo(destination);
    }

    /// <summary>
    /// Decapsulates <paramref name="ciphertext" />, writing the shared secret to
    /// <paramref name="sharedSecret" /> (FIPS 203 algorithms 18 and 21). A ciphertext that
    /// does not re-encrypt to itself yields the implicit-rejection key J(z || c); the choice
    /// is a masked select after a branch-free comparison.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Decapsulate(ReadOnlySpan<byte> ciphertext, Span<byte> sharedSecret)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireSize(ciphertext.Length, parameters.CiphertextSize, nameof(ciphertext));
        RequireSize(sharedSecret.Length, SharedSecretSize, nameof(sharedSecret));
        ReadOnlySpan<byte> key = decapsulationKey;
        int hashOffset = HashOffset(parameters);
        Span<byte> messageAndHash = stackalloc byte[2 * SeedSize];
        Span<byte> keyAndRandomness = stackalloc byte[2 * SeedSize];
        Span<byte> rejectionKey = stackalloc byte[SharedSecretSize];
        Span<byte> reencrypted = stackalloc byte[parameters.CiphertextSize];
        try
        {
            MlKemPublicKeyEncryption.Decrypt(parameters, key[..parameters.EncodedVectorSize], ciphertext, messageAndHash[..SeedSize]);
            key.Slice(hashOffset, SeedSize).CopyTo(messageAndHash[SeedSize..]);
            Sha3.HashData512(messageAndHash, keyAndRandomness);
            using (Shake rejection = Shake.Create256())
            {
                rejection.AppendData(key[(hashOffset + SeedSize)..]);
                rejection.AppendData(ciphertext);
                rejection.Read(rejectionKey);
            }

            MlKemPublicKeyEncryption.Encrypt(parameters, EncapsulationKey, messageAndHash[..SeedSize], keyAndRandomness[SeedSize..], reencrypted);
            SelectSharedSecret(ciphertext, reencrypted, keyAndRandomness[..SeedSize], rejectionKey, sharedSecret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(messageAndHash);
            CryptographicOperations.ZeroMemory(keyAndRandomness);
            CryptographicOperations.ZeroMemory(rejectionKey);
            CryptographicOperations.ZeroMemory(reencrypted);
        }
    }

    /// <summary>Zeroes the decapsulation key; every later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(decapsulationKey);
        disposed = true;
    }

    private ReadOnlySpan<byte> EncapsulationKey =>
        decapsulationKey.AsSpan(parameters.EncodedVectorSize, parameters.EncapsulationKeySize);

    // Writes accepted where received equals reencrypted and rejected elsewhere, deciding
    // by a mask built from every byte of both, never by a branch.
    internal static void SelectSharedSecret(
        ReadOnlySpan<byte> received,
        ReadOnlySpan<byte> reencrypted,
        ReadOnlySpan<byte> accepted,
        ReadOnlySpan<byte> rejected,
        Span<byte> destination)
    {
        uint difference = 0;
        for (int index = 0; index < received.Length; index++)
        {
            difference |= (uint)(received[index] ^ reencrypted[index]);
        }

        uint equalMask = ConstantTime.EqualMask(difference, 0);
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = (byte)ConstantTime.Select(equalMask, accepted[index], rejected[index]);
        }
    }

    private static int HashOffset(MlKemParameters parameters) =>
        parameters.EncodedVectorSize + parameters.EncapsulationKeySize;

    private static void RequireSize(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"Must be exactly {expected} bytes.", parameterName);
        }
    }
}
