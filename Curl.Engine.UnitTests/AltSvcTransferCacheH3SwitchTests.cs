using Curl.Api.Options.Http;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;
using HttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Engine;

/// <summary>
/// Pins when a stored <c>h3</c> alt-svc entry switches a transfer to HTTP/3 (BL-5549, ADR-1309): with no
/// version option, <c>--http3</c> and <c>--http3-only</c>, but not with <c>--http1.1</c> or <c>--http2</c>.
/// </summary>
[TestClass]
public sealed class AltSvcTransferCacheH3SwitchTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    /// <summary>Windows (the Schannel build, measured, ADR-1313): with no version option an <c>h2</c> or <c>h3</c> entry leaves the transfer on the origin.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("h3=\":8443\"")]
    [DataRow("h2=\":8443\"")]
    public async Task ApplyTo_NoVersionOptionOnWindows_IgnoresH2AndH3Entries(string header)
    {
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty }, [], new NoFileSystem(), TimeProvider.System);
        cache.StoreFromResponse(CurlUrl.Parse("https://h.test/"), header, System.Net.HttpVersion.Version11, DateTimeOffset.UtcNow);

        HttpRequestOptions actual = cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions());
        Diagnostics.Act("route", actual.AltSvcRoute);

        Assert.IsNull(actual.AltSvcRoute);
    }

    /// <summary>Other platforms (the OpenSSL build): with no version option an <c>h2</c> entry for another port is used.</summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ApplyTo_NoVersionOptionOffWindows_UsesAnH2Entry()
    {
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty }, [], new NoFileSystem(), TimeProvider.System);
        cache.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "h2=\":8443\"", System.Net.HttpVersion.Version11, DateTimeOffset.UtcNow);

        Assert.IsNotNull(cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions()).AltSvcRoute);
    }

    [TestMethod]
    [DataRow(false, "H1,H2,H3")]
    [DataRow(true, "H1")]
    public void LookupOf_NoVersionOption_AllowsH1AloneOnWindowsAndEveryVersionElsewhere(bool isWindows, string expected)
    {
        string actual = string.Join(",", AltSvcTransferCache.LookupOf(HttpVersion.None, isWindows).Allowed.Order());
        Diagnostics.Act("allowed", actual);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(HttpVersion.None, true, true, DisplayName = "no option, learned over h2")]
    [DataRow(HttpVersion.None, false, true, DisplayName = "no option, learned over h1")]
    [DataRow(HttpVersion.Http3, false, true, DisplayName = "--http3 finds it")]
    [DataRow(HttpVersion.Http2, true, false, DisplayName = "--http2 ignores it")]
    [DataRow(HttpVersion.Http11, false, false, DisplayName = "--http1.1 ignores it")]
    public async Task ApplyTo_StoredH3EntryForAnotherPort_SwitchesToHttp3AlonePerTheVersionOption(HttpVersion option, bool learnedOverH2, bool switches)
    {
        Diagnostics.Arrange("option and learned over", $"{option} {learnedOverH2}");
        AltSvcTransferCache cache = await AltSvcTransferCache.OpenAsync(new EasyHttpOptions { AltSvc = string.Empty, HttpVersion = option }, [], new NoFileSystem(), TimeProvider.System, onWindows: false);
        cache.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "h3=\":8443\"", learnedOverH2 ? System.Net.HttpVersion.Version20 : System.Net.HttpVersion.Version11, DateTimeOffset.UtcNow);

        HttpRequestOptions actual = cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions());
        Diagnostics.Act("route", actual.AltSvcRoute);

        Assert.AreEqual(switches, actual.AltSvcRoute is not null);
        Assert.AreEqual(switches, actual.Version == HttpVersionPreference.Http3Only);
    }

    /// <summary>A file system the cache must never touch: an empty <c>--alt-svc</c> name reads and writes nothing.</summary>
    private sealed class NoFileSystem : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
