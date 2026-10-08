using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="ClientCertificateFileTypeName" />: the <c>--cert-type</c> and <c>--key-type</c>
/// names curl knows, in any case.
/// </summary>
[TestClass]
public sealed class ClientCertificateFileTypeNameTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Arrange("name", name);
        Diagnostics.Arrange("expected type", expected);
        var parsed = ClientCertificateFileTypeName.Parse(name);
        Diagnostics.Act("parsed type", parsed);
        Diagnostics.Assert("parsed type", expected, parsed);
        Assert.AreEqual((ClientCertificateFileType)expected, ClientCertificateFileTypeName.Parse(name));
    }
}
