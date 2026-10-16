using Libcurl.Options;

namespace Libcurl.UnitTests.Options;

[TestClass]
public sealed class CurlOptionsTests
{
    [TestMethod]
    public void All_ListsEveryRowOfCurl8_22_0sEasyOptionsTable()
    {
        Assert.HasCount(327, CurlOptions.All);
        Assert.AreEqual(15, CurlOptions.All.Count(row => row.IsAlias));
        Assert.AreEqual(312, CurlOptions.All.Where(row => !row.IsAlias).Select(row => row.Id).Distinct().Count());
    }

    [TestMethod]
    public void All_IsInUpstreamsAlphabeticalOrderWithUniqueNames()
    {
        var names = CurlOptions.All.Select(row => row.Name).ToArray();

        CollectionAssert.AreEqual(names.Order(StringComparer.Ordinal).ToArray(), names);
        CollectionAssert.AllItemsAreUnique(names);
    }

    [TestMethod]
    [DataRow("URL", CurlOption.Url, 10002, CurlOptionType.String)]
    [DataRow("PORT", CurlOption.Port, 3, CurlOptionType.Long)]
    [DataRow("HTTP_VERSION", CurlOption.HttpVersion, 84, CurlOptionType.Values)]
    [DataRow("POSTFIELDSIZE_LARGE", CurlOption.PostfieldsizeLarge, 30120, CurlOptionType.OffT)]
    [DataRow("ERRORBUFFER", CurlOption.Errorbuffer, 10010, CurlOptionType.Object)]
    [DataRow("HTTPHEADER", CurlOption.Httpheader, 10023, CurlOptionType.SList)]
    [DataRow("WRITEDATA", CurlOption.Writedata, 10001, CurlOptionType.CallbackPointer)]
    [DataRow("SSLCERT_BLOB", CurlOption.SslcertBlob, 40291, CurlOptionType.Blob)]
    [DataRow("WRITEFUNCTION", CurlOption.Writefunction, 20011, CurlOptionType.Function)]
    public void ByName_FindsTheRowWithUpstreamsNumberAndType(string name, CurlOption id, int number, CurlOptionType type)
    {
        var row = CurlOptions.ByName(name);

        Assert.AreEqual(new CurlEasyOption(name, id, type, CurlEasyOptionFlags.None), row);
        Assert.AreEqual(number, (int)id);
    }

    [TestMethod]
    [DataRow("url")]
    [DataRow("Url")]
    [DataRow("uRL")]
    public void ByName_IgnoresTheCaseOfAsciiLetters(string name)
    {
        Assert.AreEqual("URL", CurlOptions.ByName(name)?.Name);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("CURLOPT_URL")]
    [DataRow("NO_SUCH_OPTION")]
    [DataRow("fıle")]
    public void ByName_UnknownNameOrNonAsciiCaseFold_ReturnsNull(string name)
    {
        Assert.IsNull(CurlOptions.ByName(name));
    }

    [TestMethod]
    public void ByName_AnAliasName_FindsItsOwnAliasRow()
    {
        var row = CurlOptions.ByName("writeheader");

        Assert.AreEqual(new CurlEasyOption("WRITEHEADER", CurlOption.Headerdata, CurlOptionType.CallbackPointer, CurlEasyOptionFlags.Alias), row);
        Assert.IsTrue(row!.IsAlias);
    }

    [TestMethod]
    public void ById_AnAliasedNumber_ReturnsTheRealRowNeverTheAlias()
    {
        Assert.AreEqual("HEADERDATA", CurlOptions.ById(CurlOption.Writeheader)?.Name);
        Assert.IsFalse(CurlOptions.ById(CurlOption.Headerdata)!.IsAlias);
    }

    [TestMethod]
    public void ById_EveryNonAliasRow_FindsItself()
    {
        foreach (var row in CurlOptions.All.Where(row => !row.IsAlias))
        {
            Assert.AreSame(row, CurlOptions.ById(row.Id));
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(8)]
    [DataRow(99999)]
    public void ById_ANumberNoOptionHas_ReturnsNull(int number)
    {
        Assert.IsNull(CurlOptions.ById((CurlOption)number));
    }

    [TestMethod]
    public void Next_FromNullToTheEnd_WalksEveryRowInOrder()
    {
        var walked = new List<CurlEasyOption>();
        for (var row = CurlOptions.Next(null); row is not null; row = CurlOptions.Next(row))
        {
            walked.Add(row);
        }

        CollectionAssert.AreEqual(CurlOptions.All.ToArray(), walked);
        Assert.AreEqual("ABSTRACT_UNIX_SOCKET", walked[0].Name);
        Assert.AreEqual("XOAUTH2_BEARER", walked[^1].Name);
    }

    [TestMethod]
    public void Next_ARowNotInTheTable_ReturnsNull()
    {
        Assert.IsNull(CurlOptions.Next(new CurlEasyOption("NOT_AN_OPTION", (CurlOption)1, CurlOptionType.Long, CurlEasyOptionFlags.None)));
    }
}
