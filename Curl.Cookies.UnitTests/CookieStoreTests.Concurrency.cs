using System.Collections.Concurrent;
using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// A <c>-Z</c> run's transfers share one <see cref="CookieStore"/> (ADR-0127), so its members are called
/// from several transfers at once (BL-755).
/// </summary>
public sealed partial class CookieStoreTests
{
    private const int ConcurrentCallers = 100;

    [TestMethod]
    public async Task StoreFromResponse_ManyConcurrentCallers_KeepsEveryCookie()
    {
        CookieStore store = new();
        ConcurrentBag<string> reported = [];
        Diagnostics.Arrange("concurrent callers", ConcurrentCallers);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, ConcurrentCallers),
            (number, cancellationToken) =>
            {
                int stored = store.StoreFromResponse(Loopback, string.Create(CultureInfo.InvariantCulture, $"c{number}=v{number}"), 0, Now, NoTransferEvents.Instance);
                reported.Add(stored.ToString(CultureInfo.InvariantCulture));
                return ValueTask.CompletedTask;
            });

        Diagnostics.ActCookies("stored cookies", store.Cookies);
        Diagnostics.Act("counts reported", string.Join(",", reported.Distinct().Order(StringComparer.Ordinal)));

        string[] expected = [.. Enumerable.Range(0, ConcurrentCallers).Select(number => string.Create(CultureInfo.InvariantCulture, $"c{number}=v{number}")).Order(StringComparer.Ordinal)];
        Diagnostics.AssertTexts("stored name=value, sorted", expected, store.Cookies.Select(cookie => $"{cookie.Name}={cookie.Value}").Order(StringComparer.Ordinal));
        CollectionAssert.AreEqual(expected, store.Cookies.Select(cookie => $"{cookie.Name}={cookie.Value}").Order(StringComparer.Ordinal).ToArray());
        CollectionAssert.AreEqual(expected, store.GetCookieHeader(Loopback, secure: false, Now)!.Split("; ").Order(StringComparer.Ordinal).ToArray());
        Assert.IsTrue(reported.All(stored => stored == "1"));
    }

    [TestMethod]
    public async Task GetCookieHeader_WhileAnotherCallerStores_NeverThrowsAndSeesOnlyWholeCookies()
    {
        CookieStore store = new();
        store.AddCookieString("given=1");
        Diagnostics.Arrange("-b string", "given=1");
        Diagnostics.Arrange("cookies stored while reading", ConcurrentCallers);
        using CancellationTokenSource storingDone = new();

        Task storing = Task.Run(() =>
        {
            for (int number = 0; number < ConcurrentCallers; number++)
            {
                store.StoreFromResponse(Loopback, [string.Create(CultureInfo.InvariantCulture, $"c{number}=v{number}")], Now, NoTransferEvents.Instance);
            }

            storingDone.Cancel();
        });
        Task<int> reading = Task.Run(() =>
        {
            int reads = 0;
            do
            {
                string header = store.GetCookieHeader(Loopback, secure: false, Now)!;
                Assert.IsTrue(header.Split("; ").All(IsWholeCookie), header);
                using StringWriter jar = new(CultureInfo.InvariantCulture);
                store.WriteCookieJar(jar, Now);
                _ = store.Cookies.Count;
                reads++;
            }
            while (!storingDone.IsCancellationRequested);

            return reads;
        });

        await Task.WhenAll(storing, reading);
        Diagnostics.Act("reads while storing", await reading);
        Diagnostics.Act("stored cookie count", store.Cookies.Count);
        Diagnostics.Assert("stored cookie count", ConcurrentCallers, store.Cookies.Count);

        Assert.IsGreaterThan(0, await reading);
        Assert.HasCount(ConcurrentCallers, store.Cookies);
        Assert.HasCount(ConcurrentCallers + 1, store.GetCookieHeader(Loopback, secure: false, Now)!.Split("; "));
    }

    /// <summary>The <c>-b</c> string <c>given=1</c>, or a stored <c>c&lt;n&gt;=v&lt;n&gt;</c> cookie.</summary>
    private static bool IsWholeCookie(string pair)
    {
        int equals = pair.IndexOf('=', StringComparison.Ordinal);
        return pair == "given=1" || (pair[0] == 'c' && pair[(equals + 1)..] == "v" + pair[1..equals]);
    }

    [TestMethod]
    public async Task AddCookieStringAndLoadCookieFile_ManyConcurrentCallers_KeepEveryOne()
    {
        CookieStore store = new();
        Diagnostics.Arrange("concurrent callers, each adding a -b string and loading a one-line file", ConcurrentCallers);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, ConcurrentCallers),
            (number, cancellationToken) =>
            {
                store.AddCookieString(string.Create(CultureInfo.InvariantCulture, $"s{number}=1"));
                using StringReader file = new(string.Create(CultureInfo.InvariantCulture, $"127.0.0.1\tFALSE\t/\tFALSE\t0\tf{number}\t1\n"));
                store.LoadCookieFile(file, discardSessionCookies: false, Now);
                return ValueTask.CompletedTask;
            });

        string[] sent = store.GetCookieHeader(Loopback, secure: false, Now)!.Split("; ");
        Diagnostics.Act("distinct cookies sent", sent.Distinct().Count());
        Diagnostics.Assert("distinct cookies sent", 2 * ConcurrentCallers, sent.Distinct().Count());
        Diagnostics.Assert("stored cookie count", ConcurrentCallers, store.Cookies.Count);
        Assert.HasCount(2 * ConcurrentCallers, sent.Distinct());
        Assert.HasCount(ConcurrentCallers, store.Cookies);
    }
}
