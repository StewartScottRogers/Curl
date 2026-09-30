using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// ML-DSA, the module-lattice digital signature algorithm of FIPS 204, in its three
/// parameter sets: key generation, hedged and deterministic signing, and verification, all
/// pure ML-DSA with a context string of up to 255 bytes (FIPS 204 algorithms 1 to 3). An
/// instance holds one key pair; verification is static, needing only the public key. The
/// TLS 1.3 signature schemes <c>mldsa44</c>, <c>mldsa65</c> and <c>mldsa87</c> use it
/// (ADR-0118: the BCL's <c>MLDsa</c> exists only on Linux with OpenSSL 3.5+ and on
/// Windows Insider builds). SHAKE128 and SHAKE256 are the hand-built <see cref="Shake" />.
/// </summary>
/// <remarks>
/// Key generation and signing are constant-time in the private key, with the exceptions
/// FIPS 204 allows: the signing loop rejects attempts until one passes its norm and hint
/// checks, so the number of attempts leaks, and the samplers of s1, s2 and the challenge
/// reject out-of-range bytes of a secret SHAKE256 stream, so which bytes were rejected
/// leaks, but never the values kept. Everything else - the NTT, reduction mod q, the
/// rounding functions, the norm checks, the hint and the placing of the challenge's
/// coefficients - has no secret-dependent branch or table index. Every secret temporary is
/// zeroed before returning; <see cref="Dispose" /> zeroes the private key. Verification
/// works on public data only.
/// </remarks>
public sealed class MlDsa : IDisposable
{
    /// <summary>The length in bytes of key generation's seed xi.</summary>
    public const int SeedSize = MlDsaParameters.SeedSize;

    /// <summary>The length in bytes of signing's randomness rnd.</summary>
    public const int RandomnessSize = MlDsaParameters.SeedSize;

    /// <summary>The longest context FIPS 204 allows, in bytes.</summary>
    public const int MaximumContextSize = 255;

    private readonly MlDsaParameters parameters;
    private readonly byte[] publicKey;
    private readonly byte[] privateKey;
    private bool disposed;

    private MlDsa(MlDsaParameterSet parameterSet, MlDsaParameters parameters, byte[] publicKey, byte[] privateKey)
    {
        ParameterSet = parameterSet;
        this.parameters = parameters;
        this.publicKey = publicKey;
        this.privateKey = privateKey;
    }

    /// <summary>The parameter set of the key pair this instance holds.</summary>
    public MlDsaParameterSet ParameterSet { get; }

    /// <summary>Returns the length in bytes of a public key: 1312, 1952 or 2592.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static int GetPublicKeySize(MlDsaParameterSet parameterSet) =>
        MlDsaParameters.For(parameterSet).PublicKeySize;

    /// <summary>Returns the length in bytes of a private key: 2560, 4032 or 4896.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static int GetPrivateKeySize(MlDsaParameterSet parameterSet) =>
        MlDsaParameters.For(parameterSet).PrivateKeySize;

    /// <summary>Returns the length in bytes of a signature: 2420, 3309 or 4627.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static int GetSignatureSize(MlDsaParameterSet parameterSet) =>
        MlDsaParameters.For(parameterSet).SignatureSize;

