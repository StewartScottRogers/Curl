using Curl.Core.AltSvc;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="AltSvcTransferCache" />: <see cref="AltSvcTransferCache.SourceAlpnOf" />, the source ALPN an <c>Alt-Svc</c> header is learned
/// under, against curl 8.21.0's <c>Curl_altsvc_parse</c> (BL-947), and the outcomes it hands the HTTP handler (ADR-0409).
/// </summary>
[TestClass]
public sealed class AltSvcTransferCacheTests
{
    [TestMethod]
    [DataRow(3, 0, AltSvcAlpn.H3)]
    [DataRow(2, 0, AltSvcAlpn.H2)]
    [DataRow(1, 1, AltSvcAlpn.H1)]
    [DataRow(1, 0, AltSvcAlpn.H1)]
    public void SourceAlpnOf_ResponseVersion_GivesCurlsSourceAlpn(int major, int minor, AltSvcAlpn expected)
    {
        Assert.AreEqual(expected, AltSvcTransferCache.SourceAlpnOf(new Version(major, minor)));
    }

    /// <summary>
    /// The cache's outcomes reach the HTTP handler as they are, the skipped alternative's reason
    /// included, so it can write curl's <c>-v</c> line for it (ADR-0409).
    /// </summary>
    [TestMethod]
    public async Task StoreFromResponse_AddedThenBadPort_GivesTheAddedAlternativeThenTheSkipReason()
    {
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(string.Empty, null, [], new InMemoryFileSystem(), TimeProvider.System);

        IReadOnlyList<AltSvcHeaderOutcome> outcomes = cache.StoreFromResponse(
            CurlUrl.Parse("https://h.test/"),
            "h2=\":8443\", h2=\"host:\"",
            System.Net.HttpVersion.Version11,
            DateTimeOffset.UnixEpoch);

        Assert.HasCount(2, outcomes);
        Assert.AreEqual(new Curl.Protocol.Abstractions.AltSvcAlternative("h2", "h.test", 8443), outcomes[0].Added);
        Assert.AreEqual(AltSvcSkipReason.UnknownPortNumber, outcomes[1].SkipReason);
    }
}
