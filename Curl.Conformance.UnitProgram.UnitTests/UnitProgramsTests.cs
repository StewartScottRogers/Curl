using System.Reflection;

namespace Curl.Conformance.UnitProgram.UnitTests;

[TestClass]
public sealed class UnitProgramsTests
{
    [TestMethod]
    public void Find_RegisteredProgram_IsThatProgram()
    {
        NamedProgram unit1302 = new("unit1302");
        UnitPrograms programs = new([unit1302, new NamedProgram("tool1394")]);

        IUnitProgram? found = programs.Find("unit1302");

        Assert.AreSame(unit1302, found);
        CollectionAssert.AreEquivalent(new[] { "unit1302", "tool1394" }, programs.ProgramNames.ToArray());
    }

    [TestMethod]
    public void Find_ProgramNotPorted_IsNull()
    {
        UnitPrograms programs = new([new NamedProgram("unit1302")]);

        IUnitProgram? found = programs.Find("unit1303");

        Assert.IsNull(found);
    }

    [TestMethod]
    public void Constructor_TwoProgramsOfOneName_Throws()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new UnitPrograms([new NamedProgram("unit1302"), new NamedProgram("unit1302")]));

        StringAssert.StartsWith(exception.Message, "Two programs are named unit1302.");
    }

    [TestMethod]
    public void Constructor_NullList_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UnitPrograms(null!));
    }

    [TestMethod]
    public void Constructor_NullProgram_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new UnitPrograms([null!]));

        Assert.AreEqual("programs", exception.ParamName);
    }

    [TestMethod]
    public void Ported_EveryProgramHasANameOfItsOwn()
    {
        string[] names = [.. UnitPrograms.Ported.ProgramNames];

        Assert.AreEqual(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void Ported_HoldsTheProgramsOfEveryAreaFile()
    {
        string[] areas = ["Hashes", "Asn1", "UrlParsing", "ToolParsers", "Headers", "Dns", "Limits"];
        List<string> expected = [];
        foreach (string area in areas)
        {
            PropertyInfo property = typeof(UnitPrograms).GetProperty(area + "Programs", BindingFlags.NonPublic | BindingFlags.Static)!;
            expected.AddRange(((IUnitProgram[])property.GetValue(null)!).Select(program => program.ProgramName));
        }

        CollectionAssert.AreEquivalent(expected, UnitPrograms.Ported.ProgramNames.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_TrivialProgram_ReturnsItsFailureCountAsTheExitCode()
    {
        UnitPrograms programs = new([new NamedProgram("unit1302", failures: 2)]);

        UnitProgramResult result = await programs.Find("unit1302")!.RunAsync(new UnitProgramContext(1302, []), CancellationToken.None);

        Assert.AreEqual(2, result.ExitCode);
    }

    private sealed class NamedProgram(string programName, int failures = 0) : IUnitProgram
    {
        public string ProgramName => programName;

        public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new UnitProgramResult(failures));
    }
}
