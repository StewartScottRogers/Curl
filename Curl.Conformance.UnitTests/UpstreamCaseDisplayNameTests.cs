using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins how <see cref="UpstreamCaseDisplayName"/> names an upstream case's test row.</summary>
[TestClass]
public sealed class UpstreamCaseDisplayNameTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void For_CaseWithServerAndName_NamesNumberServersAndName()
    {
        UpstreamTestCase testCase = Parse("<client>\n<server>\nhttp\n</server>\n<name>\nHTTP cookies in a folded header\n</name>\n</client>\n");

        string name = UpstreamCaseDisplayName.For(798, testCase);

        Expect("test798 [http]: HTTP cookies in a folded header", name);
    }

    [TestMethod]
    public void For_SeveralServersAndAFoldedName_JoinsServersAndFoldsTheName()
    {
        UpstreamTestCase testCase = Parse("<client>\n<server>\nhttps test-localhost.pem\nhttp-proxy\n</server>\n<name>\nHTTPS over\n\tproxy\n</name>\n</client>\n");

        string name = UpstreamCaseDisplayName.For(1, testCase);

        Expect("test1 [https, http-proxy]: HTTPS over proxy", name);
    }

    [TestMethod]
    public void For_LongName_IsCutToEightyCharactersEndingInDots()
    {
        UpstreamTestCase testCase = Parse($"<client>\n<name>\n{new string('n', 100)}\n</name>\n</client>\n");

        string name = UpstreamCaseDisplayName.For(2, testCase);

        Expect($"test2: {new string('n', 80 - "test2: ".Length - 3)}...", name);
    }

    [TestMethod]
    public void For_CaseThatDidNotParse_IsTheNumberAlone()
    {
        string name = UpstreamCaseDisplayName.For(3, null);

        Expect("test3", name);
    }

    [TestMethod]
    public void Servers_CaseWithoutServerPart_IsEmpty()
    {
        UpstreamTestCase testCase = Parse("<client>\n<name>\n \n</name>\n</client>\n");

        IReadOnlyList<string> servers = UpstreamCaseDisplayName.Servers(testCase);

        TestDiagnostics.For(TestContext).Act("servers", string.Join(",", servers));
        Assert.IsEmpty(servers);
        Assert.AreEqual("test4", UpstreamCaseDisplayName.For(4, testCase));
    }

    private void Expect(string expected, string name)
    {
        TestDiagnostics.For(TestContext).Diff("display name", expected, name);
        Assert.AreEqual(expected, name);
    }

    private static UpstreamTestCase Parse(string text) =>
        UpstreamTestCaseParser.Parse(Encoding.ASCII.GetBytes($"<testcase>\n{text}</testcase>\n")).TestCase!;
}
