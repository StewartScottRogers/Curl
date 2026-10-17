namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestProgramsTests
{
    [TestMethod]
    public void Find_RegisteredProgram_IsThatProgram()
    {
        NamedProgram lib500 = new("lib500");
        LibTestPrograms programs = new([lib500, new NamedProgram("lib501")]);

        ILibTest? found = programs.Find("lib500");

        Assert.AreSame(lib500, found);
        CollectionAssert.AreEquivalent(new[] { "lib500", "lib501" }, programs.ProgramNames.ToArray());
    }

    [TestMethod]
    public void Find_ProgramNotPorted_IsNull()
    {
        LibTestPrograms programs = new([new NamedProgram("lib500")]);

        ILibTest? found = programs.Find("lib502");

        Assert.IsNull(found);
    }

    [TestMethod]
    public void Constructor_TwoProgramsOfOneName_Throws()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new LibTestPrograms([new NamedProgram("lib500"), new NamedProgram("lib500")]));

        StringAssert.StartsWith(exception.Message, "Two programs are named lib500.");
    }

    [TestMethod]
    public void Constructor_NullList_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestPrograms(null!));
    }

    [TestMethod]
    public void Constructor_NullProgram_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestPrograms([null!]));

        Assert.AreEqual("programs", exception.ParamName);
    }

    [TestMethod]
    public void Ported_BeforeTheFirstPort_IsEmpty()
    {
        Assert.IsEmpty(LibTestPrograms.Ported.ProgramNames);
    }

    private sealed class NamedProgram(string programName) : ILibTest
    {
        public string ProgramName => programName;

        public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
            Task.FromResult(LibTestResult.Success);
    }
}
