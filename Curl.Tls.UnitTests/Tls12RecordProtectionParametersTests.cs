namespace Curl.Tls;

/// <summary>The combinations and key lengths the record states refuse.</summary>
[TestClass]
public sealed class Tls12RecordProtectionParametersTests
{
    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls11, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None)]
    [DataRow(TlsProtocolVersion.Tls10, Tls12BulkCipher.ChaCha20Poly1305, Tls12MacAlgorithm.None)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.HmacSha256)]
    [DataRow(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.None)]
    public void ACombinationNoSuiteNamesIsRefused(TlsProtocolVersion version, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm)
    {
        Tls12RecordProtectionParameters parameters = new(version, bulkCipher, macAlgorithm);
        Tls12WriteKeys keys = Tls12Records.RandomKeys(parameters);

        Assert.ThrowsExactly<ArgumentException>(() => Tls12RecordWriteState.Create(parameters, keys, SystemTlsRandomSource.Instance));
        Assert.ThrowsExactly<ArgumentException>(() => Tls12RecordReadState.Create(parameters, keys));
    }

    [TestMethod]
    [DataRow(19, 16, 0)]
    [DataRow(20, 15, 0)]
    [DataRow(20, 16, 16)]
    public void AKeyOfTheWrongLengthIsRefused(int macKeyLength, int keyLength, int ivLength)
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1);

        Assert.ThrowsExactly<ArgumentException>(() => Tls12RecordReadState.Create(parameters, new Tls12WriteKeys(new byte[macKeyLength], new byte[keyLength], new byte[ivLength])));
    }

    [TestMethod]
    public void NullArgumentsAreRefused()
    {
        Tls12RecordProtectionParameters parameters = new(TlsProtocolVersion.Tls12, Tls12BulkCipher.Null, Tls12MacAlgorithm.None);

        Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordWriteState.Create(null!, Tls12WriteKeys.None, SystemTlsRandomSource.Instance));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordWriteState.Create(parameters, null!, SystemTlsRandomSource.Instance));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordWriteState.Create(parameters, Tls12WriteKeys.None, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordReadState.Create(null!, Tls12WriteKeys.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tls12RecordReadState.Create(parameters, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tls12KeyBlock.Partition(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsPrf.Sha256.Compute([], null!, [], 1));
    }
}
