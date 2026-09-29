namespace Curl.Cryptography;

/// <summary>
/// One ML-DSA parameter set's numbers (FIPS 204 section 4, tables 1 and 2): the matrix
/// dimensions k and l, the secret width eta, the challenge weight tau, the mask range
/// gamma1, the low-order rounding range gamma2, the hint limit omega, the length of the
/// commitment hash, and the key and signature lengths they give.
/// </summary>
internal sealed class MlDsaParameters
{
    /// <summary>The length in bytes of the seeds xi, rho, K and rnd.</summary>
    public const int SeedSize = 32;

    /// <summary>The length in bytes of rho', mu and tr.</summary>
    public const int HashSize = 64;

    /// <summary>d, the number of bits Power2Round drops from t.</summary>
    public const int DroppedBits = 13;

    /// <summary>The bits per coefficient of t1: bitlen(q - 1) - d.</summary>
    public const int HighBitsOfTBits = 10;

    private static readonly MlDsaParameters MlDsa44 = new(4, 4, 2, 39, 17, 88, 80, 32);
    private static readonly MlDsaParameters MlDsa65 = new(6, 5, 4, 49, 19, 32, 55, 48);
    private static readonly MlDsaParameters MlDsa87 = new(8, 7, 2, 60, 19, 32, 75, 64);

    private MlDsaParameters(int rows, int columns, int eta, int tau, int gamma1Bits, int gamma2Divisor, int omega, int commitmentHashSize)
    {
        Rows = rows;
        Columns = columns;
        Eta = eta;
        Tau = tau;
        Gamma1Bits = gamma1Bits;
        Gamma2 = (MlDsaPolynomial.Modulus - 1) / gamma2Divisor;
        Omega = omega;
        CommitmentHashSize = commitmentHashSize;
    }

    /// <summary>k, the rows of A and the length of t, s2 and the hint.</summary>
    public int Rows { get; }

    /// <summary>l, the columns of A and the length of s1, y and z.</summary>
    public int Columns { get; }

    /// <summary>eta, the bound on the secret coefficients.</summary>
    public int Eta { get; }

    /// <summary>tau, the number of nonzero coefficients of the challenge.</summary>
    public int Tau { get; }

    /// <summary>log2 of gamma1, the range of the mask y.</summary>
    public int Gamma1Bits { get; }

    /// <summary>gamma1, the range of the mask y.</summary>
    public int Gamma1 => 1 << Gamma1Bits;

    /// <summary>gamma2, the low-order rounding range: (q - 1) / 88 or (q - 1) / 32.</summary>
    public int Gamma2 { get; }

    /// <summary>beta = tau * eta, the bound on the coefficients of c s1 and c s2.</summary>
    public int Beta => Tau * Eta;

    /// <summary>omega, the most ones a hint may hold.</summary>
    public int Omega { get; }

    /// <summary>lambda / 4, the length in bytes of the commitment hash c-tilde.</summary>
    public int CommitmentHashSize { get; }

    /// <summary>The bits per coefficient of s1 and s2: bitlen(2 eta).</summary>
    public int SecretBits => Eta == 2 ? 3 : 4;

    /// <summary>The bits per coefficient of z: 1 + bitlen(gamma1 - 1).</summary>
    public int MaskBits => Gamma1Bits + 1;

    /// <summary>The bits per coefficient of w1: bitlen((q - 1) / (2 gamma2) - 1).</summary>
    public int CommitmentBits => Gamma2 == (MlDsaPolynomial.Modulus - 1) / 88 ? 6 : 4;

    /// <summary>The length of a public key: rho and t1.</summary>
    public int PublicKeySize => SeedSize + (32 * Rows * HighBitsOfTBits);

    /// <summary>The length of a private key: rho, K, tr, s1, s2 and t0.</summary>
    public int PrivateKeySize =>
        (2 * SeedSize) + HashSize + (32 * (((Rows + Columns) * SecretBits) + (Rows * DroppedBits)));

    /// <summary>The length of a signature: c-tilde, z and the hint.</summary>
    public int SignatureSize => CommitmentHashSize + (32 * Columns * MaskBits) + Omega + Rows;

    /// <summary>The length of w1 encoded: w1Encode's output.</summary>
    public int EncodedCommitmentSize => 32 * Rows * CommitmentBits;

    /// <summary>Returns the numbers of <paramref name="parameterSet" />.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parameterSet" /> is not a defined value.</exception>
    public static MlDsaParameters For(MlDsaParameterSet parameterSet) => parameterSet switch
    {
        MlDsaParameterSet.MlDsa44 => MlDsa44,
        MlDsaParameterSet.MlDsa65 => MlDsa65,
        MlDsaParameterSet.MlDsa87 => MlDsa87,
        _ => throw new ArgumentOutOfRangeException(nameof(parameterSet), parameterSet, "Not an ML-DSA parameter set."),
    };
}
