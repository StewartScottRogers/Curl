using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the <see cref="ICookieStore" /> seam for <c>-b name=value</c> strings on a redirect:
/// <see cref="HttpRequestOptions.SendsCookieStrings" /> is on by default, and a store that adds no
/// strings of its own gives its full header for <see cref="ICookieStore.GetStoredCookieHeader" />
/// (BL-1846).
/// </summary>
[TestClass]
public sealed class ICookieStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SendsCookieStrings_NothingSet_IsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "new HttpRequestOptions()");

        bool sends = new HttpRequestOptions().SendsCookieStrings;

        diagnostics.Act("sends cookie strings", sends);
        diagnostics.Assert("sends cookie strings", true, sends);
        Assert.IsTrue(sends);
    }

    [TestMethod]
    public void GetStoredCookieHeader_StoreWithoutOwnStrings_GivesTheCookieHeader()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ICookieStore store = new FixedCookieStore();
        diagnostics.Arrange("store cookie header", "j=k");

        string? header = store.GetStoredCookieHeader(CurlUrl.Parse("http://h.test/"), false, DateTimeOffset.UnixEpoch, NoTransferEvents.Instance);

        diagnostics.Act("stored cookie header", header);
        diagnostics.Assert("stored cookie header", "j=k", header);
        Assert.AreEqual("j=k", header);
    }

    private sealed class FixedCookieStore : ICookieStore
    {
        public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events) => "j=k";

        public int StoreFromResponse(CurlUrl url, string setCookieHeader, int storedFromResponse, DateTimeOffset now, ITransferEvents events) =>
            storedFromResponse;
    }
}
