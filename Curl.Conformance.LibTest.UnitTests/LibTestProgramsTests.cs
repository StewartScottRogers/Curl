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
    public void Ported_HoldsLib500ToLib519AndLib530()
    {
        string[] names = [.. LibTestPrograms.Ported.ProgramNames.Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(new[] { "lib500", "lib501", "lib502", "lib503", "lib504", "lib505", "lib506", "lib507", "lib508", "lib509", "lib510", "lib511", "lib512", "lib513", "lib514", "lib515", "lib516", "lib517", "lib518", "lib519", "lib530" }, names);
    }

    private sealed class NamedProgram(string programName) : ILibTest
    {
        public string ProgramName => programName;

        public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
            Task.FromResult(LibTestResult.Success);
    }
}
