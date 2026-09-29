namespace Curl.Tls;

/// <summary>
/// The CompressedCertificate codec and its decompression (RFC 8879 section 4): each
/// algorithm round-trips a body, and an algorithm not offered, a wrong
/// <c>uncompressed_length</c> or corrupt data yields no body.
/// </summary>
[TestClass]
public sealed class CompressedCertificateTests
{
    private static readonly ushort[] AllAlgorithms =
        [CertificateCompressionAlgorithm.Zlib, CertificateCompressionAlgorithm.Brotli, CertificateCompressionAlgorithm.Zstd];

    private static readonly byte[] CertificateBody = [.. Enumerable.Range(0, 3000).Select(index => (byte)(index % 17))];

    [TestMethod]
    public void EncodeAndDecodeRoundTrip()
    {
        CompressedCertificate message = new(CertificateCompressionAlgorithm.Zstd, 0x012345, [1, 2, 3]);

        byte[] encoded = message.Encode();
        TlsDecodeResult<CompressedCertificate> decoded = CompressedCertificate.Decode(HandshakeMessageReader.Read(encoded).Message!.Body);

        CollectionAssert.AreEqual(Convert.FromHexString("1900000b" + "0003" + "012345" + "000003" + "010203"), encoded);
        Assert.AreEqual(CertificateCompressionAlgorithm.Zstd, decoded.Value.Algorithm);
        Assert.AreEqual(0x012345, decoded.Value.UncompressedLength);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, decoded.Value.CompressedCertificateMessage);
    }

    [TestMethod]
    public void ATruncatedBodyIsADecodeError()
    {
        Assert.AreEqual(TlsAlertDescription.DecodeError, CompressedCertificate.Decode(Convert.FromHexString("0001000010000005")).Alert);
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib)]
    [DataRow(CertificateCompressionAlgorithm.Brotli)]
    [DataRow(CertificateCompressionAlgorithm.Zstd)]
    public void EachAlgorithmDecompressesToTheOriginalBody(int algorithm)
    {
        CompressedCertificate message = TestCertificateCompressor.Wrap((ushort)algorithm, CertificateBody);

        CollectionAssert.AreEqual(CertificateBody, message.Decompress(AllAlgorithms));
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib, 1)]
    [DataRow(CertificateCompressionAlgorithm.Zlib, -1)]
    [DataRow(CertificateCompressionAlgorithm.Brotli, 1)]
    [DataRow(CertificateCompressionAlgorithm.Brotli, -1)]
    [DataRow(CertificateCompressionAlgorithm.Zstd, 1)]
    [DataRow(CertificateCompressionAlgorithm.Zstd, -1)]
    public void AWrongUncompressedLengthYieldsNoBody(int algorithm, int error)
    {
        CompressedCertificate message = TestCertificateCompressor.Wrap((ushort)algorithm, CertificateBody);

        Assert.IsNull((message with { UncompressedLength = CertificateBody.Length + error }).Decompress(AllAlgorithms));
    }

    [TestMethod]
    public void AnAlgorithmNotOfferedYieldsNoBody()
    {
        CompressedCertificate message = TestCertificateCompressor.Wrap(CertificateCompressionAlgorithm.Brotli, CertificateBody);

        Assert.IsNull(message.Decompress([CertificateCompressionAlgorithm.Zlib, CertificateCompressionAlgorithm.Zstd]));
    }

    [TestMethod]
    public void AnAlgorithmTheClientCannotDecompressYieldsNoBodyEvenWhenListed()
    {
        CompressedCertificate message = new(4, 3, [1, 2, 3]);

        Assert.IsNull(message.Decompress([4]));
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib, "789cffffffffffff")]
    [DataRow(CertificateCompressionAlgorithm.Zlib, "789c")]
    [DataRow(CertificateCompressionAlgorithm.Brotli, "ffffffffffffffff")]
    [DataRow(CertificateCompressionAlgorithm.Zstd, "00112233445566778899")]
    public void CorruptDataYieldsNoBody(int algorithm, string compressedHex)
    {
        CompressedCertificate message = new((ushort)algorithm, 16, Convert.FromHexString(compressedHex));

        Assert.IsNull(message.Decompress(AllAlgorithms));
    }

    [TestMethod]
    public void DecompressRejectsANullAlgorithmList()
    {
        CompressedCertificate message = new(CertificateCompressionAlgorithm.Zlib, 3, [1, 2, 3]);

        Assert.ThrowsExactly<ArgumentNullException>(() => message.Decompress(null!));
    }

    [TestMethod]
    [DataRow((ushort)0, false)]
    [DataRow(CertificateCompressionAlgorithm.Zlib, true)]
    [DataRow(CertificateCompressionAlgorithm.Brotli, true)]
    [DataRow(CertificateCompressionAlgorithm.Zstd, true)]
    [DataRow((ushort)4, false)]
    public void CanDecompressNamesTheThreeAlgorithms(int algorithm, bool expected)
    {
        Assert.AreEqual(expected, CertificateCompressionAlgorithm.CanDecompress((ushort)algorithm));
    }
}
