namespace Curl.Tls;

/// <summary>Key block sizes and partitioning (RFC 5246 section 6.3, RFC 5288, RFC 7905).</summary>
[TestClass]
public sealed class Tls12KeyBlockTests
{
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
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm);

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

        Tls12KeyBlock keys = Tls12KeyBlock.Partition(parameters, keyBlock);

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

        Tls12KeyBlock keys = Tls12KeyBlock.Partition(parameters, keyBlock);

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

        Assert.ThrowsExactly<ArgumentException>(() => Tls12KeyBlock.Partition(parameters, new byte[39]));
    }

    [TestMethod]
    public void TheKeyBlockFromThePrfPartitionsIntoWorkingRecordKeys()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None);
        byte[] master = TlsPrf.Sha256.ComputeExtendedMasterSecret(new byte[32], new byte[32]);
        Tls12KeyBlock keys = Tls12KeyBlock.Partition(parameters, TlsPrf.Sha256.ComputeKeyBlock(master, new byte[32], new byte[32], parameters.KeyBlockLength));
        using Tls12RecordWriteState client = Tls12RecordWriteState.Create(parameters, keys.ClientWrite, SystemTlsRandomSource.Instance);
        using Tls12RecordReadState server = Tls12RecordReadState.Create(parameters, keys.ClientWrite);

        byte[] record = client.Protect(TlsContentType.ApplicationData, "GET / HTTP/1.1\r\n"u8);

        CollectionAssert.AreEqual("GET / HTTP/1.1\r\n"u8.ToArray(), server.Unprotect(TlsContentType.ApplicationData, record.AsSpan(5)).Value);
    }
}
