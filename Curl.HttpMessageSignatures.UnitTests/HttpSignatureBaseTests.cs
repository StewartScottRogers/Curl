namespace Curl.HttpMessageSignatures;

/// <summary>Checks the signature parameters and base against curl 8.22.0's <c>build_sig_params</c> and <c>build_sig_base</c>.</summary>
[TestClass]
public sealed class HttpSignatureBaseTests
{
    [TestMethod]
    public void FormatParameters_KeyIdAndAlgorithm_WritesCreatedKeyIdAlgInOrder()
    {
        var parameters = HttpSignatureBase.FormatParameters(["@method", "content-type"], 1700000000, "my-key", "ed25519");

        Assert.AreEqual("(\"@method\" \"content-type\");created=1700000000;keyid=\"my-key\";alg=\"ed25519\"", parameters);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void FormatParameters_NoKeyIdNoAlgorithm_WritesCreatedOnly(string? keyId)
    {
        var parameters = HttpSignatureBase.FormatParameters(["@path"], 0, keyId, null);

        Assert.AreEqual("(\"@path\");created=0", parameters);
    }

    [TestMethod]
    public void FormatParameters_QuoteAndBackslashInKeyId_EscapesBoth()
    {
        var parameters = HttpSignatureBase.FormatParameters([], -1, "a\"b\\c", null);

        Assert.AreEqual("();created=-1;keyid=\"a\\\"b\\\\c\"", parameters);
    }

    [TestMethod]
    [DataRow("a\u0001b")]
    [DataRow("a\u007fb")]
    public void FormatParameters_ControlCharacterInKeyId_ThrowsBadArgument(string keyId)
    {
        var exception = Assert.ThrowsExactly<HttpSignatureException>(() => HttpSignatureBase.FormatParameters(["@path"], 1, keyId, null));

        Assert.AreEqual(HttpSignatureException.BadArgumentMessage, exception.Message);
    }

    [TestMethod]
    public void FormatParameters_NonAsciiKeyId_KeepsIt()
    {
        var parameters = HttpSignatureBase.FormatParameters([], 1, "clé\u0080", null);

        Assert.AreEqual("();created=1;keyid=\"clé\u0080\"", parameters);
    }

    [TestMethod]
    public void Build_Components_WritesOneLinePerComponentThenParametersWithNoLineFeed()
    {
        var signatureBase = HttpSignatureBase.Build([KeyValuePair.Create("@method", "GET"), KeyValuePair.Create("x-a", "1, 2")], "(\"@method\" \"x-a\");created=5");

        Assert.AreEqual("\"@method\": GET\n\"x-a\": 1, 2\n\"@signature-params\": (\"@method\" \"x-a\");created=5", signatureBase);
    }
}
