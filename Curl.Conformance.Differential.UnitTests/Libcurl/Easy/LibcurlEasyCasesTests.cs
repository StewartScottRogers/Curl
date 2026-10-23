using System.Text.Json;
using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Easy;

[TestClass]
public sealed class LibcurlEasyCasesTests
{
    private const string ResourceName = "Curl.Conformance.Differential.Cases.LibcurlEasyCases.EasyCases.json";

    [TestMethod]
    public void EasyCases_EveryEasytestCase_ReadsWithRoundReaderUnderAUniqueName()
    {
        using var stream = typeof(LibcurlEasyRoundReader).Assembly.GetManifestResourceStream(ResourceName);
        Assert.IsNotNull(stream);
        using var document = JsonDocument.Parse(stream);

        var cases = document.RootElement.EnumerateArray()
            .Select(easyCase => (Name: easyCase.GetProperty("name").GetString()!, Rounds: LibcurlEasyRoundReader.Read(easyCase.GetProperty("rounds").GetRawText())))
            .ToList();

        Assert.HasCount(467, cases);
        Assert.HasCount(cases.Count, cases.Select(easyCase => easyCase.Name).Distinct(StringComparer.Ordinal));
        Assert.IsTrue(cases.All(easyCase => easyCase.Rounds.Count > 0));
        var cookieClear = cases.Single(easyCase => easyCase.Name == "cookie-session-clear").Rounds;
        Assert.HasCount(3, cookieClear);
        Assert.AreEqual("ALL", cookieClear[1].Options.Single(option => option.Id == 10031).Text);
        Assert.IsTrue(cases.Single(easyCase => easyCase.Name == "reset-between").Rounds[0].Resets);
        Assert.HasCount(29, cases.Single(easyCase => easyCase.Name == "redirect-url-info").Rounds[0].Info);
    }
}
