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
    public void Ported_HoldsLib500ToLib521Lib523ToLib526Lib530Lib533Lib536ToLib544Lib547Lib549Lib552ToLib556Lib558ToLib560Lib562Lib564Lib566ToLib576Lib578Lib579Lib582Lib583Lib589ToLib591AndLib597ToLib599()
    {
        string[] names = [.. LibTestPrograms.Ported.ProgramNames.Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(new[] { "lib500", "lib501", "lib502", "lib503", "lib504", "lib505", "lib506", "lib507", "lib508", "lib509", "lib510", "lib511", "lib512", "lib513", "lib514", "lib515", "lib516", "lib517", "lib518", "lib519", "lib520", "lib521", "lib523", "lib524", "lib525", "lib526", "lib530", "lib533", "lib536", "lib537", "lib539", "lib540", "lib541", "lib542", "lib543", "lib544", "lib547", "lib549", "lib552", "lib553", "lib554", "lib555", "lib556", "lib558", "lib559", "lib560", "lib562", "lib564", "lib566", "lib567", "lib568", "lib569", "lib570", "lib571", "lib572", "lib573", "lib574", "lib575", "lib576", "lib578", "lib579", "lib582", "lib583", "lib589", "lib590", "lib591", "lib597", "lib598", "lib599" }, names);
    }

    private sealed class NamedProgram(string programName) : ILibTest
    {
        public string ProgramName => programName;

        public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
            Task.FromResult(LibTestResult.Success);
    }
}
