using System.Collections;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Share;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="ShareCookieListLines" />: <c>CURLINFO_COOKIELIST</c> over a share handle's cookies is read under one
/// cookie lock pair the first time the list is read, and every later read answers the same lines without locking again.
/// </summary>
[TestClass]
public sealed class ShareCookieListLinesTests
{
    [TestMethod]
    public void Read_EveryWay_ListsTheSharedCookiesUnderOneCookieLockPair()
    {
        ManagedShareHandle share = new(TimeProvider.System);
        share.Share(CurlLockData.Cookie);
        List<string> calls = [];
        share.SetLockCallback((_, data, _) => calls.Add($"lock {data}"));
        share.SetUnlockCallback((_, data) => calls.Add($"unlock {data}"));
        using StringReader cookie = new(".example.com\tTRUE\t/\tFALSE\t4102444800\tname\tvalue\n");
        share.Cookies.LoadCookieFile(cookie, discardSessionCookies: false, DateTimeOffset.UnixEpoch, NoTransferEvents.Instance);
        ShareCookieListLines lines = new(share, new UnusedTransfer());

        int count = lines.Count;
        string first = lines[0];
        List<object?> untyped = [];
        foreach (object? line in (IEnumerable)lines)
        {
            untyped.Add(line);
        }

        Assert.AreEqual(1, count);
        Assert.AreEqual(".example.com\tTRUE\t/\tFALSE\t4102444800\tname\tvalue", first);
        CollectionAssert.AreEqual(new object?[] { first }, untyped);
        CollectionAssert.AreEqual(new[] { "lock Cookie", "unlock Cookie" }, calls);
    }

    /// <summary>A transfer the share handle only names to its callbacks.</summary>
    private sealed class UnusedTransfer : IEasyTransfer
    {
        public Api.Options.EasyOptions Options { get; set; } = new();

        public Api.Info.TransferInfo Info => throw new NotSupportedException();

        public string ErrorText => throw new NotSupportedException();

        public Api.EngineContracts.Headers.ITransferHeaders Headers => throw new NotSupportedException();

        public Api.EngineContracts.WebSocket.IEasyWebSocket WebSocket => throw new NotSupportedException();

        public Api.EngineContracts.ConnectOnly.IConnectOnlyConnection Connection => throw new NotSupportedException();

        public ValueTask<Api.Codes.CurlCode> PerformAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public Api.Codes.CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<Api.Codes.CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
