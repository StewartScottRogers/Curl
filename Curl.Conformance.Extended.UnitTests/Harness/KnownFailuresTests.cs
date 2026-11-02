using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Harness;

[TestClass]
public sealed class KnownFailuresTests
{
    private const string List = "# Known failures\n\nCoap:A-2 BL-9002\r\n  Coap:A-1   BL-9001  \n";

    [TestMethod]
    public void TheListAgreesWithTheTestsThatCarrySpecRequirement()
    {
        KnownFailures knownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

        IReadOnlyList<string> problems = knownFailures.FindProblems(RequirementMatrixTests.CarriersInThisAssembly());

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void Parse_ReadsIdsInAnyOrderAndSkipsCommentsAndBlankLines()
    {
        KnownFailures knownFailures = KnownFailures.Parse(List);

        CollectionAssert.AreEquivalent(new[] { "Coap:A-1", "Coap:A-2" }, knownFailures.RequirementIds.ToArray());
        Assert.IsTrue(knownFailures.Lists("Coap:A-1"));
        Assert.IsFalse(knownFailures.Lists("Coap:A-3"));
        Assert.AreEqual(0, knownFailures.FindProblems([new SpecRequirementCarrier("T.M", ["Coap:A-1", "Coap:A-2"])]).Count);
    }

    [TestMethod]
    public void Load_MissingFile_ListsNothing()
    {
        Assert.AreEqual(0, KnownFailures.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt")).RequirementIds.Count);
    }

    [TestMethod]
    public void Load_ReadsTheFile()
    {
        string path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "KnownFailures.txt");
        try
        {
            File.WriteAllText(path, List);

            Assert.HasCount(2, KnownFailures.Load(path).RequirementIds);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [TestMethod]
    public void FindProblems_ListedIdNoTestCarries_Fails()
    {
        IReadOnlyList<string> problems = KnownFailures.Parse(List).FindProblems([new SpecRequirementCarrier("T.M", ["Coap:A-1"])]);

        CollectionAssert.AreEqual(
            new[] { "KnownFailures.txt lists Coap:A-2, which no test carries: remove its KnownFailures.txt line." },
            problems.ToArray());
    }

    [TestMethod]
    public void FindProblems_MalformedAndRepeatedLines_Fail()
    {
        IReadOnlyList<string> problems = KnownFailures.Parse("Coap:A-1\nCoap:A-1 9001\nCoap:A-1 BL-1 extra\nCoap:A-1 BL-1\nCoap:A-1 BL-2\n")
            .FindProblems([new SpecRequirementCarrier("T.M", ["Coap:A-1"])]);

        CollectionAssert.AreEqual(
            new[]
            {
                "KnownFailures.txt line 1 is not '<requirement id> BL-####': Coap:A-1",
                "KnownFailures.txt line 2 is not '<requirement id> BL-####': Coap:A-1 9001",
                "KnownFailures.txt line 3 is not '<requirement id> BL-####': Coap:A-1 BL-1 extra",
                "KnownFailures.txt lists Coap:A-1 twice.",
            },
            problems.ToArray());
    }

    [TestMethod]
    public async Task RunCaseAsync_UnlistedCasePasses_Passes()
    {
        bool ran = false;

        await KnownFailures.Parse(List).RunCaseAsync(["Coap:A-3"], () => { ran = true; return Task.CompletedTask; });

        Assert.IsTrue(ran);
    }

    [TestMethod]
    public async Task RunCaseAsync_UnlistedCaseFails_Fails()
    {
        await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
            KnownFailures.Parse(List).RunCaseAsync(["Coap:A-3"], () => throw new AssertFailedException("still wrong")));
    }

    [TestMethod]
    public async Task RunCaseAsync_ListedCaseStillFails_Passes()
    {
        await KnownFailures.Parse(List).RunCaseAsync(["Coap:A-3", "Coap:A-2"], () => throw new AssertFailedException("still wrong"));
    }

    [TestMethod]
    public async Task RunCaseAsync_ListedCasePasses_FailsAskingForItsLineToBeRemoved()
    {
        KnownFailurePassedException exception = await Assert.ThrowsExactlyAsync<KnownFailurePassedException>(() =>
            KnownFailures.Parse(List).RunCaseAsync(["Coap:A-2"], () => Task.CompletedTask));

        Assert.AreEqual("Coap:A-2 passes now: remove its KnownFailures.txt line (BL-9002).", exception.Message);
    }
}
