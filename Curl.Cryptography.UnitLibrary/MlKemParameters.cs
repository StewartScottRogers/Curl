namespace Curl.Cryptography;

/// <summary>
/// One ML-KEM parameter set's numbers (FIPS 203 section 8, tables 2 and 3): the module
/// rank k, the noise widths eta1 and eta2, the compression widths du and dv, and the
/// key and ciphertext lengths they give.
/// </summary>
internal sealed class MlKemParameters
{
    /// <summary>eta2, the same for all three parameter sets.</summary>
    public const int Eta2 = 2;

    /// <summary>The length in bytes of one polynomial encoded at 12 bits per coefficient.</summary>
    public const int EncodedPolynomialSize = 384;

    /// <summary>The length in bytes of a seed, a hash, a message or a shared secret.</summary>
    public const int SeedSize = 32;

    private static readonly MlKemParameters MlKem512 = new(2, 3, 10, 4);
    private static readonly MlKemParameters MlKem768 = new(3, 2, 10, 4);
    private static readonly MlKemParameters MlKem1024 = new(4, 2, 11, 5);

    private MlKemParameters(int rank, int eta1, int du, int dv)
    {
        Rank = rank;
        Eta1 = eta1;
        Du = du;
        Dv = dv;
    }

    /// <summary>k, the number of polynomials in a vector.</summary>
    public int Rank { get; }

    /// <summary>eta1, the noise width of the secret and of encryption's y.</summary>
    public int Eta1 { get; }

    /// <summary>du, the bits per coefficient of the ciphertext's u.</summary>
    public int Du { get; }

    /// <summary>dv, the bits per coefficient of the ciphertext's v.</summary>
    public int Dv { get; }

    /// <summary>The length of a vector of k polynomials at 12 bits per coefficient: K-PKE's decryption key.</summary>
    public int EncodedVectorSize => EncodedPolynomialSize * Rank;

    /// <summary>The length of an encapsulation key: the encoded t-hat and rho.</summary>
    public int EncapsulationKeySize => EncodedVectorSize + SeedSize;

    /// <summary>The length of a decapsulation key: K-PKE's key, the encapsulation key, H(ek) and z.</summary>
    public int DecapsulationKeySize => EncodedVectorSize + EncapsulationKeySize + (2 * SeedSize);

    /// <summary>The length of the ciphertext's first part, u compressed to du bits.</summary>
    public int CompressedVectorSize => 32 * Du * Rank;

    /// <summary>The length of a ciphertext: u at du bits and v at dv bits.</summary>
    public int CiphertextSize => CompressedVectorSize + (32 * Dv);

    /// <summary>Returns the numbers of <paramref name="parameterSet" />.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static MlKemParameters For(MlKemParameterSet parameterSet) => parameterSet switch
    {
        MlKemParameterSet.MlKem512 => MlKem512,
        MlKemParameterSet.MlKem768 => MlKem768,
        MlKemParameterSet.MlKem1024 => MlKem1024,
        _ => throw new ArgumentOutOfRangeException(nameof(parameterSet), parameterSet, "Not an ML-KEM parameter set."),
    };
}
