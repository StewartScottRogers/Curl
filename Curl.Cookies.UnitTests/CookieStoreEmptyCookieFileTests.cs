using Curl.Cookies.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins what <see cref="CookieStore.LoadCookieFileAsync(IFileSystem, string, bool, DateTimeOffset, ITransferEvents, CancellationToken)"/>
/// does with an empty path (BL-3177). Measured on curl 8.22.0 (mingw, Schannel) on 2026-10-22 with
/// <c>Record-CurlExchange.ps1</c>: <c>curl -v -b '' http://127.0.0.1:&lt;port&gt;/</c> wrote no cookie file warning,
/// and <c>curl -v -L -b '' -c - </c> against a 302 carrying <c>Set-Cookie: a=b</c> sent <c>Cookie: a=b</c> on the
/// redirect and wrote the cookie to the jar: an empty <c>-b</c> only turns the cookie engine on.
/// </summary>
[TestClass]
public sealed class CookieStoreEmptyCookieFileTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_790_460_351);

    private static readonly CurlUrl Loopback = CurlUrl.Parse("http://127.0.0.1/");

    [TestMethod]
    public async Task LoadCookieFileAsync_EmptyPath_OpensNothingAndReportsNothing()
    {
        RecordingTransferEvents events = new();
        OpenRecordingFileSystem fileSystem = new();
        CookieStore store = new();

        bool loaded = await store.LoadCookieFileAsync(fileSystem, string.Empty, discardSessionCookies: false, Now, events, CancellationToken.None);

        Assert.IsTrue(loaded);
        Assert.AreEqual(0, fileSystem.OpenCount);
        Assert.IsEmpty(events.Info);
        Assert.IsEmpty(store.Cookies);
    }

    [TestMethod]
    public async Task LoadCookieFileAsync_EmptyPath_LeavesTheStoreTakingCookies()
    {
        CookieStore store = new();

        await store.LoadCookieFileAsync(new OpenRecordingFileSystem(), string.Empty, discardSessionCookies: false, Now, new RecordingTransferEvents(), CancellationToken.None);
        store.StoreFromResponse(Loopback, ["a=b"], Now, NoTransferEvents.Instance);

        Assert.AreEqual("a=b", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    /// <summary>A file system with no files that counts every open.</summary>
    private sealed class OpenRecordingFileSystem : IFileSystem
    {
        public int OpenCount { get; private set; }

        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
        {
            OpenCount++;
            return ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.NotFound));
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken)
        {
            OpenCount++;
            return ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.AccessDenied));
        }
    }
}
