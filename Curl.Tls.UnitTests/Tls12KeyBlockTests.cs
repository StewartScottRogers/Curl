using Curl.Testing;

namespace Curl.Tls;

/// <summary>Key block sizes and partitioning (RFC 5246 section 6.3, RFC 5288, RFC 7905).</summary>
[TestClass]
public sealed class Tls12KeyBlockTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, 20, 16, 16, 104)]
    [DataRow(TlsProtocolVersion.Tls11, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, 20, 16, 0, 72)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes256Cbc, Tls12MacAlgorithm.HmacSha384, 48, 32, 0, 160)]
    [DataRow(TlsProtocolVersion.Tls10, Tls12BulkCipher.TripleDesEdeCbc, Tls12MacAlgorithm.HmacSha1, 20, 24, 8, 104)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Camellia256Cbc, Tls12MacAlgorithm.HmacSha256, 32, 32, 0, 128)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None, 0, 16, 4, 40)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aria256Gcm, Tls12MacAlgorithm.None, 0, 32, 4, 72)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None, 0, 32, 12, 88)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Null, Tls12MacAlgorithm.HmacMd5, 16, 0, 0, 32)]
    public void ParametersSizeTheKeyBlock(
        TlsProtocolVersion version,
        Tls12BulkCipher bulkCipher,
        Tls12MacAlgorithm macAlgorithm,
        int macKeyLength,
        int keyLength,
        int ivLength,
        int keyBlockLength)
    {
        Diagnostics.Arrange("parameters", $"{version}, {bulkCipher}, {macAlgorithm}");

        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm);
        Diagnostics.Act("lengths", $"mac key {parameters.MacKeyLength}, key {parameters.KeyLength}, fixed iv {parameters.FixedIvLength}, key block {parameters.KeyBlockLength}");

        Diagnostics.Assert("lengths", $"{macKeyLength}/{keyLength}/{ivLength}/{keyBlockLength}", $"{parameters.MacKeyLength}/{parameters.KeyLength}/{parameters.FixedIvLength}/{parameters.KeyBlockLength}");
        Assert.AreEqual(macKeyLength, parameters.MacKeyLength);
        Assert.AreEqual(keyLength, parameters.KeyLength);
        Assert.AreEqual(ivLength, parameters.FixedIvLength);
        Assert.AreEqual(keyBlockLength, parameters.KeyBlockLength);
    }

    [TestMethod]
    public void PartitionTakesMacKeysThenKeysThenIvsClientFirst()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls10, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);
        byte[] keyBlock = Enumerable.Range(0, 110).Select(value => (byte)value).ToArray();
        Diagnostics.Arrange("parameters", "Tls10, Aes128Cbc, HmacSha1");
        Diagnostics.Arrange("key block", "110 bytes counting up from 0");

        Tls12KeyBlock keys = Tls12KeyBlock.Partition(parameters, keyBlock);
        Diagnostics.Act("partition", Describe(keys));

        Diagnostics.Diff("client write mac key", keyBlock[0..20], keys.ClientWrite.MacKey);
        Diagnostics.Diff("server write iv", keyBlock[88..104], keys.ServerWrite.Iv);
        CollectionAssert.AreEqual(keyBlock[0..20], keys.ClientWrite.MacKey);
        CollectionAssert.AreEqual(keyBlock[20..40], keys.ServerWrite.MacKey);
        CollectionAssert.AreEqual(keyBlock[40..56], keys.ClientWrite.Key);
        CollectionAssert.AreEqual(keyBlock[56..72], keys.ServerWrite.Key);
        CollectionAssert.AreEqual(keyBlock[72..88], keys.ClientWrite.Iv);
        CollectionAssert.AreEqual(keyBlock[88..104], keys.ServerWrite.Iv);
    }

    [TestMethod]
    public void PartitionOfAGcmSuiteTakesFourByteSalts()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None);
        byte[] keyBlock = Enumerable.Range(0, 40).Select(value => (byte)value).ToArray();
        Diagnostics.Arrange("parameters", "Tls12, Aes128Gcm, no mac");
        Diagnostics.Arrange("key block", "40 bytes counting up from 0");

        Tls12KeyBlock keys = Tls12KeyBlock.Partition(parameters, keyBlock);
        Diagnostics.Act("partition", Describe(keys));

        Diagnostics.Assert("client write iv length", 4, keys.ClientWrite.Iv.Length);
        Assert.IsEmpty(keys.ClientWrite.MacKey);
        CollectionAssert.AreEqual(keyBlock[0..16], keys.ClientWrite.Key);
        CollectionAssert.AreEqual(keyBlock[16..32], keys.ServerWrite.Key);
        CollectionAssert.AreEqual(keyBlock[32..36], keys.ClientWrite.Iv);
        CollectionAssert.AreEqual(keyBlock[36..40], keys.ServerWrite.Iv);
    }

    [TestMethod]
    public void PartitionRefusesAShortKeyBlock()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None);
        Diagnostics.Arrange("key block", $"39 bytes for a suite that needs {parameters.KeyBlockLength}");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Tls12KeyBlock.Partition(parameters, new byte[39]));

        Diagnostics.Act("exception", exception.Message);
        Diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void TheKeyBlockFromThePrfPartitionsIntoWorkingRecordKeys()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None);
        byte[] master = TlsPrf.Sha256.ComputeExtendedMasterSecret(new byte[32], new byte[32]);
        Tls12KeyBlock keys = Tls12KeyBlock.Partition(parameters, TlsPrf.Sha256.ComputeKeyBlock(master, new byte[32], new byte[32], parameters.KeyBlockLength));
        using Tls12RecordWriteState client = Tls12RecordWriteState.Create(parameters, keys.ClientWrite, SystemTlsRandomSource.Instance);
        using Tls12RecordReadState server = Tls12RecordReadState.Create(parameters, keys.ClientWrite);
        Diagnostics.Arrange("parameters", "Tls12, ChaCha20Poly1305; key block from the SHA-256 PRF over zero secrets");

        byte[] record = client.Protect(TlsContentType.ApplicationData, "GET / HTTP/1.1\r\n"u8);
        Diagnostics.Bytes("record", record);

        byte[]? recovered = server.Unprotect(TlsContentType.ApplicationData, record.AsSpan(5)).Value;
        Diagnostics.Act("recovered", recovered is null ? "nothing" : $"{recovered.Length} bytes");
        Diagnostics.Diff("round trip", "GET / HTTP/1.1\r\n"u8, recovered);
        CollectionAssert.AreEqual("GET / HTTP/1.1\r\n"u8.ToArray(), recovered);
    }

    private static string Describe(Tls12KeyBlock keys) =>
        $"client mac key {keys.ClientWrite.MacKey.Length}, server mac key {keys.ServerWrite.MacKey.Length}, client key {keys.ClientWrite.Key.Length}, server key {keys.ServerWrite.Key.Length}, client iv {keys.ClientWrite.Iv.Length}, server iv {keys.ServerWrite.Iv.Length} bytes";
}
