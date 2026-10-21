using Curl.Conformance.UnitProgram.Limits;

namespace Curl.Conformance.UnitProgram.UnitTests.Limits;

[TestClass]
public sealed class Unit1605NegativeLengthsTests
{
    [TestMethod]
    public async Task RunAsync_CurlStrings_BothChecksPassAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1605NegativeLengths(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1605", new Unit1605NegativeLengths().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_NegativeLengthsAnswerText_BothChecksFail()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1605NegativeLengths((_, _, _) => string.Empty, (_, _, _) => []), standardError);

        string line = "unit1605: negative string length cannot work" + Environment.NewLine;
        Assert.AreEqual(2, result.ExitCode);
        Assert.AreEqual(line + line, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1605NegativeLengths().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1605NegativeLengths program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1605, []) { StandardError = standardError }, CancellationToken.None);
}
