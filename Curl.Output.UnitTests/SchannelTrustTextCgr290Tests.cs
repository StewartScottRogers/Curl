using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>CGR-290: curl 8.22.0's Schannel build words a shared credential handle.</summary>
[TestClass]
public sealed class SchannelTrustTextCgr290Tests
{
    [TestMethod]
    public void Lines_ReusedCredentialHandle_AreCurlsReuseAndRefcountLines()
    {
        var lines = SchannelTrustText.Lines(new TlsTrustEvent { VerifiesPeer = true, ReusesCredentialHandle = true });

        CollectionAssert.AreEqual(
            new[] { "schannel: reusing existing credential handle", "schannel: incremented credential handle refcount = 2" },
            lines.ToArray());
    }
}
