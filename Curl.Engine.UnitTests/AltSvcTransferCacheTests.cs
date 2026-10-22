using Curl.Api.Options.Http;
using Curl.Core.FileSystem;
using Curl.Core.AltSvc;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;
using HttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="AltSvcTransferCache" />: <see cref="AltSvcTransferCache.SourceAlpnOf" />, the source ALPN an <c>Alt-Svc</c> header is learned
/// under, against curl 8.21.0's <c>Curl_altsvc_parse</c> (BL-947), and the outcomes it hands the HTTP handler (ADR-0409).
/// </summary>
[TestClass]
public sealed class AltSvcTransferCacheTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(3, 0, AltSvcAlpn.H3)]
    [DataRow(2, 0, AltSvcAlpn.H2)]
    [DataRow(1, 1, AltSvcAlpn.H1)]
    [DataRow(1, 0, AltSvcAlpn.H1)]
    public void SourceAlpnOf_ResponseVersion_GivesCurlsSourceAlpn(int major, int minor, AltSvcAlpn expected)
    {
        Diagnostics.Arrange("response version", $"{major}.{minor}");
        AltSvcAlpn actual = AltSvcTransferCache.SourceAlpnOf(new Version(major, minor));
        Diagnostics.Act("source alpn", actual);
        Diagnostics.Assert("source alpn", expected, actual);
        Assert.AreEqual(expected, AltSvcTransferCache.SourceAlpnOf(new Version(major, minor)));
    }

    /// <summary>
    /// The cache's outcomes reach the HTTP handler as they are, the skipped alternative's reason
    /// included, so it can write curl's <c>-v</c> line for it (ADR-0409).
    /// </summary>
    [TestMethod]
    public async Task StoreFromResponse_AddedThenBadPort_GivesTheAddedAlternativeThenTheSkipReason()
    {
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty }, [], new UnusedFileSystem(), TimeProvider.System);

        IReadOnlyList<AltSvcHeaderOutcome> outcomes = cache.StoreFromResponse(
            CurlUrl.Parse("https://h.test/"),
            "h2=\":8443\", h2=\"host:\"",
            System.Net.HttpVersion.Version11,
            DateTimeOffset.UnixEpoch);

        Diagnostics.Arrange("alt-svc header", "h2=\":8443\", h2=\"host:\"");
        Diagnostics.Act("outcome count", outcomes.Count);
        Diagnostics.Assert("outcome count", 2, outcomes.Count);
        Diagnostics.Assert("second skip reason", AltSvcSkipReason.UnknownPortNumber, outcomes[1].SkipReason);
        Assert.HasCount(2, outcomes);
        Assert.AreEqual(new Curl.Protocol.Abstractions.AltSvcAlternative("h2", "h.test", 8443), outcomes[0].Added);
        Assert.AreEqual(AltSvcSkipReason.UnknownPortNumber, outcomes[1].SkipReason);
    }

    /// <summary>
    /// With no entry for the origin, a connection keeps the version the option group's
    /// <see cref="EasyHttpOptions.HttpVersion" /> asks for, and a <see langword="null" /> file reads nothing.
    /// </summary>
    [TestMethod]
    [DataRow(HttpVersion.None, HttpVersionPreference.Http11, DisplayName = "no version option")]
    [DataRow(HttpVersion.Http11, HttpVersionPreference.Http11, DisplayName = "--http1.1")]
    [DataRow(HttpVersion.Http3, HttpVersionPreference.Http3, DisplayName = "--http3")]
    [DataRow(HttpVersion.Http3Only, HttpVersionPreference.Http3Only, DisplayName = "--http3-only")]
    public async Task ApplyTo_NoEntryForTheOrigin_KeepsTheRequestedVersion(HttpVersion requested, HttpVersionPreference expected)
    {
        Diagnostics.Arrange("http version", requested);
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { HttpVersion = requested }, [], new UnusedFileSystem(), TimeProvider.System);

        HttpVersionPreference actual = cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions()).Version;
        Diagnostics.Act("version", actual);

        Diagnostics.Assert("version", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    /// <summary>
    /// An entry whose alternative is another host is followed: the connection gets a route to that
    /// alternative, which carries the origin's ALPN and the alternative's, host and port (AF-0314).
    /// </summary>
    [TestMethod]
    public async Task ApplyTo_EntryForAnotherHost_RoutesToTheAlternative()
    {
        Diagnostics.Arrange("alt-svc header", "h2=\"other.test:8443\"");
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty }, [], new UnusedFileSystem(), TimeProvider.System);
        cache.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "h2=\"other.test:8443\"", System.Net.HttpVersion.Version11, DateTimeOffset.UtcNow);

        AltSvcRoute? actual = cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions()).AltSvcRoute;
        Diagnostics.Act("route", actual);

        AltSvcRoute expected = new("h1", new Curl.Protocol.Abstractions.AltSvcAlternative("h2", "other.test", 8443));
        Diagnostics.Assert("route", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    /// <summary>
    /// An entry whose alternative is the origin's own host and port dials no alternative: the
    /// connection gets no route, only the alternative's version (AF-0314).
    /// </summary>
    [TestMethod]
    public async Task ApplyTo_EntryForTheSameHostAndPort_GivesNoRoute()
    {
        Diagnostics.Arrange("alt-svc header", "h2=\":443\"");
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty }, [], new UnusedFileSystem(), TimeProvider.System);
        cache.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "h2=\":443\"", System.Net.HttpVersion.Version11, DateTimeOffset.UtcNow);

        HttpRequestOptions actual = cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions { AltSvcRoute = new AltSvcRoute("h1", new Curl.Protocol.Abstractions.AltSvcAlternative("h2", "stale.test", 1)) });
        Diagnostics.Act("route", actual.AltSvcRoute);

        Diagnostics.Assert("route", (AltSvcRoute?)null, actual.AltSvcRoute);
        Assert.IsNull(actual.AltSvcRoute);
    }

    /// <summary>A file system the cache must never touch: an empty <c>--alt-svc</c> name reads and writes nothing.</summary>
    private sealed class UnusedFileSystem : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
