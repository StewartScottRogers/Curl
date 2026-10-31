using Curl.Cryptography;

namespace Curl.Extension.MlDsaCertificates;

/// <summary>The ML-DSA algorithm identifiers of RFC 9881 section 2, which name both the signature and the public key algorithm.</summary>
internal static class MlDsaAlgorithmIdentifiers
{
    /// <summary>The object identifier <c>id-ml-dsa-44</c>.</summary>
    internal const string MlDsa44 = "2.16.840.1.101.3.4.3.17";

    /// <summary>The object identifier <c>id-ml-dsa-65</c>.</summary>
    internal const string MlDsa65 = "2.16.840.1.101.3.4.3.18";

    /// <summary>The object identifier <c>id-ml-dsa-87</c>.</summary>
    internal const string MlDsa87 = "2.16.840.1.101.3.4.3.19";

    /// <summary>Gives the parameter set an ML-DSA algorithm identifier names.</summary>
    /// <param name="algorithm">The algorithm identifier.</param>
    /// <returns>The parameter set, or <see langword="null" /> when it is not an ML-DSA identifier or carries parameters, which section 2 says must be absent.</returns>
    internal static MlDsaParameterSet? ParameterSetOf(CertificateAlgorithm algorithm) =>
        algorithm.HasParameters ? null : ParameterSetOf(algorithm.Oid);

    /// <summary>Gives the parameter set an ML-DSA object identifier names.</summary>
    /// <param name="oid">The object identifier, in dotted form.</param>
    /// <returns>The parameter set, or <see langword="null" /> when it is not an ML-DSA identifier.</returns>
    internal static MlDsaParameterSet? ParameterSetOf(string oid) => oid switch
    {
        MlDsa44 => MlDsaParameterSet.MlDsa44,
        MlDsa65 => MlDsaParameterSet.MlDsa65,
        MlDsa87 => MlDsaParameterSet.MlDsa87,
        _ => null,
    };

    /// <summary>Gives the name RFC 9881 and FIPS 204 give a parameter set.</summary>
    /// <param name="parameterSet">The parameter set.</param>
    /// <returns><c>ML-DSA-44</c>, <c>ML-DSA-65</c> or <c>ML-DSA-87</c>.</returns>
    internal static string NameOf(MlDsaParameterSet parameterSet) => parameterSet switch
    {
        MlDsaParameterSet.MlDsa44 => "ML-DSA-44",
        MlDsaParameterSet.MlDsa65 => "ML-DSA-65",
        _ => "ML-DSA-87",
    };
}