    /// <summary>
    /// Generates a key pair of <paramref name="parameterSet" /> from a seed drawn from
    /// <see cref="RandomNumberGenerator" /> (FIPS 204 algorithm 1).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static MlDsa GenerateKey(MlDsaParameterSet parameterSet)
    {
        Span<byte> seed = stackalloc byte[SeedSize];
        try
        {
            RandomNumberGenerator.Fill(seed);
            return GenerateKey(parameterSet, seed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }
    }

    /// <summary>
    /// Generates the key pair of <paramref name="parameterSet" /> that the 32-byte
    /// <paramref name="seed" /> xi determines (FIPS 204 algorithm 6, ML-DSA.KeyGen_internal),
    /// so published known answers reproduce.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="seed" /> is not 32 bytes.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static MlDsa GenerateKey(MlDsaParameterSet parameterSet, ReadOnlySpan<byte> seed)
    {
        MlDsaParameters parameters = MlDsaParameters.For(parameterSet);
        RequireSize(seed.Length, SeedSize, nameof(seed));
        byte[] publicKey = new byte[parameters.PublicKeySize];
        byte[] privateKey = new byte[parameters.PrivateKeySize];
        MlDsaInternalFunctions.GenerateKeys(parameters, seed, publicKey, privateKey);
        return new MlDsa(parameterSet, parameters, publicKey, privateKey);
    }

    /// <summary>
    /// Takes a copy of the private key <paramref name="privateKey" /> of
    /// <paramref name="parameterSet" />, recomputing its public key from rho, s1 and s2 and
    /// checking that the tr and t0 it holds are the ones that public key gives.
    /// </summary>
    /// <exception cref="ArgumentException">The key has the wrong length, or its tr or t0 does not match its s1 and s2.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static MlDsa ImportPrivateKey(MlDsaParameterSet parameterSet, ReadOnlySpan<byte> privateKey)
    {
        MlDsaParameters parameters = MlDsaParameters.For(parameterSet);
        RequireSize(privateKey.Length, parameters.PrivateKeySize, nameof(privateKey));
        byte[] publicKey = new byte[parameters.PublicKeySize];
        byte[] rederived = new byte[parameters.PrivateKeySize];
        int[] s1 = new int[parameters.Columns * MlDsaPolynomial.Degree];
        int[] s2 = new int[parameters.Rows * MlDsaPolynomial.Degree];
        try
        {
            MlDsaInternalFunctions.DecodeSecrets(parameters, privateKey, s1, s2);
            MlDsaInternalFunctions.DeriveKeys(
                parameters,
                privateKey[..SeedSize],
                privateKey.Slice(SeedSize, SeedSize),
                s1,
                s2,
                publicKey,
                rederived);
            if (!CryptographicOperations.FixedTimeEquals(rederived, privateKey))
            {
                throw new ArgumentException("The private key's tr or t0 does not match its s1 and s2.", nameof(privateKey));
            }

            return new MlDsa(parameterSet, parameters, publicKey, rederived);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(rederived);
            throw;
        }
        finally
        {
            Array.Clear(s1);
            Array.Clear(s2);
        }
    }

    /// <summary>
    /// Verifies <paramref name="signature" /> over <paramref name="message" /> under
    /// <paramref name="context" /> against <paramref name="publicKey" /> of
    /// <paramref name="parameterSet" /> (FIPS 204 algorithm 3).
    /// </summary>
    /// <returns>
    /// <c>true</c> when the signature is valid; <c>false</c> when its hint is malformed, its
    /// z is out of range, or it does not match the message, context and key.
    /// </returns>
    /// <exception cref="ArgumentException">A span has the wrong length, or the context is longer than <see cref="MaximumContextSize" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static bool VerifyData(
        MlDsaParameterSet parameterSet,
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> context,
        ReadOnlySpan<byte> signature)
    {
        MlDsaParameters parameters = MlDsaParameters.For(parameterSet);
        RequireSize(publicKey.Length, parameters.PublicKeySize, nameof(publicKey));
        RequireSize(signature.Length, parameters.SignatureSize, nameof(signature));
        RequireContext(context);
        Span<byte> tr = stackalloc byte[MlDsaParameters.HashSize];
        Span<byte> mu = stackalloc byte[MlDsaParameters.HashSize];
        Shake.HashData256(publicKey, tr);
        MlDsaInternalFunctions.ComputeMessageRepresentative(tr, context, message, mu);
        return MlDsaInternalFunctions.Verify(parameters, publicKey, mu, signature);
    }

    /// <summary>Writes this key pair's public key to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void ExportPublicKey(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireSize(destination.Length, parameters.PublicKeySize, nameof(destination));
        publicKey.CopyTo(destination);
    }

    /// <summary>Writes the private key to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void ExportPrivateKey(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireSize(destination.Length, parameters.PrivateKeySize, nameof(destination));
        privateKey.CopyTo(destination);
    }

    /// <summary>
    /// Signs <paramref name="message" /> under <paramref name="context" /> into
    /// <paramref name="signature" /> with randomness drawn from
    /// <see cref="RandomNumberGenerator" />: FIPS 204's default, hedged variant (algorithm 2).
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length, or the context is longer than <see cref="MaximumContextSize" />.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void SignData(ReadOnlySpan<byte> message, ReadOnlySpan<byte> context, Span<byte> signature)
    {
        Span<byte> randomness = stackalloc byte[RandomnessSize];
        try
        {
            RandomNumberGenerator.Fill(randomness);
            SignData(message, context, randomness, signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(randomness);
        }
    }

    /// <summary>
    /// Signs <paramref name="message" /> under <paramref name="context" /> into
    /// <paramref name="signature" /> with the 32 bytes <paramref name="randomness" /> as rnd
    /// (FIPS 204 algorithm 2 with rnd given), so published known answers reproduce.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length, or the context is longer than <see cref="MaximumContextSize" />.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void SignData(ReadOnlySpan<byte> message, ReadOnlySpan<byte> context, ReadOnlySpan<byte> randomness, Span<byte> signature)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireSize(randomness.Length, RandomnessSize, nameof(randomness));
        RequireSize(signature.Length, parameters.SignatureSize, nameof(signature));
        RequireContext(context);
        Span<byte> mu = stackalloc byte[MlDsaParameters.HashSize];
        try
        {
            ReadOnlySpan<byte> tr = privateKey.AsSpan(2 * SeedSize, MlDsaParameters.HashSize);
            MlDsaInternalFunctions.ComputeMessageRepresentative(tr, context, message, mu);
            MlDsaInternalFunctions.Sign(parameters, privateKey, mu, randomness, signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mu);
        }
    }

    /// <summary>
    /// Signs <paramref name="message" /> under <paramref name="context" /> into
    /// <paramref name="signature" /> with rnd all zero: FIPS 204's deterministic variant,
    /// the same signature every time.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length, or the context is longer than <see cref="MaximumContextSize" />.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void SignDataDeterministic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> context, Span<byte> signature) =>
        SignData(message, context, stackalloc byte[RandomnessSize], signature);

    /// <summary>Zeroes the private key; every later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(privateKey);
        disposed = true;
    }

    private static void RequireSize(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"Must be exactly {expected} bytes.", parameterName);
        }
    }

    private static void RequireContext(ReadOnlySpan<byte> context)
    {
        if (context.Length > MaximumContextSize)
        {
            throw new ArgumentException(
                $"An ML-DSA context is at most {MaximumContextSize} bytes; the one given is {context.Length}.",
                nameof(context));
        }
    }
}
