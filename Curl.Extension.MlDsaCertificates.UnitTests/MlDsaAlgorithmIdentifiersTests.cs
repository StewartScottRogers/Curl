using Curl.Cryptography;

namespace Curl.Extension.MlDsaCertificates.UnitTests;

[TestClass]
public sealed class MlDsaAlgorithmIdentifiersTests
{
    [TestMethod]
    [DataRow("2.16.840.1.101.3.4.3.17", MlDsaParameterSet.MlDsa44, "ML-DSA-44")]
    [DataRow("2.16.840.1.101.3.4.3.18", MlDsaParameterSet.MlDsa65, "ML-DSA-65")]
    [DataRow("2.16.840.1.101.3.4.3.19", MlDsaParameterSet.MlDsa87, "ML-DSA-87")]
    public void ParameterSetOf_Rfc9881Section2Identifier_GivesItsParameterSet(string oid, MlDsaParameterSet parameterSet, string name)
    {
        Assert.AreEqual(parameterSet, MlDsaAlgorithmIdentifiers.ParameterSetOf(oid));
        Assert.AreEqual(parameterSet, MlDsaAlgorithmIdentifiers.ParameterSetOf(new CertificateAlgorithm(oid, false)));
        Assert.AreEqual(name, MlDsaAlgorithmIdentifiers.NameOf(parameterSet));
    }

    [TestMethod]
    public void ParameterSetOf_IdentifierWithParametersOrNotMlDsa_GivesNone()
    {
        Assert.IsNull(MlDsaAlgorithmIdentifiers.ParameterSetOf(new CertificateAlgorithm(MlDsaAlgorithmIdentifiers.MlDsa44, true)));
        Assert.IsNull(MlDsaAlgorithmIdentifiers.ParameterSetOf("1.3.101.112"));
    }
}
