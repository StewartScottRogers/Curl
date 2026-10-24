using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Easy;

[TestClass]
public sealed class LibcurlHeaderCasesTests
{
    [TestMethod]
    public void All_HasEveryPathTwiceAndTheProxyCase()
    {
        var cases = LibcurlHeaderCases.All;

        Assert.HasCount(51, cases);
        Assert.AreEqual("/dupheaders", cases[0].Name);
        Assert.AreEqual("/dupheaders follow", cases[1].Name);
        Assert.AreEqual("proxy-connect", cases[^1].Name);
    }

    [TestMethod]
    public void All_PathCase_HasHdrtestsQueriesAndWalks()
    {
        var round = LibcurlHeaderCases.All[1].Rounds.Single();

        Assert.HasCount(492, round.HeaderQueries);
        Assert.HasCount(36, round.HeaderWalks);
        Assert.AreEqual(new LibcurlEasyHeaderQuery(null, 0, 1, 0), round.HeaderQueries[^1]);
        Assert.AreEqual("http://127.0.0.1:18080/dupheaders", round.Options[2].Text);
        Assert.AreEqual(52, round.Options[3].Id);
        Assert.IsEmpty(round.Info);
    }

    [TestMethod]
    public void All_ProxyCase_MatchesTheTestersRound()
    {
        var round = LibcurlHeaderCases.All[^1].Rounds.Single();
        var tester = LibcurlEasyRoundReader.ReadEmbedded("LibcurlEasy/HeaderRound.json").Single();

        CollectionAssert.AreEqual(tester.Options.Select(o => (o.Id, o.Kind, o.Number, o.Text)).ToArray(), round.Options.Select(o => (o.Id, o.Kind, o.Number, o.Text)).ToArray());
        CollectionAssert.AreEqual(tester.HeaderQueries.ToArray(), round.HeaderQueries.ToArray());
        CollectionAssert.AreEqual(tester.HeaderWalks.ToArray(), round.HeaderWalks.ToArray());
    }
}
