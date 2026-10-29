using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Record;

[TestClass]
public sealed class DtlsAes128GcmKeyBlockTests
{
    [TestMethod]
    public void Derive_Rfc5288Section3_DividesTheKeyExpansionIntoKeysThenWriteIvs()
    {
        byte[] masterSecret = DtlsAes128GcmRecordUnprotectorTests.MasterSecret;
        byte[] clientRandom = DtlsAes128GcmRecordUnprotectorTests.ClientRandom;
        byte[] serverRandom = DtlsAes128GcmRecordUnprotectorTests.ServerRandom;
        byte[] keyBlock = TlsPrf.Sha256.Compute(masterSecret, "key expansion", [.. serverRandom, .. clientRandom], 40);

        Tls12KeyBlock keys = DtlsAes128GcmKeyBlock.Derive(masterSecret, clientRandom, serverRandom);

        CollectionAssert.AreEqual(keyBlock[..16], keys.ClientWrite.Key);
        CollectionAssert.AreEqual(keyBlock[16..32], keys.ServerWrite.Key);
        CollectionAssert.AreEqual(keyBlock[32..36], keys.ClientWrite.Iv);
        CollectionAssert.AreEqual(keyBlock[36..40], keys.ServerWrite.Iv);
        Assert.IsEmpty(keys.ClientWrite.MacKey);
    }
}
