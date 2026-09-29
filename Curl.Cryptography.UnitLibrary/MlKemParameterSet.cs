namespace Curl.Cryptography;

/// <summary>The three ML-KEM parameter sets of FIPS 203 section 8, table 2.</summary>
public enum MlKemParameterSet
{
    /// <summary>ML-KEM-512: k = 2, security category 1.</summary>
    MlKem512,

    /// <summary>ML-KEM-768: k = 3, security category 3.</summary>
    MlKem768,

    /// <summary>ML-KEM-1024: k = 4, security category 5.</summary>
    MlKem1024,
}
