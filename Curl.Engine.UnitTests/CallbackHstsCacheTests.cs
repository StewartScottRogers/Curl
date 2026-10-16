using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options.Http;
using Curl.Core.Hsts;
using Curl.Protocol.Abstractions;
using HstsEntry = Curl.Api.Callbacks.HstsEntry;

namespace Curl.Engine;

/// <summary>Pins <c>CURLOPT_HSTSREADFUNCTION</c> and <c>CURLOPT_HSTSWRITEFUNCTION</c> over an HSTS cache (ADR-0620).</summary>
[TestClass]
public sealed class CallbackHstsCacheTests
{
    [TestMethod]
    public void Load_SeveralEntriesThenDone_AddsEachInOrder()
    {
        HstsTransferPolicy hsts = new(TimeProvider.System);
        HstsReadCallback read = ReadOf(
            new HstsEntry("one.example", false, null),
            new HstsEntry("two.example", true, "20991231 10:20:30"),
            new HstsEntry("three.example", false, string.Empty));

        CurlCode code = CallbackHstsCache.Load(read, hsts);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(
            "one.example \"unlimited\"\n.two.example \"20991231 10:20:30\"\nthree.example \"unlimited\"\n",
            EntryLinesOf(hsts));
        Assert.IsTrue(hsts.TrySwitchToHttps("http://a.two.example/", CurlUrl.Parse("http://a.two.example/"), out _));
    }

    [TestMethod]
    public void Load_Fail_AnswersAbortedByCallback()
    {
        HstsTransferPolicy hsts = new(TimeProvider.System);

        CurlCode code = CallbackHstsCache.Load(ReadOf(new HstsEntry("one.example", false, null), HstsResult.Fail), hsts);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual("one.example \"unlimited\"\n", EntryLinesOf(hsts));
    }

    [TestMethod]
    public void Load_OkWithNoEntry_AnswersBadFunctionArgument()
    {
        HstsReadCallback read = (out HstsEntry? entry) =>
        {
            entry = null;
            return HstsResult.Ok;
        };

        Assert.AreEqual(CurlCode.BadFunctionArgument, CallbackHstsCache.Load(read, new HstsTransferPolicy(TimeProvider.System)));
    }

    [TestMethod]
    public void Load_OkWithAnEmptyName_AnswersBadFunctionArgument()
    {
        HstsReadCallback read = ReadOf(new HstsEntry(string.Empty, false, null));

        Assert.AreEqual(CurlCode.BadFunctionArgument, CallbackHstsCache.Load(read, new HstsTransferPolicy(TimeProvider.System)));
    }

    [TestMethod]
    public void Save_EveryEntry_HandsEachWithItsIndexAndTheTotal()
    {
        HstsTransferPolicy hsts = new(TimeProvider.System);
        hsts.ReadFile("one.example \"unlimited\"\n.two.example \"20991231 10:20:30\"\n");
        List<(HstsEntry Entry, int Index, int Total)> calls = [];

        CallbackHstsCache.Save(
            (entry, index, total) =>
            {
                calls.Add((entry, index, total));
                return HstsResult.Ok;
            },
            hsts);

        CollectionAssert.AreEqual(
            new[] { (new HstsEntry("one.example", false, null), 0, 2), (new HstsEntry("two.example", true, "20991231 10:20:30"), 1, 2) },
            calls);
    }

    [TestMethod]
    [DataRow(HstsResult.Done)]
    [DataRow(HstsResult.Fail)]
    public void Save_AnswerOtherThanOk_StopsAfterThatEntry(HstsResult answer)
    {
        HstsTransferPolicy hsts = new(TimeProvider.System);
        hsts.ReadFile("one.example \"unlimited\"\ntwo.example \"unlimited\"\n");
        int calls = 0;

        CallbackHstsCache.Save(
            (_, _, _) =>
            {
                calls++;
                return answer;
            },
            hsts);

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void Save_EmptyCache_CallsNothing()
    {
        int calls = 0;

        CallbackHstsCache.Save(
            (_, _, _) =>
            {
                calls++;
                return HstsResult.Ok;
            },
            new HstsTransferPolicy(TimeProvider.System));

        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void IsEnabled_EnableOrAFile_IsTrueAndNeitherIsFalse()
    {
        Assert.IsTrue(CallbackHstsCache.IsEnabled(new EasyHttpOptions { HstsControl = HstsControl.Enable | HstsControl.ReadOnlyFile }));
        Assert.IsTrue(CallbackHstsCache.IsEnabled(new EasyHttpOptions { Hsts = string.Empty }));
        Assert.IsFalse(CallbackHstsCache.IsEnabled(new EasyHttpOptions { HstsControl = HstsControl.ReadOnlyFile }));
    }

    /// <summary>A read callback handing over <paramref name="entries" /> one per call, then answering <paramref name="last" />.</summary>
    internal static HstsReadCallback ReadOf(HstsEntry[] entries, HstsResult last)
    {
        int next = 0;
        return (out HstsEntry? entry) =>
        {
            entry = next < entries.Length ? entries[next] : null;
            return next++ < entries.Length ? HstsResult.Ok : last;
        };
    }

    private static HstsReadCallback ReadOf(params HstsEntry[] entries) => ReadOf(entries, HstsResult.Done);

    private static HstsReadCallback ReadOf(HstsEntry entry, HstsResult last) => ReadOf([entry], last);

    /// <summary>The cache's file lines after its header comments.</summary>
    private static string EntryLinesOf(HstsTransferPolicy hsts)
    {
        string text = hsts.FormatFile("\n", long.MaxValue)!;
        return string.Concat(text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[HstsCache.FileHeaderLines.Count..].Select(line => line + "\n"));
    }
}
