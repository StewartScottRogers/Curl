using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// One brainpool curve's domain parameters (RFC 5639 section 3) with the arithmetic its
/// points and scalars need: the field modulo p and the group modulo q as
/// <see cref="MontgomeryModulus" />, the coefficients a, b and 3b and the generator in
/// Montgomery form, inversion by Fermat's little theorem, and the scalar checks and
/// conversions <see cref="BrainpoolEcdh" /> and <see cref="BrainpoolEcdsa" /> share.
/// </summary>
/// <remarks>
/// Every r1 curve has cofactor h = 1, so the group has prime order q, and p and q have the
/// same byte length (<see cref="Length" />) with their top bit set. The parameters are
/// public; only the scalars and points passed in may be secret, and every member that
/// takes one is constant-time in it.
/// </remarks>
internal sealed class BrainpoolDomainParameters
{
    private static readonly BrainpoolDomainParameters P256r1 = new(
        "A9FB57DBA1EEA9BC3E660A909D838D726E3BF623D52620282013481D1F6E5377",
        "7D5A0975FC2C3057EEF67530417AFFE7FB8055C126DC5C6CE94A4B44F330B5D9",
        "26DC5C6CE94A4B44F330B5D9BBD77CBF958416295CF7E1CE6BCCDC18FF8C07B6",
        "8BD2AEB9CB7E57CB2C4B482FFC81B7AFB9DE27E1E3BD23C23A4453BD9ACE3262",
        "547EF835C3DAC4FD97F8461A14611DC9C27745132DED8E545C1D54C72F046997",
        "A9FB57DBA1EEA9BC3E660A909D838D718C397AA3B561A6F7901E0E82974856A7");

    private static readonly BrainpoolDomainParameters P384r1 = new(
        "8CB91E82A3386D280F5D6F7E50E641DF152F7109ED5456B412B1DA197FB71123ACD3A729901D1A71874700133107EC53",
        "7BC382C63D8C150C3C72080ACE05AFA0C2BEA28E4FB22787139165EFBA91F90F8AA5814A503AD4EB04A8C7DD22CE2826",
        "04A8C7DD22CE28268B39B55416F0447C2FB77DE107DCD2A62E880EA53EEB62D57CB4390295DBC9943AB78696FA504C11",
        "1D1C64F068CF45FFA2A63A81B7C13F6B8847A3E77EF14FE3DB7FCAFE0CBD10E8E826E03436D646AAEF87B2E247D4AF1E",
        "8ABE1D7520F9C2A45CB1EB8E95CFD55262B70B29FEEC5864E19C054FF99129280E4646217791811142820341263C5315",
        "8CB91E82A3386D280F5D6F7E50E641DF152F7109ED5456B31F166E6CAC0425A7CF3AB6AF6B7FC3103B883202E9046565");

    private static readonly BrainpoolDomainParameters P512r1 = new(
        "AADD9DB8DBE9C48B3FD4E6AE33C9FC07CB308DB3B3C9D20ED6639CCA703308717D4D9B009BC66842AECDA12AE6A380E62881FF2F2D82C68528AA6056583A48F3",
        "7830A3318B603B89E2327145AC234CC594CBDD8D3DF91610A83441CAEA9863BC2DED5D5AA8253AA10A2EF1C98B9AC8B57F1117A72BF2C7B9E7C1AC4D77FC94CA",
        "3DF91610A83441CAEA9863BC2DED5D5AA8253AA10A2EF1C98B9AC8B57F1117A72BF2C7B9E7C1AC4D77FC94CADC083E67984050B75EBAE5DD2809BD638016F723",
        "81AEE4BDD82ED9645A21322E9C4C6A9385ED9F70B5D916C1B43B62EEF4D0098EFF3B1F78E2D0D48D50D1687B93B97D5F7C6D5047406A5E688B352209BCB9F822",
        "7DDE385D566332ECC0EABFA9CF7822FDF209F70024A57B1AA000C55B881F8111B2DCDE494A5F485E5BCA4BD88A2763AED1CA2B2FA8F0540678CD1E0F3AD80892",
        "AADD9DB8DBE9C48B3FD4E6AE33C9FC07CB308DB3B3C9D20ED6639CCA70330870553E5C414CA92619418661197FAC10471DB1D381085DDADDB58796829CA90069");

    private readonly uint[] a;
    private readonly uint[] b;
    private readonly uint[] threeB;
    private readonly uint[] one;
    private readonly uint[] generator;
    private readonly byte[] fieldMinusTwo;
    private readonly byte[] orderMinusTwo;

    private BrainpoolDomainParameters(string prime, string coefficientA, string coefficientB, string generatorX, string generatorY, string order)
    {
        byte[] primeBytes = Convert.FromHexString(prime);
        byte[] orderBytes = Convert.FromHexString(order);
        Length = primeBytes.Length;
        Field = new MontgomeryModulus(primeBytes);
        Order = new MontgomeryModulus(orderBytes);
        fieldMinusTwo = MontgomeryModulus.MinusTwo(primeBytes);
        orderMinusTwo = MontgomeryModulus.MinusTwo(orderBytes);
        int n = LimbCount;
        uint[] scratch = new uint[n + 2];
        a = ToMontgomeryForm(coefficientA, scratch);
        b = ToMontgomeryForm(coefficientB, scratch);
        threeB = new uint[n];
        Field.Add(threeB, b, b, scratch);
        Field.Add(threeB, threeB, b, scratch);
        one = new uint[n];
        uint[] unit = new uint[n];
        unit[0] = 1;
        Field.ToMontgomeryForm(unit, one, scratch);
        generator = new uint[3 * n];
        ToMontgomeryForm(generatorX, scratch).CopyTo(generator, 0);
        ToMontgomeryForm(generatorY, scratch).CopyTo(generator, n);
        one.CopyTo(generator, 2 * n);
    }

