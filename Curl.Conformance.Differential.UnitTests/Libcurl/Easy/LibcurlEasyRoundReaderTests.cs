using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Easy;

[TestClass]
public sealed class LibcurlEasyRoundReaderTests
{
    [TestMethod]
    public void Read_EveryOptionKind_ReadsEachValue()
    {
        const string Json = """
            [{"opts": [[20011, "W", null], [20079, "H", null], [10002, "s", "http://h/"], [10004, "s", null], [64, "l", -2], [10023, "L", ["X-A: 1", "B:"]]],
              "info": [1048577], "hq": [["x-a", 1, 4, -1], [null, 0, 1, 0]], "hwalk": [[31, 5]], "reset": true}]
            """;

        var round = LibcurlEasyRoundReader.Read(Json).Single();

        CollectionAssert.AreEqual(
            new[] { LibcurlEasyOptionKind.WriteCallback, LibcurlEasyOptionKind.HeaderCallback, LibcurlEasyOptionKind.Text, LibcurlEasyOptionKind.Text, LibcurlEasyOptionKind.Number, LibcurlEasyOptionKind.List },
            round.Options.Select(o => o.Kind).ToArray());
        Assert.AreEqual("http://h/", round.Options[2].Text);
        Assert.IsNull(round.Options[3].Text);
        Assert.AreEqual(-2, round.Options[4].Number);
        CollectionAssert.AreEqual(new[] { "X-A: 1", "B:" }, round.Options[5].List.ToArray());
        CollectionAssert.AreEqual(new[] { 1048577 }, round.Info.ToArray());
        Assert.AreEqual(new LibcurlEasyHeaderQuery("x-a", 1, 4, -1), round.HeaderQueries[0]);
        Assert.IsNull(round.HeaderQueries[1].Name);
        Assert.AreEqual(new LibcurlEasyHeaderWalk(31, 5), round.HeaderWalks.Single());
        Assert.IsTrue(round.Resets);
    }

    [TestMethod]
    public void Read_NoQueriesWalksOrReset_TakesNone()
    {
        var round = LibcurlEasyRoundReader.Read("""[{"opts": [], "info": []}]""").Single();

        Assert.IsEmpty(round.HeaderQueries);
        Assert.IsEmpty(round.HeaderWalks);
        Assert.IsFalse(round.Resets);
    }

    [TestMethod]
    public void Read_UnknownKind_Throws() =>
        Assert.ThrowsExactly<FormatException>(() => LibcurlEasyRoundReader.Read("""[{"opts": [[1, "x", 0]], "info": []}]"""));

    [TestMethod]
    public void ReadEmbedded_TesterRoundFiles_ReadThem()
    {
        Assert.HasCount(28, LibcurlEasyRoundReader.ReadEmbedded("LibcurlEasy/EasyRounds.json").Single().Info);
        Assert.HasCount(36, LibcurlEasyRoundReader.ReadEmbedded("LibcurlEasy/HeaderRound.json").Single().HeaderWalks);
    }

    [TestMethod]
    public void ReadEmbedded_NoSuchFile_Throws() =>
        Assert.ThrowsExactly<FileNotFoundException>(() => LibcurlEasyRoundReader.ReadEmbedded("LibcurlEasy/None.json"));
}
