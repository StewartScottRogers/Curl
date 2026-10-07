using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void EncodeAndDecodeRoundTrip()
    {
        CompressedCertificate message = new(CertificateCompressionAlgorithm.Zstd, 0x012345, [1, 2, 3]);
        Diagnostics.Arrange("message", "algorithm zstd (0x0003), uncompressed length 0x012345, compressed 010203");

        byte[] encoded = message.Encode();
        TlsDecodeResult<CompressedCertificate> decoded = CompressedCertificate.Decode(HandshakeMessageReader.Read(encoded).Message!.Body);

        Diagnostics.Bytes("encoded", encoded);
        Diagnostics.Act("decoded", $"algorithm 0x{decoded.Value.Algorithm:x4}, uncompressed length 0x{decoded.Value.UncompressedLength:x6}, compressed {Convert.ToHexStringLower(decoded.Value.CompressedCertificateMessage)}");
        Diagnostics.Diff("encoded", Convert.FromHexString("1900000b" + "0003" + "012345" + "000003" + "010203"), encoded);
        CollectionAssert.AreEqual(Convert.FromHexString("1900000b" + "0003" + "012345" + "000003" + "010203"), encoded);
        Assert.AreEqual(CertificateCompressionAlgorithm.Zstd, decoded.Value.Algorithm);
        Assert.AreEqual(0x012345, decoded.Value.UncompressedLength);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, decoded.Value.CompressedCertificateMessage);
    }

    [TestMethod]
    public void ATruncatedBodyIsADecodeError()
    {
        Diagnostics.Arrange("body", "0001000010000005");

        TlsAlertDescription? alert = CompressedCertificate.Decode(Convert.FromHexString("0001000010000005")).Alert;

        Diagnostics.Act("alert", Describe(alert));
        Diagnostics.Assert("alert", Describe(TlsAlertDescription.DecodeError), Describe(alert));
        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib)]
    [DataRow(CertificateCompressionAlgorithm.Brotli)]
    [DataRow(CertificateCompressionAlgorithm.Zstd)]
    public void EachAlgorithmDecompressesToTheOriginalBody(int algorithm)
    {
        CompressedCertificate message = Wrap(algorithm);

        byte[]? body = message.Decompress(AllAlgorithms);

        WriteDecompressed(body);
        Diagnostics.Diff("decompressed body", CertificateBody, body ?? []);
        CollectionAssert.AreEqual(CertificateBody, body);
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
        CompressedCertificate message = Wrap(algorithm);
        Diagnostics.Arrange("claimed uncompressed length", CertificateBody.Length + error);

        byte[]? body = (message with { UncompressedLength = CertificateBody.Length + error }).Decompress(AllAlgorithms);

        AssertNoBody(body);
        Assert.IsNull(body);
    }

    [TestMethod]
    public void AnAlgorithmNotOfferedYieldsNoBody()
    {
        CompressedCertificate message = Wrap(CertificateCompressionAlgorithm.Brotli);
        Diagnostics.Arrange("offered algorithms", "zlib, zstd");

        byte[]? body = message.Decompress([CertificateCompressionAlgorithm.Zlib, CertificateCompressionAlgorithm.Zstd]);

        AssertNoBody(body);
        Assert.IsNull(body);
    }

    [TestMethod]
    public void AnAlgorithmTheClientCannotDecompressYieldsNoBodyEvenWhenListed()
    {
        CompressedCertificate message = new(4, 3, [1, 2, 3]);
        Diagnostics.Arrange("message", "algorithm 0x0004, uncompressed length 3, compressed 010203; offered 0x0004");

        byte[]? body = message.Decompress([4]);

        AssertNoBody(body);
        Assert.IsNull(body);
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib, "789cffffffffffff")]
    [DataRow(CertificateCompressionAlgorithm.Zlib, "789c")]
    [DataRow(CertificateCompressionAlgorithm.Brotli, "ffffffffffffffff")]
    [DataRow(CertificateCompressionAlgorithm.Zstd, "00112233445566778899")]
    public void CorruptDataYieldsNoBody(int algorithm, string compressedHex)
    {
        CompressedCertificate message = new((ushort)algorithm, 16, Convert.FromHexString(compressedHex));
        Diagnostics.Arrange("message", $"algorithm 0x{algorithm:x4}, uncompressed length 16, compressed {compressedHex}");

        byte[]? body = message.Decompress(AllAlgorithms);

        AssertNoBody(body);
        Assert.IsNull(body);
    }

    [TestMethod]
    public void DecompressRejectsANullAlgorithmList()
    {
        CompressedCertificate message = new(CertificateCompressionAlgorithm.Zlib, 3, [1, 2, 3]);
        Diagnostics.Arrange("offered algorithms", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => message.Decompress(null!));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow((ushort)0, false)]
    [DataRow(CertificateCompressionAlgorithm.Zlib, true)]
    [DataRow(CertificateCompressionAlgorithm.Brotli, true)]
    [DataRow(CertificateCompressionAlgorithm.Zstd, true)]
    [DataRow((ushort)4, false)]
    public void CanDecompressNamesTheThreeAlgorithms(int algorithm, bool expected)
    {
        Diagnostics.Arrange("algorithm", $"0x{algorithm:x4}");

        bool canDecompress = CertificateCompressionAlgorithm.CanDecompress((ushort)algorithm);

        Diagnostics.Act("can decompress", canDecompress);
        Diagnostics.Assert("can decompress", expected, canDecompress);
        Assert.AreEqual(expected, canDecompress);
    }

    private static string Describe(TlsAlertDescription? alert) =>
        alert is { } description ? $"{description} ({(byte)description})" : "none";

    /// <summary>Compresses the 3000-byte test body with <paramref name="algorithm"/>, writing what it built.</summary>
    private CompressedCertificate Wrap(int algorithm)
    {
        CompressedCertificate message = TestCertificateCompressor.Wrap((ushort)algorithm, CertificateBody);
        Diagnostics.Arrange("algorithm", $"0x{algorithm:x4}");
        Diagnostics.Arrange("uncompressed length", CertificateBody.Length);
        Diagnostics.Bytes("compressed", message.CompressedCertificateMessage);
        return message;
    }

    private void WriteDecompressed(byte[]? body) =>
        Diagnostics.Act("decompressed body", body is null ? "none" : $"{body.Length} bytes");

    private void AssertNoBody(byte[]? body)
    {
        WriteDecompressed(body);
        Diagnostics.Assert("decompressed body", "none", body is null ? "none" : $"{body.Length} bytes");
    }
}
