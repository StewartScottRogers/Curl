using System.Collections;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Libcurl.UnitTests.Misc.DatesAndLists;

[TestClass]
public sealed class CurlSlistTests
{
    [TestMethod]
    public void Append_NullList_StartsANewListHoldingTheText()
    {
        var list = CurlSlist.Append(null, "X-A: 1");

        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("X-A: 1", list[0]);
    }

    [TestMethod]
    public void Append_ExistingList_AddsToItsEndAndAnswersTheSameList()
    {
        var list = CurlSlist.Append(null, "first");

        var answered = CurlSlist.Append(list, "second");

        Assert.AreSame(list, answered);
        CollectionAssert.AreEqual(new[] { "first", "second" }, list.ToArray());
    }

    [TestMethod]
    public void Append_NullText_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlSlist.Append(null, null!));
    }

    [TestMethod]
    public void FreeAll_List_EmptiesIt()
    {
        var list = CurlSlist.Append(CurlSlist.Append(null, "a"), "b");

        CurlSlist.FreeAll(list);

        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void FreeAll_Null_DoesNothing()
    {
        CurlSlist.FreeAll(null);
    }

    [TestMethod]
    public void NonGenericEnumerator_YieldsTheStringsInOrder()
    {
        var list = CurlSlist.Append(CurlSlist.Append(null, "a"), "b");
        var read = new List<object>();

        IEnumerator enumerator = ((IEnumerable)list).GetEnumerator();
        while (enumerator.MoveNext())
        {
            read.Add(enumerator.Current);
        }

        CollectionAssert.AreEqual(new object[] { "a", "b" }, read);
    }

    [TestMethod]
    public void SetOption_Httpheader_CopiesTheListSoLaterAppendsAndFreeAllLeaveTheOption()
    {
        var list = CurlSlist.Append(CurlSlist.Append(null, "X-A: 1"), "Accept:");
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Httpheader, list));
        CurlSlist.Append(list, "X-B: 2");
        CurlSlist.FreeAll(list);

        CollectionAssert.AreEqual(new[] { "X-A: 1", "Accept:" }, options.Http.HttpHeaders.ToArray());
    }

    [TestMethod]
    public void SetOption_Quote_TakesTheListAsAnSlistValue()
    {
        var list = CurlSlist.Append(null, "PWD");
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Quote, list));
        CurlSlist.FreeAll(list);

        CollectionAssert.AreEqual(new[] { "PWD" }, options.Protocols.Ftp.Quote.ToArray());
    }
}
