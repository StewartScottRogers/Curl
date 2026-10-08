using Curl.Testing;

namespace Curl.Tls;

/// <summary>The combinations and key lengths the record states refuse.</summary>
[TestClass]
public sealed class Tls12RecordProtectionParametersTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls11, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None)]
    [DataRow(TlsProtocolVersion.Tls10, Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.HmacSha256)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.None)]
    [DataRow(TlsProtocolVersion.Tls11, Tls12BulkCipher.Aes128Ccm, Tls12MacAlgorithm.None)]
    [DataRow(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes256Ccm8, Tls12MacAlgorithm.None)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Rc4128, Tls12MacAlgorithm.None)]
    public void ACombinationNoSuiteNamesIsRefused(TlsProtocolVersion version, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm)
    {
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);
        Diagnostics.Arrange("parameters", $"{version}, {bulkCipher}, {macAlgorithm}");

        ArgumentException writeRefusal = Assert.ThrowsExactly<ArgumentException>(() => Tls12RecordWriteState.Create(parameters, keys, SystemTlsRandomSource.Instance));
        ArgumentException readRefusal = Assert.ThrowsExactly<ArgumentException>(() => Tls12RecordReadState.Create(parameters, keys));

        Diagnostics.Act("write state refusal", writeRefusal.Message);
        Diagnostics.Act("read state refusal", readRefusal.Message);
        Diagnostics.Assert("both refused with", nameof(ArgumentException), $"{writeRefusal.GetType().Name}, {readRefusal.GetType().Name}");
    }

    [TestMethod]
    [DataRow(19, 16, 0)]
    [DataRow(20, 15, 0)]
    [DataRow(20, 16, 16)]
    public void AKeyOfTheWrongLengthIsRefused(int macKeyLength, int keyLength, int ivLength)
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        Diagnostics.Arrange("key lengths", $"mac key {macKeyLength}, key {keyLength}, iv {ivLength}; Aes128Cbc with HmacSha1 wants {parameters.MacKeyLength}, {parameters.KeyLength}, {parameters.FixedIvLength}");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Tls12RecordReadState.Create(parameters, new Tls12WriteKeys(new byte[macKeyLength], new byte[keyLength], new byte[ivLength])));

        Diagnostics.Act("exception", exception.Message);
        Diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void NullArgumentsAreRefused()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Null, Tls12MacAlgorithm.None);
        Diagnostics.Arrange("parameters", "Tls12, Null, no mac; each call given one null argument");

        List<string> refused =
        [
            Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordWriteState.Create(null!, Tls12WriteKeys.None, SystemTlsRandomSource.Instance)).ParamName!,
            Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordWriteState.Create(parameters, null!, SystemTlsRandomSource.Instance)).ParamName!,
            Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordWriteState.Create(parameters, Tls12WriteKeys.None, null!)).ParamName!,
            Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordReadState.Create(null!, Tls12WriteKeys.None)).ParamName!,
            Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordReadState.Create(parameters, null!)).ParamName!,
            Assert.ThrowsExactly<ArgumentNullException>(() => Tls12KeyBlock.Partition(null!, [])).ParamName!,
            Assert.ThrowsExactly<ArgumentNullException>(() => TlsPrf.Sha256.Compute([], null!, [], 1)).ParamName!,
        ];

        Diagnostics.Act("refused parameters", string.Join(", ", refused));
        Diagnostics.Assert("calls refused", 7, refused.Count);
    }
}