    /// <summary>The length in bytes of p and of q: of a coordinate, a scalar, a shared secret, and each half of a signature.</summary>
    public int Length { get; }

    /// <summary>The number of 32-bit limbs of a field element or a scalar.</summary>
    public int LimbCount => Field.LimbCount;

    /// <summary>Arithmetic modulo the field prime p.</summary>
    public MontgomeryModulus Field { get; }

    /// <summary>Arithmetic modulo the group order q.</summary>
    public MontgomeryModulus Order { get; }

    /// <summary>The coefficient a, in Montgomery form.</summary>
    public ReadOnlySpan<uint> A => a;

    /// <summary>The coefficient b, in Montgomery form.</summary>
    public ReadOnlySpan<uint> B => b;

    /// <summary>3b, in Montgomery form: the constant of the complete addition formulas.</summary>
    public ReadOnlySpan<uint> ThreeB => threeB;

    /// <summary>1, in Montgomery form.</summary>
    public ReadOnlySpan<uint> One => one;

    /// <summary>The generator G as projective (X : Y : 1), three runs of <see cref="LimbCount" /> limbs in Montgomery form.</summary>
    public ReadOnlySpan<uint> Generator => generator;

    /// <summary>Returns the domain parameters of <paramref name="curve" />.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="curve" /> is not a <see cref="BrainpoolCurve" /> value.</exception>
    public static BrainpoolDomainParameters For(BrainpoolCurve curve) => curve switch
    {
        BrainpoolCurve.BrainpoolP256r1 => P256r1,
        BrainpoolCurve.BrainpoolP384r1 => P384r1,
        BrainpoolCurve.BrainpoolP512r1 => P512r1,
        _ => throw new ArgumentOutOfRangeException(nameof(curve), curve, "The curve must be brainpoolP256r1, brainpoolP384r1 or brainpoolP512r1."),
    };

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" />^-1 modulo p, in ordinary
    /// form, for a value in Montgomery form: 0 gives 0. A fixed exponentiation by p - 2.
    /// </summary>
    public void InvertField(ReadOnlySpan<uint> value, Span<uint> result)
    {
        uint[] ordinary = new uint[(3 * LimbCount) + 2];
        try
        {
            FromMontgomeryForm(Field, value, ordinary.AsSpan(0, LimbCount), ordinary.AsSpan(LimbCount));
            Field.Exponentiate(ordinary.AsSpan(0, LimbCount), fieldMinusTwo, result);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(ordinary.AsSpan()));
        }
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="value" />^-1 modulo q, both in ordinary form: a fixed exponentiation by q - 2.</summary>
    public void InvertOrder(ReadOnlySpan<uint> value, Span<uint> result) => Order.Exponentiate(value, orderMinusTwo, result);

    /// <summary>
    /// Reads the big-endian <paramref name="scalar" />, exactly <see cref="Length" /> bytes,
    /// into <paramref name="limbs" /> and returns whether it lies in [1, q - 1]. Both checks
    /// run whatever the value, so a secret scalar costs the same time whatever it is.
    /// </summary>
    public bool TryReadScalar(ReadOnlySpan<byte> scalar, Span<uint> limbs)
    {
        MontgomeryModulus.ToLimbs(scalar, limbs);
        return Order.IsBelowModulus(limbs) & !IsZero(limbs);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to z mod q, where z is the leftmost min(N, outlen)
    /// bits of <paramref name="hash" /> (SEC 1 section 4.1.3 step 5; RFC 6979's bits2int).
    /// N is a whole number of bytes on every brainpool curve, so that is the first N / 8 bytes.
    /// </summary>
    public void ReduceHash(ReadOnlySpan<byte> hash, Span<uint> result)
    {
        uint[] leftmost = new uint[LimbCount];
        MontgomeryModulus.ToLimbs(hash[..Math.Min(hash.Length, Length)], leftmost);
        Order.Reduce(leftmost, result);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(leftmost.AsSpan()));
    }

    /// <summary>Returns whether every limb of <paramref name="limbs" /> is zero, reading all of them.</summary>
    public static bool IsZero(ReadOnlySpan<uint> limbs) => ConstantTime.IsAllZero(MemoryMarshal.AsBytes(limbs));

    /// <summary>Sets <paramref name="result" /> to <paramref name="value" /> * R^-1: a Montgomery-form value back in ordinary form.</summary>
    public static void FromMontgomeryForm(MontgomeryModulus modulus, ReadOnlySpan<uint> value, Span<uint> result, Span<uint> scratch)
    {
        Span<uint> unit = scratch[..modulus.LimbCount];
        unit.Clear();
        unit[0] = 1;
        modulus.Multiply(result, value, unit, scratch[modulus.LimbCount..]);
    }

    private uint[] ToMontgomeryForm(string hex, uint[] scratch)
    {
        uint[] limbs = new uint[LimbCount];
        MontgomeryModulus.ToLimbs(Convert.FromHexString(hex), limbs);
        Field.ToMontgomeryForm(limbs, limbs, scratch);
        return limbs;
    }
}
