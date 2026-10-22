using System.Text;
using Curl.Conformance.UnitProgram.UrlParsing;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1627SchemeLookupTests
{
    [TestMethod]
    public async Task RunAsync_CurlSchemes_Writes547InvokesAndPasses()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1627SchemeLookup(), standardOutput);

        Assert.AreEqual("547 invokes\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1627", new Unit1627SchemeLookup().ProgramName);
    }

    [TestMethod]
    [DataRow("SOCKS5H", "socks5h")]
    [DataRow("Http", "http")]
    [DataRow("FILE", "file")]
    [DataRow("p+", null)]
    public void FindKnownScheme_Name_ReturnsLowerCaseSchemeOrNull(string name, string? expected)
    {
        Assert.AreEqual(expected, Unit1627SchemeLookup.FindKnownScheme(name));
    }

    [TestMethod]
    public async Task RunAsync_NothingIsFound_StopsAtTheFirstOkayRowAndFailsInit()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1627SchemeLookup(_ => null), standardOutput);

        Assert.AreEqual("Input: DICT, expected okay\n" + "451 invokes\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(2, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_ADifferentSchemeIsFound_StopsAtTheFirstOkayRow()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1627SchemeLookup(_ => "http"), standardOutput);

        StringAssert.StartsWith(Encoding.ASCII.GetString(standardOutput.ToArray()), "Input: DICT, expected okay\n");
        Assert.AreEqual(2, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_SchemeIsNotLowerCase_StopsAndSaysSo()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1627SchemeLookup(name => name.ToUpperInvariant()), standardOutput);

        StringAssert.StartsWith(Encoding.ASCII.GetString(standardOutput.ToArray()), "Input: dict is not lowercase: DICT\n");
        Assert.AreEqual(2, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_ANotOkayNameIsFound_StopsTheSecondLoop()
    {
        using MemoryStream standardOutput = new();
        Unit1627SchemeLookup program = new(name => name == "tt" ? "tt" : Unit1627SchemeLookup.FindKnownScheme(name));

        UnitProgramResult result = await RunAsync(program, standardOutput);

        Assert.AreEqual("Input: tt, expected not okay\n" + "99 invokes\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(2, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1627SchemeLookup().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1627SchemeLookup program, MemoryStream standardOutput) =>
        program.RunAsync(new UnitProgramContext(1627, []) { StandardOutput = standardOutput }, CancellationToken.None);
}
