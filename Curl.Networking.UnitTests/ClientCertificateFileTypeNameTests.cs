namespace Curl.Networking;

/// <summary>
/// <see cref="ClientCertificateFileTypeName" />: the <c>--cert-type</c> and <c>--key-type</c>
/// names curl knows, in any case.
/// </summary>
[TestClass]
public sealed class ClientCertificateFileTypeNameTests
{
    [TestMethod]
    [DataRow(null, ClientCertificateFileType.Pem)]
    [DataRow("PEM", ClientCertificateFileType.Pem)]
    [DataRow("pem", ClientCertificateFileType.Pem)]
    [DataRow("DER", ClientCertificateFileType.Der)]
    [DataRow("Der", ClientCertificateFileType.Der)]
    [DataRow("P12", ClientCertificateFileType.Pkcs12)]
    [DataRow("p12", ClientCertificateFileType.Pkcs12)]
    [DataRow("ENG", ClientCertificateFileType.Engine)]
    [DataRow("PROV", ClientCertificateFileType.Provider)]
    [DataRow("PKCS12", ClientCertificateFileType.Unsupported)]
    [DataRow("FOO", ClientCertificateFileType.Unsupported)]
    public void Parse_ATypeName_ReturnsTheTypeItNames(string? name, object expected)
    {
        Assert.AreEqual((ClientCertificateFileType)expected, ClientCertificateFileTypeName.Parse(name));
    }
}
