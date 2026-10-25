using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that a chain built with no elements and no errors leaves the certificates the server
/// sent as the reported chain (AF-0436), not the chain's own empty list.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationEmptyChainTests
{
    [TestMethod]
    public void Judge_ChainWithNoElementsAndNoErrors_ReportsThePeerCertificates()
    {
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(Insecure: true), matchesSchannelBuild: false, TimeProvider.System);
        ReadOnlyMemory<byte>[] sent = [new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }];
        using var emptyChain = new X509Chain();

        var (observed, _) = verification.Judge(
            SslPolicyErrors.None, emptyChain, "empty-chain.test", [], null, sent);

        Assert.AreEqual(2, observed.Chain.Length);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, observed.Chain[0].ToArray());
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, observed.Chain[1].ToArray());
    }
}
