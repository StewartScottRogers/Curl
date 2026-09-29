namespace Curl.Tls;

/// <summary>The ClientHello's extension order, its fixed extensions, and the padding rule at each edge of its 256-to-511-byte window.</summary>
[TestClass]
public sealed class Tls13ClientHelloBuilderTests
{
    private static readonly byte[] Random = new byte[32];

    [TestMethod]
    [DataRow(255, -1)]
    [DataRow(256, 252)]
    [DataRow(507, 1)]
    [DataRow(508, 1)]
    [DataRow(511, 1)]
    [DataRow(512, -1)]
    public void PaddingBringsAHelloOf256To511BytesUpTo512(int unpaddedLength, int expectedPadding)
    {
        ClientHello hello = HelloOfLength(unpaddedLength, withPadding: true);

        TlsExtension? padding = hello.Extensions.FirstOrDefault(extension => extension.Type == TlsExtensionType.Padding);
        Assert.AreEqual(expectedPadding, padding?.Data.Length ?? -1);
    }

    [TestMethod]
    public void AFixedExtensionNotInTheOrderGoesAfterTheListedOnes()
    {
        TlsExtension quic = new(TlsExtensionType.QuicTransportParameters, [1, 2]);
        Tls13ClientSettings settings = new() { ExtensionOrder = [TlsExtensionType.SupportedVersions], FixedExtensions = [quic] };

        ClientHello hello = new Tls13ClientHelloBuilder(settings, Random, []).Build([], null);

        CollectionAssert.AreEqual(new[] { TlsExtensionType.SupportedVersions, TlsExtensionType.QuicTransportParameters }, hello.Extensions.Select(extension => extension.Type).ToArray());
    }

    [TestMethod]
    public void AListedExtensionWithNothingToSendIsLeftOut()
    {
        Tls13ClientSettings settings = new()
        {
            ExtensionOrder = [TlsExtensionType.ServerName, TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsExtensionType.Cookie, TlsExtensionType.EarlyData, TlsExtensionType.KeyShare],
        };

        ClientHello hello = new Tls13ClientHelloBuilder(settings, Random, []).Build([], null);

        Assert.AreEqual(TlsExtensionType.KeyShare, hello.Extensions.Single().Type);
    }

    private static ClientHello HelloOfLength(int length, bool withPadding)
    {
        int filler = length - Build(0, withPadding: false).Encode().Length;
        ClientHello hello = Build(filler, withPadding);
        int unpadded = Build(filler, withPadding: false).Encode().Length;
        Assert.AreEqual(length, unpadded);
        return hello;
    }

    private static ClientHello Build(int fillerLength, bool withPadding)
    {
        Tls13ClientSettings settings = new()
        {
            ExtensionOrder = withPadding
                ? [TlsExtensionType.SupportedVersions, TlsExtensionType.RecordSizeLimit, TlsExtensionType.Padding]
                : [TlsExtensionType.SupportedVersions, TlsExtensionType.RecordSizeLimit],
            FixedExtensions = [new TlsExtension(TlsExtensionType.RecordSizeLimit, new byte[fillerLength])],
        };
        return new Tls13ClientHelloBuilder(settings, Random, []).Build([], null);
    }
}
