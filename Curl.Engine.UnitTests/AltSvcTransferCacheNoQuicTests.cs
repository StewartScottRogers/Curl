using Curl.Api.Options.Http;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;
using HttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="AltSvcTransferCache" /> on a build that is not a QUIC build: with no version option an
/// <c>h3</c> entry is ignored off Windows, as curl 8.18.0's OpenSSL build without QUIC ignores it (BL-5099, STD-054).
/// </summary>
[TestClass]
public sealed class AltSvcTransferCacheNoQuicTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(false, "H1,H2")]
    [DataRow(true, "H1,H2,H3")]
    public void LookupOf_NoVersionOptionOffWindows_AllowsH3OnlyOnAQuicBuild(bool actsAsQuicBuild, string expected)
    {
        Diagnostics.Arrange("acts as QUIC build", actsAsQuicBuild);

        string actual = string.Join(",", AltSvcTransferCache.LookupOf(HttpVersion.None, isWindows: false, actsAsQuicBuild).Allowed.Order());
        Diagnostics.Act("allowed", actual);

        Diagnostics.Assert("allowed", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void LookupOf_Http3OptionOnANonQuicBuild_StillAllowsH3()
    {
        string actual = string.Join(",", AltSvcTransferCache.LookupOf(HttpVersion.Http3, isWindows: false).Allowed.Order());
        Diagnostics.Act("allowed", actual);

        Diagnostics.Assert("allowed", "H1,H2,H3", actual);
        Assert.AreEqual("H1,H2,H3", actual);
    }

    /// <summary>The repro: an <c>h3</c> entry for another port leaves the transfer on the origin over TCP, with no route.</summary>
    [TestMethod]
    public async Task ApplyTo_H3EntryForAnotherPortOnANonQuicBuild_StaysOnTheOriginOverTcp()
    {
        Diagnostics.Arrange("alt-svc header", "h3=\"localhost:25998\"");
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty }, [], new NoFileSystem(), TimeProvider.System, onWindows: false);
        cache.StoreFromResponse(CurlUrl.Parse("https://localhost:25443/"), "h3=\"localhost:25998\"", System.Net.HttpVersion.Version11, DateTimeOffset.UtcNow);

        HttpRequestOptions actual = cache.ApplyTo(CurlUrl.Parse("https://localhost:25443/"), new HttpRequestOptions());
        Diagnostics.Act("route", actual.AltSvcRoute);

        Diagnostics.Assert("route", (AltSvcRoute?)null, actual.AltSvcRoute);
        Assert.IsNull(actual.AltSvcRoute);
        Assert.AreEqual(HttpVersionPreference.Http11, actual.Version);
    }

    [TestMethod]
    public async Task ApplyTo_H2EntryForAnotherPortOnANonQuicBuild_StillRoutesToTheAlternative()
    {
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty }, [], new NoFileSystem(), TimeProvider.System, onWindows: false);
        cache.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "h2=\"other.test:8443\"", System.Net.HttpVersion.Version11, DateTimeOffset.UtcNow);

        AltSvcRoute? actual = cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions()).AltSvcRoute;
        Diagnostics.Act("route", actual);

        Assert.IsNotNull(actual);
        Assert.AreEqual(8443, actual.Alternative.Port);
    }

    [TestMethod]
    public async Task ApplyTo_H3EntryForAnotherPortOnAQuicBuild_RoutesToTheAlternative()
    {
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty }, [], new NoFileSystem(), TimeProvider.System, onWindows: false, actsAsQuicBuild: true);
        cache.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "h3=\"other.test:8443\"", System.Net.HttpVersion.Version11, DateTimeOffset.UtcNow);

        AltSvcRoute? actual = cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions()).AltSvcRoute;
        Diagnostics.Act("route", actual);

        Assert.IsNotNull(actual);
        Assert.AreEqual(8443, actual.Alternative.Port);
    }

    /// <summary>A file system the cache must never touch: an empty <c>--alt-svc</c> name reads and writes nothing.</summary>
    private sealed class NoFileSystem : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
