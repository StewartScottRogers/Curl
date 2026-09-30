namespace Curl.Cryptography;

/// <summary>The three ML-DSA parameter sets of FIPS 204 section 4, table 1.</summary>
public enum MlDsaParameterSet
{
    /// <summary>ML-DSA-44: (k, l) = (4, 4), security category 2.</summary>
    MlDsa44,

    /// <summary>ML-DSA-65: (k, l) = (6, 5), security category 3.</summary>
    MlDsa65,

    /// <summary>ML-DSA-87: (k, l) = (8, 7), security category 5.</summary>
    MlDsa87,
}
